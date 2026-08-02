using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using OrdersApi.Application.Consumers;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;
using OrdersApi.Domain.Entities;
using OrdersApi.Domain.Enums;
using OrdersApi.Presentation.Hubs;
using Xunit;

namespace OrderFlow.Tests;

public class StockRejectedConsumerTests
{
    [Fact]
    public async Task Consume_StockRejectedEvent_ShouldTransitionOrderStatusToRejected()
    {
        var order = new Order("Maria Lopez", "ABC-03", 3);
        order.Estado.Should().Be(OrderStatus.Pending);

        var orderRepoMock = new Mock<IOrderRepository>();
        orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var hubContextMock = new Mock<IHubContext<OrderHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);

        var loggerMock = new Mock<ILogger<StockRejectedConsumer>>();

        var consumer = new StockRejectedConsumer(orderRepoMock.Object, hubContextMock.Object, loggerMock.Object);

        var stockRejectedEvent = new StockRejectedEvent(
            EventId: Guid.NewGuid(),
            OrderId: order.Id,
            Motivo: "Stock insuficiente o SKU no encontrado",
            ProcesadoEn: DateTime.UtcNow
        );

        var contextMock = new Mock<ConsumeContext<StockRejectedEvent>>();
        contextMock.Setup(c => c.Message).Returns(stockRejectedEvent);

        await consumer.Consume(contextMock.Object);

        order.Estado.Should().Be(OrderStatus.Rejected);
        orderRepoMock.Verify(
            r => r.UpdateAsync(It.Is<Order>(o => o.Id == order.Id && o.Estado == OrderStatus.Rejected), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
