using System.Text.Json.Serialization;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Consumers;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;
using OrdersApi.Application.Validation;
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

// 3. Registro de Repositorio y Event Publisher
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

// 4. Registro de Validadores (FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// 5. Configuración de MassTransit con RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<StockReservedConsumer>();
    x.AddConsumer<StockRejectedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"]
            ?? throw new InvalidOperationException("RabbitMQ:Host no está configurado.");
        var rabbitUser = builder.Configuration["RabbitMQ:Username"]
            ?? throw new InvalidOperationException("RabbitMQ:Username no está configurado.");
        var rabbitPass = builder.Configuration["RabbitMQ:Password"]
            ?? throw new InvalidOperationException("RabbitMQ:Password no está configurado.");

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.UseRawJsonSerializer(RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders, isDefault: true);

        // Mismo entity name en ambos servicios para que publish/consume usen el mismo exchange
        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("order-created-event"));
        cfg.Message<StockReservedEvent>(m => m.SetEntityName("stock-reserved-event"));
        cfg.Message<StockRejectedEvent>(m => m.SetEntityName("stock-rejected-event"));

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Habilitar CORS
app.UseCors("AllowFrontend");

// 6. Aplicar Middleware de Manejo de Errores Global
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 7. Pipeline de HTTP / Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

// 8. Mapear Endpoints de la Aplicación y Hub de SignalR
app.MapOrderEndpoints();
app.MapHub<OrderHub>("/hubs/orders");

// 9. Inicializar tabla Orders al arrancar (CREATE IF NOT EXISTS + reintentos)
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

            // CREATE TABLE IF NOT EXISTS evita la condición de carrera con inventory-worker:
            // EnsureCreatedAsync no crea tablas si la BD ya tiene otras (Stocks, ProcessedEvents).
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
