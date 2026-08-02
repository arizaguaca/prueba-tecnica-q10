namespace OrdersApi.Domain.Entities;

/// <summary>
/// Proyección de solo lectura sobre la tabla Stocks (gestionada por Inventory Worker).
/// </summary>
public class StockCatalogEntry
{
    public string Sku { get; private set; } = string.Empty;

    private StockCatalogEntry() { }
}
