using MassTransit;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;

namespace OrdersApi.Application.Consumers;

public class StockReservedConsumer : IConsumer<StockReservedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<StockReservedConsumer> _logger;

    public StockReservedConsumer(IOrderRepository orderRepository, ILogger<StockReservedConsumer> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StockReservedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("StockReservedEvent recibido para OrderId: {OrderId}", message.OrderId);

        var order = await _orderRepository.GetByIdAsync(message.OrderId);
        if (order != null)
        {
            order.ConfirmOrder();
            await _orderRepository.UpdateAsync(order);
            _logger.LogInformation("Pedido {OrderId} confirmado exitosamente.", order.Id);
        }
        else
        {
            _logger.LogWarning("Pedido {OrderId} no fue encontrado para procesar StockReservedEvent.", message.OrderId);
        }
    }
}
