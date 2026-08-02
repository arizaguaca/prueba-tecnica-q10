using Microsoft.EntityFrameworkCore;
using OrdersApi.Application.Interfaces;

namespace OrdersApi.Infrastructure.Persistence;

public class ProductCatalogRepository : IProductCatalogRepository
{
    private readonly OrdersDbContext _dbContext;

    public ProductCatalogRepository(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return _dbContext.StockCatalog.AnyAsync(entry => entry.Sku == sku, cancellationToken);
    }
}
