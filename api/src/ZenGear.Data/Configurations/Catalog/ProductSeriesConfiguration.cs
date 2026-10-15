using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Catalog;

namespace ZenGear.Data.Configurations.Catalog;

/// <summary><c>product_series</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class ProductSeriesConfiguration : IEntityTypeConfiguration<ProductSeries>
{
    public void Configure(EntityTypeBuilder<ProductSeries> builder)
    {
        builder.ToTable("product_series");

        builder.HasKey(s => s.Id).HasName("pk_product_series");
        builder.Property(s => s.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(s => s.Name).HasColumnType("varchar");
        builder.Property(s => s.Slug).HasColumnType("varchar");
        // True as sentinel, as for brands: a retired series is never inserted as active by the database default.
        builder.Property(s => s.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

        // A brand that has series is retired, never deleted (R-DEL-01).
        builder.HasOne<Brand>().WithMany().HasForeignKey(s => s.BrandId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_product_series_brands_brand_id");

        builder.HasIndex(s => s.Slug).IsUnique().HasDatabaseName("ix_product_series_slug");
        builder.HasIndex(s => s.BrandId).HasDatabaseName("ix_product_series_brand_id");
        // The target of the products FK on (series_id, brand_id), which keeps a product in its series' brand (R-PRD-07).
        builder.HasIndex(s => new { s.Id, s.BrandId }).IsUnique().HasDatabaseName("ix_product_series_id_brand_id");
    }
}
