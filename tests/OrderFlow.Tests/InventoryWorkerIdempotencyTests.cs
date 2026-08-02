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

public class InventoryWorkerIdempotencyTests
{
    private InventoryDbContext CreateInMemoryDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task Consume_DuplicateEventId_ShouldBeIdempotentAndNotDeductStockTwice()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);

        // Seed stock inicial: 10 unidades de ABC-01
        var initialStock = new Stock("ABC-01", 10);
        await dbContext.Stocks.AddAsync(initialStock);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var sameEventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(sameEventId, orderId, "ABC-01", 2, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        // Act - PRIMERA EJECUCIÓN
        await consumer.Consume(contextMock.Object);

        var stockAfterFirstRun = (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad;
        stockAfterFirstRun.Should().Be(8); // 10 - 2 = 8

        // Act - SEGUNDA EJECUCIÓN (MISMO EventId)
        await consumer.Consume(contextMock.Object);

        // Assert
        var stockAfterSecondRun = (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad;
        stockAfterSecondRun.Should().Be(8); // El stock DEBE permanecer en 8 (NO se descuenta dos veces)

        var processedEventsCount = await dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == sameEventId);
        processedEventsCount.Should().Be(1); // Un solo registro en ProcessedEvents

        contextMock.Verify(x => x.Publish(It.Is<StockReservedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Consume_InsufficientStock_ShouldEmitStockRejectedEventAndNotModifyStock()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);

        var initialStock = new Stock("ABC-02", 5); // 5 disponibles
        await dbContext.Stocks.AddAsync(initialStock);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(eventId, orderId, "ABC-02", 20, DateTime.UtcNow); // Solicita 20

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var stockAfterRun = (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-02")).Disponibilidad;
        stockAfterRun.Should().Be(5); // El stock NO debe cambiar

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == eventId);
        processedEvent.Should().NotBeNull();
        processedEvent!.Resultado.Should().Be("Rejected");

        contextMock.Verify(x => x.Publish(It.Is<StockRejectedEvent>(e => e.OrderId == orderId && e.Motivo.Contains("Stock insuficiente")), It.IsAny<CancellationToken>()), Times.Once);
    }
}
