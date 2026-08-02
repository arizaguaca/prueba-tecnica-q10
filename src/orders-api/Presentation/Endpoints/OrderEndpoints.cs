using FluentValidation;
using OrdersApi.Application.DTOs;
using OrdersApi.Application.Contracts.Events;
using OrdersApi.Application.Interfaces;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Presentation.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", async (
            CreateOrderRequest request, 
            IValidator<CreateOrderRequest> validator, 
            IOrderRepository repository, 
            IEventPublisher publisher) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var order = new Order(request.ClienteNombre, request.Sku, request.Cantidad);
            await repository.AddAsync(order);

            var @event = new OrderCreatedEvent(
                EventId: Guid.NewGuid(),
                OrderId: order.Id,
                Sku: order.Sku,
                Cantidad: order.Cantidad,
                OcurridoEn: order.CreadoEn
            );

            await publisher.PublishAsync(@event);

            var response = new OrderResponse(
                order.Id,
                order.ClienteNombre,
                order.Sku,
                order.Cantidad,
                order.Estado,
                order.CreadoEn
            );

            return Results.Created($"/orders/{order.Id}", response);
        })
        .WithName("CreateOrder")
        .WithOpenApi();

        group.MapGet("/", async (IOrderRepository repository) =>
        {
            var orders = await repository.GetAllAsync();
            var response = orders.Select(order => new OrderResponse(
                order.Id,
                order.ClienteNombre,
                order.Sku,
                order.Cantidad,
                order.Estado,
                order.CreadoEn
            ));
            return Results.Ok(response);
        })
        .WithName("GetOrders")
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id, IOrderRepository repository) =>
        {
            var order = await repository.GetByIdAsync(id);
            if (order == null)
            {
                return Results.NotFound();
            }

            var response = new OrderResponse(
                order.Id,
                order.ClienteNombre,
                order.Sku,
                order.Cantidad,
                order.Estado,
                order.CreadoEn
            );
            return Results.Ok(response);
        })
        .WithName("GetOrderById")
        .WithOpenApi();
    }
}
