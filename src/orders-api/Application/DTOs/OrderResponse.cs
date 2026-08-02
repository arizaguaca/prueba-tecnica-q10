using OrdersApi.Domain.Enums;

namespace OrdersApi.Application.DTOs;

public record OrderResponse(
    Guid Id,
    string ClienteNombre,
    string Sku,
    int Cantidad,
    OrderStatus Estado,
    DateTime CreadoEn
);
