using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using InventoryWorker.Application.Consumers;
using InventoryWorker.Application.Contracts.Events;
using InventoryWorker.Domain.Entities;
using InventoryWorker.Infrastructure.Persistence;
using Xunit;

namespace OrderFlow.Tests;

public class OrderCreatedConsumerTests
{
    private static InventoryDbContext CreateInMemoryDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task Consume_FirstTime_DeductsStockAndSavesProcessedEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var dbContext = CreateInMemoryDbContext(dbName);

        await dbContext.Stocks.AddAsync(new Stock("ABC-01", 10));
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(eventId, orderId, "ABC-01", 2, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        await consumer.Consume(contextMock.Object);

        var updatedStock = await dbContext.Stocks.FirstOrDefaultAsync(s => s.Sku == "ABC-01");
        updatedStock.Should().NotBeNull();
        updatedStock!.Disponibilidad.Should().Be(8);

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == eventId);
        processedEvent.Should().NotBeNull();
        processedEvent!.Resultado.Should().Be("Reserved");

        contextMock.Verify(
            x => x.Publish(It.Is<StockReservedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_DuplicateEventId_DoesNotDeductStockTwice()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var dbContext = CreateInMemoryDbContext(dbName);

        await dbContext.Stocks.AddAsync(new Stock("ABC-01", 10));
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var sameEventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(sameEventId, orderId, "ABC-01", 2, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        await consumer.Consume(contextMock.Object);
        (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad.Should().Be(8);

        await consumer.Consume(contextMock.Object);

        (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad.Should().Be(8);
        (await dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == sameEventId)).Should().Be(1);

        contextMock.Verify(
            x => x.Publish(It.Is<StockReservedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Consume_InsufficientStock_ShouldEmitStockRejectedEventAndNotModifyStock()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var dbContext = CreateInMemoryDbContext(dbName);

        await dbContext.Stocks.AddAsync(new Stock("ABC-02", 5));
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(eventId, orderId, "ABC-02", 20, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        await consumer.Consume(contextMock.Object);

        (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-02")).Disponibilidad.Should().Be(5);

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == eventId);
        processedEvent.Should().NotBeNull();
        processedEvent!.Resultado.Should().Be("Rejected");

        contextMock.Verify(
            x => x.Publish(
                It.Is<StockRejectedEvent>(e => e.OrderId == orderId && e.Motivo.Contains("Stock insuficiente")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_ZeroStock_RejectsOrderAndIsIdempotentOnDuplicate()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var dbContext = CreateInMemoryDbContext(dbName);

        await dbContext.Stocks.AddAsync(new Stock("ABC-03", 0));
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var sameEventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(sameEventId, orderId, "ABC-03", 1, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        await consumer.Consume(contextMock.Object);

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == sameEventId);
        processedEvent.Should().NotBeNull();
        processedEvent!.Resultado.Should().Be("Rejected");

        contextMock.Verify(
            x => x.Publish(It.Is<StockRejectedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);

        await consumer.Consume(contextMock.Object);

        (await dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == sameEventId)).Should().Be(1);

        contextMock.Verify(
            x => x.Publish(It.Is<StockRejectedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
