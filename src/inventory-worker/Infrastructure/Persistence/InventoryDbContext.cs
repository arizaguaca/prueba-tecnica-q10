using Microsoft.EntityFrameworkCore;
using InventoryWorker.Domain.Entities;

namespace InventoryWorker.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stocks");
            entity.HasKey(e => e.Sku);
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.Disponibilidad).IsRequired();
            entity.Property(e => e.ActualizadoEn).IsRequired();
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("ProcessedEvents");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.TipoEvento).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Resultado).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MotivoRechazo).HasMaxLength(500);
            entity.Property(e => e.ProcesadoEn).IsRequired();
        });
    }
}
