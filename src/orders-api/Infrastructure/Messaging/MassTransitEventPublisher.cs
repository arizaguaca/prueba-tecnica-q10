using MassTransit;
using OrdersApi.Application.Interfaces;

namespace OrdersApi.Infrastructure.Messaging;

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        await _publishEndpoint.Publish(@event);
    }
}
