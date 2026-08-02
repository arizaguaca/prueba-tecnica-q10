namespace InventoryWorker.Application.Contracts.Events;

public record StockRejectedEvent(
    Guid EventId,
    Guid OrderId,
    string Motivo,
    DateTime ProcesadoEn
);
