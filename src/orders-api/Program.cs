using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Consumers;
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
builder.Services.AddSignalR();
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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=orderflow_db;Username=orderflow_user;Password=orderflow_pass_secret;Port=5432";
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
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

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

app.UseHttpsRedirection();

// 8. Mapear Endpoints de la Aplicación y Hub de SignalR
app.MapOrderEndpoints();
app.MapHub<OrderHub>("/hubs/orders");

// 9. Inicializar / Migrar Base de Datos Automáticamente al arrancar
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error inicializando la base de datos.");
    }
}

app.Run();
