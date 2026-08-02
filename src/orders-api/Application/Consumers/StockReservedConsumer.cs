using MassTransit;
using Microsoft.AspNetCore.SignalR;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces;
using OrdersApi.Presentation.Hubs;

namespace OrdersApi.Application.Consumers;

public class StockReservedConsumer : IConsumer<StockReservedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<StockReservedConsumer> _logger;

    public StockReservedConsumer(
        IOrderRepository orderRepository,
        IHubContext<OrderHub> hubContext,
        ILogger<StockReservedConsumer> logger)
    {
        _orderRepository = orderRepository;
        _hubContext = hubContext;
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

            var response = new OrderResponse(
                order.Id,
                order.ClienteNombre,
                order.Sku,
                order.Cantidad,
                order.Estado,
                order.CreadoEn
            );

            await _hubContext.Clients.All.SendAsync("OrderUpdated", response);
        }
        else
        {
            _logger.LogWarning("Pedido {OrderId} no fue encontrado para procesar StockReservedEvent.", message.OrderId);
        }
    }
}
