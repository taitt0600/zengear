using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Geography;
using ZenGear.Data.Entities.Locations;

namespace ZenGear.Data.Configurations.Locations;

/// <summary>
/// <c>business_locations</c> as in api/db/zengear_02_schema_phase1_store.sql. The composite FK
/// <c>fk_business_locations_wards_ward_id_province_id</c> (R-DB-09) is not in the model: its target is the unique index
/// <c>ix_wards_id_province_id</c>, and an EF Core relationship needs a key, which would add a constraint and make
/// <c>wards.province_id</c> NOT NULL. The migration creates it with SQL.
/// </summary>
internal sealed class BusinessLocationConfiguration : IEntityTypeConfiguration<BusinessLocation>
{
    public void Configure(EntityTypeBuilder<BusinessLocation> builder)
    {
        builder.ToTable("business_locations", t =>
        {
            t.HasCheckConstraint("ck_business_locations_regular_hours", "jsonb_typeof(regular_hours) = 'array'");
            t.HasCheckConstraint("ck_business_locations_special_hours",
                "special_hours is null or jsonb_typeof(special_hours) = 'array'");
        });

        builder.HasKey(b => b.Id).HasName("pk_business_locations");
        builder.Property(b => b.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(b => b.Name).HasColumnType("varchar");
        builder.Property(b => b.Phone).HasMaxLength(20);
        builder.Property(b => b.Email).HasColumnType("varchar");
        builder.Property(b => b.AddressDetail).HasColumnType("text");

        var regular = new JsonListConverter<RegularHoursPeriod>();
        builder.Property(b => b.RegularHours).HasConversion(regular, regular.Comparer).HasColumnType("jsonb");
        var special = new JsonListConverter<SpecialHoursPeriod>();
        builder.Property(b => b.SpecialHours!).HasConversion(special, special.Comparer).HasColumnType("jsonb");

        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne<Province>()
            .WithMany()
            .HasForeignKey(b => b.ProvinceId)
            .HasConstraintName("fk_business_locations_provinces_province_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.Name).IsUnique().HasDatabaseName("ix_business_locations_name");
        builder.HasIndex(b => b.ProvinceId).HasDatabaseName("ix_business_locations_province_id");
        builder.HasIndex(b => b.WardId).HasDatabaseName("ix_business_locations_ward_id");
    }
}
