using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>permissions</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id).HasName("pk_permissions");
        builder.Property(p => p.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(p => p.Key).HasMaxLength(100);
        builder.Property(p => p.Description).HasColumnType("text");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(p => p.Key).IsUnique().HasDatabaseName("ix_permissions_key");
    }
}
