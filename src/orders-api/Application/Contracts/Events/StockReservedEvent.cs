using MassTransit;

namespace OrdersApi.Application.Contracts.Events;

[MessageUrn("OrderFlow:StockReservedEvent")]
public record StockReservedEvent(
    Guid EventId,
    Guid OrderId,
    DateTime ProcesadoEn
);
