using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Audit;

namespace ZenGear.Data.Configurations.Audit;

/// <summary><c>audit_logs</c> as in api/db/zengear_02_schema_phase1_store.sql (R-AUD-01, DEC-086).</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => new { a.Id, a.ChangedAt }).HasName("pk_audit_logs");
        builder.Property(a => a.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(a => a.ChangedAt).HasDefaultValueSql("now()");

        builder.Property(a => a.TableName).HasColumnType("varchar");
        builder.Property(a => a.RecordKey).HasColumnType("jsonb");
        builder.Property(a => a.Action).HasMaxLength(10);
        builder.Property(a => a.OldValues).HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnType("jsonb");
        builder.Property(a => a.TransactionId).HasDefaultValueSql("pg_current_xact_id()::text::bigint");

        builder.HasIndex(a => new { a.TableName, a.ChangedAt }).HasDatabaseName("ix_audit_logs_table_name_changed_at");
        builder.HasIndex(a => new { a.TableName, a.RecordKey }).HasDatabaseName("ix_audit_logs_table_name_record_key");
        builder.HasIndex(a => a.ChangedBy).HasDatabaseName("ix_audit_logs_changed_by");
        builder.HasIndex(a => a.TransactionId).HasDatabaseName("ix_audit_logs_transaction_id");
    }
}
