using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Catalog;

namespace ZenGear.Data.Configurations.Catalog;

/// <summary><c>brands</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");

        builder.HasKey(b => b.Id).HasName("pk_brands");
        builder.Property(b => b.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(b => b.Name).HasColumnType("varchar");
        builder.Property(b => b.Slug).HasColumnType("varchar");
        builder.Property(b => b.LogoUrl).HasColumnType("text");
        // With the CLR default false as sentinel, EF would leave a retired brand out of the INSERT and the database
        // default would store it active; true as sentinel sends false explicitly.
        builder.Property(b => b.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(b => b.Slug).IsUnique().HasDatabaseName("ix_brands_slug");
    }
}
