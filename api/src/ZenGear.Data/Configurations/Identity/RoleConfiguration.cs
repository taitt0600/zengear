using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>roles</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", t => t.HasCheckConstraint(
            "ck_roles_max_discount_percent",
            "max_discount_percent is null or (max_discount_percent >= 0 and max_discount_percent <= 100)"));

        builder.HasKey(r => r.Id).HasName("pk_roles");
        builder.Property(r => r.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(r => r.Name).HasColumnType("varchar");
        builder.Property(r => r.ScopingType).HasMaxLength(10);
        builder.Property(r => r.IsSystemRole).HasDefaultValue(false);
        builder.Property(r => r.MaxDiscountPercent).HasPrecision(5, 2);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(r => r.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("ix_roles_name");

        builder.HasMany(r => r.RolePermissions)
            .WithOne()
            .HasForeignKey(rp => rp.RoleId)
            .HasConstraintName("fk_role_permissions_roles_role_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.RolePermissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
