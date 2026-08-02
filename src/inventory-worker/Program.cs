using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using InventoryWorker.Application.Consumers;
using InventoryWorker.Application.Contracts.Events;
using InventoryWorker.Domain.Entities;
using InventoryWorker.Infrastructure.Configuration;
using InventoryWorker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configurar base de datos (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection no está configurada. " +
        "Define ConnectionStrings__DefaultConnection o copia appsettings.Development.json.example.");
}
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 2. Configurar MassTransit con RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        cfg.Host(rabbitOptions.Host, "/", h =>
        {
            h.Username(rabbitOptions.Username);
            h.Password(rabbitOptions.Password);
        });

        cfg.UseRawJsonSerializer(RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders, isDefault: true);

        // Mismo entity name en ambos servicios para que publish/consume usen el mismo exchange
        cfg.Message<OrderCreatedEvent>(m => m.SetEntityName("order-created-event"));
        cfg.Message<StockReservedEvent>(m => m.SetEntityName("stock-reserved-event"));
        cfg.Message<StockRejectedEvent>(m => m.SetEntityName("stock-rejected-event"));

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

// 3. Inicializar base de datos y semilla de datos (Seed Data)
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var retries = 5;

    while (retries > 0)
    {
        try
        {
            logger.LogInformation("Asegurando la creación de las tablas de inventario...");

            // Crear tablas manualmente con IF NOT EXISTS para coexistir con OrdersDbContext
            // en la misma base de datos compartida.
            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Stocks"" (
                    ""Sku""           varchar(100)  NOT NULL,
                    ""Disponibilidad"" integer       NOT NULL,
                    ""ActualizadoEn"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_Stocks"" PRIMARY KEY (""Sku"")
                );
            ");

            await dbContext.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ProcessedEvents"" (
                    ""EventId""       uuid          NOT NULL,
                    ""TipoEvento""    varchar(200)  NOT NULL,
                    ""Resultado""     varchar(50)   NOT NULL,
                    ""MotivoRechazo"" varchar(500)  NULL,
                    ""ProcesadoEn""   timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_ProcessedEvents"" PRIMARY KEY (""EventId"")
                );
            ");

            logger.LogInformation("Tablas de inventario listas.");

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
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogWarning(ex, "Error al conectar con la base de datos de inventario. Reintentos restantes: {Retries}", retries);
            if (retries == 0)
            {
                logger.LogError(ex, "Ocurrió un error al inicializar la base de datos o al aplicar la semilla.");
            }
            else
            {
                await Task.Delay(3000);
            }
        }
    }
}

host.Run();
