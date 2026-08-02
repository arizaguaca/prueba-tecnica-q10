using Microsoft.EntityFrameworkCore;
using OrdersApi.Domain.Entities;

namespace OrdersApi.Infrastructure.Persistence;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<StockCatalogEntry> StockCatalog => Set<StockCatalogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClienteNombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Sku).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Cantidad).IsRequired();
            entity.Property(e => e.Estado).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreadoEn).IsRequired();
        });

        modelBuilder.Entity<StockCatalogEntry>(entity =>
        {
            entity.ToTable("Stocks");
            entity.HasKey(e => e.Sku);
            entity.Property(e => e.Sku).HasColumnName("Sku");
        });
    }
}
