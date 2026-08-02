using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InventoryWorker.Application.Consumers;
using InventoryWorker.Domain.Entities;
using InventoryWorker.Infrastructure.Persistence;
using OrdersApi.Application.Contracts.Events;
using Xunit;

namespace InventoryWorker.Tests;

public class OrderCreatedConsumerTests
{
    private InventoryDbContext CreateInMemoryDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        return new InventoryDbContext(options);
    }

    [Fact]
    public async Task Consume_FirstTime_DeductsStockAndSavesProcessedEvent()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);

        // Seed stock
        var initialStock = new Stock("ABC-01", 10);
        await dbContext.Stocks.AddAsync(initialStock);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(eventId, orderId, "ABC-01", 2, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var updatedStock = await dbContext.Stocks.FirstOrDefaultAsync(s => s.Sku == "ABC-01");
        Assert.NotNull(updatedStock);
        Assert.Equal(8, updatedStock.Disponibilidad); // 10 - 2 = 8

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == eventId);
        Assert.NotNull(processedEvent);
        Assert.Equal("Reserved", processedEvent.Resultado);

        contextMock.Verify(x => x.Publish(It.Is<StockReservedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_DuplicateEventId_DoesNotDeductStockTwice()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);

        // Seed stock: 10 units
        var initialStock = new Stock("ABC-01", 10);
        await dbContext.Stocks.AddAsync(initialStock);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var sameEventId = Guid.NewGuid(); // Mismo EventId para ambas ejecuciones
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(sameEventId, orderId, "ABC-01", 2, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        // --- PRIMERA EJECUCIÓN ---
        await consumer.Consume(contextMock.Object);

        var stockAfterFirstRun = (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad;
        Assert.Equal(8, stockAfterFirstRun); // Reducido a 8

        // --- SEGUNDA EJECUCIÓN (DUPLICADO CON EL MISMO EventId) ---
        await consumer.Consume(contextMock.Object);

        // Assert: El stock DEBE permanecer en 8 (NO debe reducirse a 6)
        var stockAfterSecondRun = (await dbContext.Stocks.FirstAsync(s => s.Sku == "ABC-01")).Disponibilidad;
        Assert.Equal(8, stockAfterSecondRun);

        // Assert: Solo debe haber 1 registro en ProcessedEvents
        var processedEventsCount = await dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == sameEventId);
        Assert.Equal(1, processedEventsCount);

        // Assert: Se debió re-publicar el evento de respuesta para garantizar la consistencia eventual (2 veces en total)
        contextMock.Verify(x => x.Publish(It.Is<StockReservedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Consume_InsufficientStock_RejectsOrderAndIsIdempotentOnDuplicate()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);

        var initialStock = new Stock("ABC-03", 0); // 0 disponibles
        await dbContext.Stocks.AddAsync(initialStock);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();
        var consumer = new OrderCreatedConsumer(dbContext, loggerMock.Object);

        var sameEventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderEvent = new OrderCreatedEvent(sameEventId, orderId, "ABC-03", 1, DateTime.UtcNow);

        var contextMock = new Mock<ConsumeContext<OrderCreatedEvent>>();
        contextMock.Setup(x => x.Message).Returns(orderEvent);

        // --- PRIMERA EJECUCIÓN ---
        await consumer.Consume(contextMock.Object);

        var processedEvent = await dbContext.ProcessedEvents.FirstOrDefaultAsync(pe => pe.EventId == sameEventId);
        Assert.NotNull(processedEvent);
        Assert.Equal("Rejected", processedEvent.Resultado);
        contextMock.Verify(x => x.Publish(It.Is<StockRejectedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Once);

        // --- SEGUNDA EJECUCIÓN (DUPLICADO) ---
        await consumer.Consume(contextMock.Object);

        // Assert: Sigue siendo 1 solo registro guardado
        var count = await dbContext.ProcessedEvents.CountAsync(pe => pe.EventId == sameEventId);
        Assert.Equal(1, count);

        // Assert: Publicado 2 veces (re-publicado por idempotencia)
        contextMock.Verify(x => x.Publish(It.Is<StockRejectedEvent>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
