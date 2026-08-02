using MassTransit;
using Microsoft.EntityFrameworkCore;
using InventoryWorker.Application.Consumers;
using InventoryWorker.Domain.Entities;
using InventoryWorker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configurar base de datos (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=orderflow_db;Username=orderflow_user;Password=orderflow_pass_secret;Port=5432";
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Configurar MassTransit con RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

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

var host = builder.Build();

// 3. Inicializar base de datos y semilla de datos (Seed Data)
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Asegurando la creación de la base de datos de inventario...");
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.Stocks.AnyAsync())
        {
            logger.LogInformation("La tabla Stocks está vacía. Insertando productos de semilla...");
            dbContext.Stocks.AddRange(
                new Stock("ABC-01", 10),
                new Stock("ABC-02", 5),
                new Stock("ABC-03", 0)
            );
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Productos de semilla insertados con éxito.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrió un error al inicializar la base de datos o al aplicar la semilla.");
    }
}

host.Run();
