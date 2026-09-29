using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>role_permissions</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId }).HasName("pk_role_permissions");
        builder.Property(rp => rp.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .HasConstraintName("fk_role_permissions_permissions_permission_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rp => rp.PermissionId).HasDatabaseName("ix_role_permissions_permission_id");
    }
}
