using System.Diagnostics;

namespace ZenGear.Data.Common;

/// <summary>
/// Correlation id of the current operation: the W3C trace id of <see cref="Activity.Current"/>
/// as a <see cref="Guid"/>, the same value as <c>TraceId</c> in the application log (DEC-086).
/// Stored in <c>audit_logs.correlation_id</c> and <c>status_history.correlation_id</c>.
/// </summary>
public static class CorrelationId
{
    public static Guid? Current =>
        Activity.Current is { } activity
            ? Guid.ParseExact(activity.TraceId.ToHexString(), "N")
            : null;
}
