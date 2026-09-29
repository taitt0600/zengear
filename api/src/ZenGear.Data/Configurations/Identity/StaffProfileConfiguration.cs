using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>staff_profiles</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("staff_profiles", t => t.HasCheckConstraint(
            "ck_staff_profiles_left_date", "left_date is null or joined_date is null or left_date >= joined_date"));

        builder.HasKey(s => s.UserId).HasName("pk_staff_profiles");
        builder.Property(s => s.UserId).ValueGeneratedNever();

        builder.Property(s => s.EmployeeCode).HasColumnType("varchar");
        builder.Property(s => s.WorkPhone).HasMaxLength(20);
        builder.Property(s => s.MustChangePassword).HasDefaultValue(false);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(s => s.EmployeeCode).IsUnique().HasDatabaseName("ix_staff_profiles_employee_code");
    }
}
