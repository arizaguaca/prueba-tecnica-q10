using MassTransit;

namespace OrdersApi.Application.Contracts.Events;

[MessageUrn("OrderFlow:OrderCreatedEvent")]
public record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);
