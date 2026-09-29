using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Common;
using ZenGear.Data.Entities.Identity;

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

    public DbSet<User> Users => Set<User>();

    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZenGearDbContext).Assembly);
        ApplyXminConcurrency(modelBuilder);
        ApplyEnumStoredValues(modelBuilder);
    }

    /// <summary>
    /// Every enum property is stored as the value of its <c>[JsonStringEnumMemberName]</c>
    /// (<see cref="AttributeEnumConverter{TEnum}"/>), the same value the JSON carries.
    /// </summary>
    private static void ApplyEnumStoredValues(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (!enumType.IsEnum || property.GetValueConverter() is not null)
            {
                continue;
            }

            var converterType = typeof(AttributeEnumConverter<>).MakeGenericType(enumType);
            property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
        }
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
