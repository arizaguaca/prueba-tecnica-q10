using OrdersApi.Domain.Enums;

namespace OrdersApi.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public string ClienteNombre { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public int Cantidad { get; private set; }
    public OrderStatus Estado { get; private set; }
    public DateTime CreadoEn { get; private set; }

    // Required for EF Core
    private Order() { }

    public Order(string clienteNombre, string sku, int cantidad)
    {
        Id = Guid.NewGuid();
        ClienteNombre = clienteNombre;
        Sku = sku;
        Cantidad = cantidad;
        Estado = OrderStatus.Pending;
        CreadoEn = DateTime.UtcNow;
    }

    public void ConfirmOrder()
    {
        if (Estado == OrderStatus.Pending)
        {
            Estado = OrderStatus.Confirmed;
        }
    }

    public void RejectOrder()
    {
        if (Estado == OrderStatus.Pending)
        {
            Estado = OrderStatus.Rejected;
        }
    }
}
