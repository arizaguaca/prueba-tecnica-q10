using System.Text.Json.Serialization;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrdersApi.Application.Consumers;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Validation;
using OrdersApi.Infrastructure.Configuration;
using OrdersApi.Infrastructure.Messaging;
using OrdersApi.Infrastructure.Persistence;
using OrdersApi.Presentation.Endpoints;
using OrdersApi.Presentation.Hubs;
using OrdersApi.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar Servicios Core / Endpoints API Explorer / Swagger / CORS / SignalR
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2. Base de Datos (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection no está configurada. " +
        "Define ConnectionStrings__DefaultConnection o copia appsettings.Development.json.example.");
}
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Configuración tipada RabbitMQ
builder.Services.AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 4. Registro de Repositorio y Event Publisher
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductCatalogRepository, ProductCatalogRepository>();
builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

// 5. Registro de Validadores (FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// 6. Health Checks
var rabbitConfig = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("RabbitMQ no está configurado.");
var rabbitUri = new Uri($"amqp://{rabbitConfig.Username}:{rabbitConfig.Password}@{rabbitConfig.Host}:5672/");

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRabbitMQ(rabbitUri, name: "rabbitmq");

// 7. Configuración de MassTransit con RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StockReservedConsumer>();
    x.AddConsumer<StockRejectedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        cfg.Host(rabbitOptions.Host, "/", h =>
        {
            h.Username(rabbitOptions.Username);
            h.Password(rabbitOptions.Password);
        });

        cfg.UseRawJsonSerializer(RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders, isDefault: true);

        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("order-created-event"));
        cfg.Message<StockReservedEvent>(m => m.SetEntityName("stock-reserved-event"));
        cfg.Message<StockRejectedEvent>(m => m.SetEntityName("stock-rejected-event"));

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapOrderEndpoints();
app.MapHub<OrderHub>("/hubs/orders");

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    var retries = 5;

    while (retries > 0)
    {
        try
        {
            logger.LogInformation("Asegurando la creación de la tabla Orders...");

            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Orders"" (
                    ""Id""             uuid                     NOT NULL,
                    ""ClienteNombre""  varchar(200)             NOT NULL,
                    ""Sku""            varchar(100)             NOT NULL,
                    ""Cantidad""       integer                  NOT NULL,
                    ""Estado""         text                     NOT NULL,
                    ""CreadoEn""       timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_Orders"" PRIMARY KEY (""Id"")
                );
            ");

            logger.LogInformation("Tabla Orders lista.");
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogWarning(ex, "Error al conectar con la base de datos. Reintentos restantes: {Retries}", retries);
            if (retries == 0)
            {
                logger.LogError(ex, "No se pudo conectar a la base de datos después de varios reintentos.");
            }
            else
            {
                await Task.Delay(3000);
            }
        }
    }
}

app.Run();
