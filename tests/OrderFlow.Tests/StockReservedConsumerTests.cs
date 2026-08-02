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

public class StockReservedConsumerTests
{
    [Fact]
    public async Task Consume_StockReservedEvent_ShouldTransitionOrderStatusToConfirmed()
    {
        // Arrange
        var order = new Order("Maria Lopez", "ABC-01", 3);
        order.Estado.Should().Be(OrderStatus.Pending); // Verifica estado inicial en Pending

        var orderRepoMock = new Mock<IOrderRepository>();
        orderRepoMock.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var hubContextMock = new Mock<IHubContext<OrderHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);

        var loggerMock = new Mock<ILogger<StockReservedConsumer>>();

        var consumer = new StockReservedConsumer(orderRepoMock.Object, hubContextMock.Object, loggerMock.Object);

        var stockReservedEvent = new StockReservedEvent(
            EventId: Guid.NewGuid(),
            OrderId: order.Id,
            ProcesadoEn: DateTime.UtcNow
        );

        var contextMock = new Mock<ConsumeContext<StockReservedEvent>>();
        contextMock.Setup(c => c.Message).Returns(stockReservedEvent);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        order.Estado.Should().Be(OrderStatus.Confirmed);
        orderRepoMock.Verify(r => r.UpdateAsync(It.Is<Order>(o => o.Id == order.Id && o.Estado == OrderStatus.Confirmed)), Times.Once);
    }
}
