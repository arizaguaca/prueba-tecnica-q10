namespace OrdersApi.Application.Contracts.Events;

public record StockReservedEvent(
    Guid EventId,
    Guid OrderId,
    DateTime ProcesadoEn
);
