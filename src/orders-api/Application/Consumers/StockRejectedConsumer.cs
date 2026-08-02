using MassTransit;
using Microsoft.AspNetCore.SignalR;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Interfaces;
using OrdersApi.Presentation.Hubs;

namespace OrdersApi.Application.Consumers;

public class StockRejectedConsumer : IConsumer<StockRejectedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<StockRejectedConsumer> _logger;

    public StockRejectedConsumer(
        IOrderRepository orderRepository,
        IHubContext<OrderHub> hubContext,
        ILogger<StockRejectedConsumer> logger)
    {
        _orderRepository = orderRepository;
        _hubContext = hubContext;
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
            _logger.LogWarning("Pedido {OrderId} no fue encontrado para procesar StockRejectedEvent.", message.OrderId);
        }
    }
}
