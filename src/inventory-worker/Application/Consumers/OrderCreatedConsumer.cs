using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Contracts.Events;
using InventoryWorker.Domain.Entities;
using InventoryWorker.Infrastructure.Persistence;

namespace InventoryWorker.Application.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(InventoryDbContext dbContext, ILogger<OrderCreatedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var @event = context.Message;
        _logger.LogInformation("Procesando OrderCreatedEvent para OrderId: {OrderId}, EventId: {EventId}", @event.OrderId, @event.EventId);

        // Paso 1 (Idempotencia): Revisa si el eventId ya existe
        var processedEvent = await _dbContext.ProcessedEvents
            .FirstOrDefaultAsync(pe => pe.EventId == @event.EventId);

        if (processedEvent != null)
        {
            _logger.LogWarning("El evento con EventId: {EventId} ya fue procesado previamente. Re-publicando respuesta.", @event.EventId);
            await PublishOutcomeAsync(context, @event.OrderId, processedEvent.Resultado, processedEvent.MotivoRechazo);
            return;
        }

        // Paso 2: Ejecutar lógica en una transacción atómica
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Volver a verificar dentro de la transacción por si acaso
            processedEvent = await _dbContext.ProcessedEvents
                .FirstOrDefaultAsync(pe => pe.EventId == @event.EventId);
            
            if (processedEvent != null)
            {
                _logger.LogWarning("El evento con EventId: {EventId} ya fue procesado previamente (detectado en tx). Re-publicando respuesta.", @event.EventId);
                await PublishOutcomeAsync(context, @event.OrderId, processedEvent.Resultado, processedEvent.MotivoRechazo);
                return;
            }

            var stock = await _dbContext.Stocks.FirstOrDefaultAsync(s => s.Sku == @event.Sku);
            string resultado;
            string? motivoRechazo = null;

            if (stock == null)
            {
                resultado = "Rejected";
                motivoRechazo = "Stock insuficiente o SKU no encontrado";
                _logger.LogWarning("Reserva rechazada: SKU {Sku} no encontrado para la Orden: {OrderId}", @event.Sku, @event.OrderId);
            }
            else if (!stock.HasSufficientStock(@event.Cantidad))
            {
                resultado = "Rejected";
                motivoRechazo = "Stock insuficiente o SKU no encontrado";
                _logger.LogWarning("Reserva rechazada: Stock insuficiente para SKU {Sku} (Cantidad: {Cantidad}, Disponible: {Disponible})", @event.Sku, @event.Cantidad, stock.Disponibilidad);
            }
            else
            {
                resultado = "Reserved";
                stock.DeductStock(@event.Cantidad);
                _logger.LogInformation("Reserva de stock exitosa para SKU: {Sku}, Cantidad: {Cantidad}", @event.Sku, @event.Cantidad);
            }

            // Registrar evento procesado
            var newProcessedEvent = new ProcessedEvent(@event.EventId, nameof(OrderCreatedEvent), resultado, motivoRechazo);
            await _dbContext.ProcessedEvents.AddAsync(newProcessedEvent);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            // Publicar el evento de respuesta fuera de la transacción para evitar bloqueos
            await PublishOutcomeAsync(context, @event.OrderId, resultado, motivoRechazo);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error procesando el stock para la orden {OrderId}", @event.OrderId);
            throw;
        }
    }

    private async Task PublishOutcomeAsync(ConsumeContext<OrderCreatedEvent> context, Guid orderId, string resultado, string? motivoRechazo)
    {
        if (resultado == "Reserved")
        {
            var response = new StockReservedEvent(
                EventId: Guid.NewGuid(),
                OrderId: orderId,
                ProcesadoEn: DateTime.UtcNow
            );
            await context.Publish(response);
            _logger.LogInformation("Publicado StockReservedEvent para OrderId: {OrderId}", orderId);
        }
        else
        {
            var response = new StockRejectedEvent(
                EventId: Guid.NewGuid(),
                OrderId: orderId,
                Motivo: motivoRechazo ?? "Stock insuficiente o SKU no encontrado",
                ProcesadoEn: DateTime.UtcNow
            );
            await context.Publish(response);
            _logger.LogInformation("Publicado StockRejectedEvent para OrderId: {OrderId}, Motivo: {Motivo}", orderId, response.Motivo);
        }
    }
}
