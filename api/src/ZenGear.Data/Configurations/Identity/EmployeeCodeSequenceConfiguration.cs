using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Data.Configurations.Identity;

/// <summary><c>employee_code_sequences</c> as in api/db/zengear_02_schema_phase1_store.sql (R-USR-24, DEC-145).</summary>
internal sealed class EmployeeCodeSequenceConfiguration : IEntityTypeConfiguration<EmployeeCodeSequence>
{
    public void Configure(EntityTypeBuilder<EmployeeCodeSequence> builder)
    {
        builder.ToTable("employee_code_sequences", t => t.HasCheckConstraint(
            "ck_employee_code_sequences_last_value", "last_value between 1 and 9999"));

        builder.HasKey(s => s.Year).HasName("pk_employee_code_sequences");
        builder.Property(s => s.Year).ValueGeneratedNever();
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");
    }
}
