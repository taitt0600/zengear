using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>staff_profiles</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("staff_profiles", t =>
        {
            t.HasCheckConstraint(
                "ck_staff_profiles_left_date", "left_date is null or joined_date is null or left_date >= joined_date");
            t.HasCheckConstraint("ck_staff_profiles_employee_code", "employee_code ~ '^ZG[0-9]{6}$'");
        });

        builder.HasKey(s => s.UserId).HasName("pk_staff_profiles");
        builder.Property(s => s.UserId).ValueGeneratedNever();

        builder.Property(s => s.EmployeeCode).HasMaxLength(8);
        builder.Property(s => s.Username).HasMaxLength(64);
        builder.Property(s => s.WorkPhone).HasMaxLength(20);
        builder.Property(s => s.MustChangePassword).HasDefaultValue(false);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(s => s.EmployeeCode).IsUnique().HasDatabaseName("ix_staff_profiles_employee_code");

        // The creator of the account (R-USR-17, DEC-128 (3)): a business reference, so a foreign key unlike the audit
        // columns. Only a PENDING account is ever deleted, and it never created one (it cannot sign in).
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.CreatedBy)
            .HasConstraintName("fk_staff_profiles_users_created_by").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.CreatedBy).HasDatabaseName("ix_staff_profiles_created_by");

        // ix_staff_profiles_username is unique on lower(username) (R-USR-24): EF Core 10 maps no index on an expression,
        // so the migration Phase1_Store_Identity_EmployeeCodeSequences creates it with SQL and the model leaves it out.
    }
}
