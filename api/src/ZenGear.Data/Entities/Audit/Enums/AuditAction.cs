using System.Text.Json.Serialization;

namespace ZenGear.Data.Entities.Audit.Enums;

/// <summary><c>audit_logs.action</c>: the kind of change a row records (R-AUD-01).</summary>
public enum AuditAction
{
    [JsonStringEnumMemberName("INSERT")]
    Insert,

    [JsonStringEnumMemberName("UPDATE")]
    Update,

    [JsonStringEnumMemberName("DELETE")]
    Delete,
}
