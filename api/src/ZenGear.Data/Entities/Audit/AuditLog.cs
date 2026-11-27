using ZenGear.Data.Entities.Audit.Enums;

namespace ZenGear.Data.Entities.Audit;

/// <summary>
/// One field-level change of a row (<c>audit_logs</c>, R-AUD-01). Mapped for the model, the migration and reads only:
/// the audit interceptor of <c>ZenGear.Services</c> inserts the rows with one statement after the changes are saved,
/// and the table is append-only (only <c>fn_audit_erase_values</c> may erase personal values on anonymization).
/// </summary>
public class AuditLog
{
    protected AuditLog()
    {
    }

    public Guid Id { get; private set; }

    public string TableName { get; private set; } = null!;

    /// <summary>The primary key of the changed row as a JSON object of its columns.</summary>
    public string RecordKey { get; private set; } = null!;

    public AuditAction Action { get; private set; }

    /// <summary>JSON object of the columns before the change (UPDATE: the changed columns only; DELETE: the row).</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON object of the columns after the change (INSERT: the row; UPDATE: the changed columns only).</summary>
    public string? NewValues { get; private set; }

    /// <summary>The database transaction that wrote the row: every row of one operation shares it (DEC-086).</summary>
    public long TransactionId { get; private set; }

    /// <summary>The W3C trace id of the request, the TraceId of the application log (DEC-086).</summary>
    public Guid? CorrelationId { get; private set; }

    public Guid? ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}
