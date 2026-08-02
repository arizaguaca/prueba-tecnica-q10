namespace InventoryWorker.Application.Contracts.Events;

public record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);
