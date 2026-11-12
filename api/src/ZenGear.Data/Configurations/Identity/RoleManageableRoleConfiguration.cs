using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>role_manageable_roles</c> as in api/db/zengear_02_schema_phase1_store.sql (R-SEC-12, DEC-128).</summary>
internal sealed class RoleManageableRoleConfiguration : IEntityTypeConfiguration<RoleManageableRole>
{
    public void Configure(EntityTypeBuilder<RoleManageableRole> builder)
    {
        builder.ToTable("role_manageable_roles");

        builder.HasKey(m => new { m.RoleId, m.ManagedRoleId }).HasName("pk_role_manageable_roles");
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(m => m.ManagedRoleId)
            .HasConstraintName("fk_role_manageable_roles_roles_managed_role_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ManagedRoleId).HasDatabaseName("ix_role_manageable_roles_managed_role_id");
    }
}
