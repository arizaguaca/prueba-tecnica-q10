using MassTransit;

namespace OrdersApi.Application.Contracts.Events;

[MessageUrn("OrderFlow:StockRejectedEvent")]
public record StockRejectedEvent(
    Guid EventId,
    Guid OrderId,
    string Motivo,
    DateTime ProcesadoEn
);
