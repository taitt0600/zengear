using Microsoft.EntityFrameworkCore;
using ZenGear.Data.Entities.Common;

namespace ZenGear.Data;

public class ZenGearDbContext : DbContext
{
    public ZenGearDbContext(DbContextOptions<ZenGearDbContext> options) : base(options)
    {
    }

    /// <summary>For a derived context (tests) with its own options type.</summary>
    protected ZenGearDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZenGearDbContext).Assembly);
        ApplyXminConcurrency(modelBuilder);
    }

    /// <summary>
    /// Every aggregate root uses <c>xmin</c> as its concurrency token; the column name is set
    /// explicitly so the snake_case convention does not rename it to <c>version</c>.
    /// </summary>
    private static void ApplyXminConcurrency(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IAggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IAggregateRoot.Version))
                .IsRowVersion()
                .HasColumnName("xmin")
                .HasColumnType("xid");
        }
    }
}
