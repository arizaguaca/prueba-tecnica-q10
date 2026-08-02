namespace OrdersApi.Application.DTOs;

public record CreateOrderRequest(string ClienteNombre, string Sku, int Cantidad);
