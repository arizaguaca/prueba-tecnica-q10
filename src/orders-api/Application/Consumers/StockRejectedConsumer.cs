using MassTransit;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;

namespace OrdersApi.Application.Consumers;

public class StockRejectedConsumer : IConsumer<StockRejectedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<StockRejectedConsumer> _logger;

    public StockRejectedConsumer(IOrderRepository orderRepository, ILogger<StockRejectedConsumer> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StockRejectedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("StockRejectedEvent recibido para OrderId: {OrderId}. Motivo: {Motivo}", message.OrderId, message.Motivo);

        var order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order != null)
        {
            order.RejectOrder();
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Pedido {OrderId} rechazado. Motivo: {Motivo}", order.Id, message.Motivo);
        }
        else
        {
            _logger.LogWarning("Pedido {OrderId} no fue encontrado para procesar StockRejectedEvent.", message.OrderId);
        }
    }
}
