using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Geography;

namespace ZenGear.Data.Configurations.Geography;

/// <summary><c>provinces</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("provinces");

        builder.HasKey(p => p.Id).HasName("pk_provinces");
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasColumnType("varchar");
        builder.Property(p => p.Code).HasMaxLength(20);
        builder.Property(p => p.Type).HasMaxLength(20);
        builder.Property(p => p.ZipCode).HasMaxLength(20);
        builder.Property(p => p.PhoneCode).HasMaxLength(10);
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.SortOrder).HasDefaultValue(0);

        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(p => p.CountryId)
            .HasConstraintName("fk_provinces_countries_country_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.CountryId).HasDatabaseName("ix_provinces_country_id");
        // The target of the composite FK addresses (province_id, country_id) (R-DB-09): a unique index, as in the spec.
        builder.HasIndex(p => new { p.Id, p.CountryId }).IsUnique().HasDatabaseName("ix_provinces_id_country_id");
        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ix_provinces_code");
    }
}
