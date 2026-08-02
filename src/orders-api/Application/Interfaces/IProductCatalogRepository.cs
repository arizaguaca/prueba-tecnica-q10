namespace OrdersApi.Application.Interfaces;

public interface IProductCatalogRepository
{
    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
