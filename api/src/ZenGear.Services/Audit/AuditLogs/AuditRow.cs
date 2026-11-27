using ZenGear.Data.Entities.Audit.Enums;

namespace ZenGear.Services.Audit.AuditLogs;

/// <summary>
/// The columns of one <c>audit_logs</c> row that come from the change itself; the transaction, the time, the actor and
/// the correlation id are added when it is written. The JSON objects are keyed by column name and hold the stored values.
/// </summary>
public sealed record AuditRow(string TableName, string RecordKey, AuditAction Action, string? OldValues, string? NewValues);
