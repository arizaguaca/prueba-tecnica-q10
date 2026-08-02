namespace InventoryWorker.Domain.Entities;

public class Stock
{
    public string Sku { get; private set; } = string.Empty;
    public int Disponibilidad { get; private set; }
    public DateTime ActualizadoEn { get; private set; }

    // Required for EF Core
    private Stock() { }

    public Stock(string sku, int disponibilidad)
    {
        Sku = sku;
        Disponibilidad = disponibilidad;
        ActualizadoEn = DateTime.UtcNow;
    }

    public bool HasSufficientStock(int cantidad)
    {
        return Disponibilidad >= cantidad;
    }

    public void DeductStock(int cantidad)
    {
        if (!HasSufficientStock(cantidad))
        {
            throw new InvalidOperationException("Stock insuficiente para realizar el descuento.");
        }
        Disponibilidad -= cantidad;
        ActualizadoEn = DateTime.UtcNow;
    }
}
