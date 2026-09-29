using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary>
/// <c>user_roles</c> as in api/db/zengear_02_schema_phase1_store.sql. The triggers of R-SEC-01 and
/// R-SEC-02 are created by the migration (<c>migrationBuilder.Sql</c>).
/// </summary>
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        builder.HasKey(ur => new { ur.UserId, ur.RoleId }).HasName("pk_user_roles");
        builder.Property(ur => ur.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne<User>()
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .HasConstraintName("fk_user_roles_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .HasConstraintName("fk_user_roles_roles_role_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");
    }
}
