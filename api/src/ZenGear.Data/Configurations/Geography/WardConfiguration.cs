using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Geography;

namespace ZenGear.Data.Configurations.Geography;

/// <summary><c>wards</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("wards");

        builder.HasKey(w => w.Id).HasName("pk_wards");
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Name).HasColumnType("varchar");
        builder.Property(w => w.Code).HasMaxLength(20);
        builder.Property(w => w.Type).HasMaxLength(20);
        builder.Property(w => w.ZipCode).HasMaxLength(20);
        builder.Property(w => w.PhoneCode).HasMaxLength(10);
        builder.Property(w => w.IsActive).HasDefaultValue(true);
        builder.Property(w => w.SortOrder).HasDefaultValue(0);

        builder.HasOne<Province>()
            .WithMany()
            .HasForeignKey(w => w.ProvinceId)
            .HasConstraintName("fk_wards_provinces_province_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.ProvinceId).HasDatabaseName("ix_wards_province_id");
        // The target of the composite FKs (ward_id, province_id) of business_locations and addresses (R-DB-09): a unique
        // index, not a key, so province_id stays nullable. Those FKs are created with SQL in their migrations, because an
        // EF Core relationship needs a key (Phase1_Store_Geography_BusinessLocations).
        builder.HasIndex(w => new { w.Id, w.ProvinceId }).IsUnique().HasDatabaseName("ix_wards_id_province_id");
        builder.HasIndex(w => w.Code).IsUnique().HasDatabaseName("ix_wards_code");
    }
}
