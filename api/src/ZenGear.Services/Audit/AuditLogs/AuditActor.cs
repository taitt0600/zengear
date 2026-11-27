namespace ZenGear.Services.Audit.AuditLogs;

/// <summary>
/// Who makes the changes of one scope: <c>audit_logs.changed_by</c> (R-AUD-01). The API sets it once the caller is
/// known to be an ACTIVE user; it stays null for work no user started (the permission sync at startup, the
/// <c>bootstrap-admin</c> command) and for a caller with no account yet (a Firebase uid that registers, AUT-02).
/// </summary>
public sealed class AuditActor
{
    public Guid? UserId { get; private set; }

    public void Set(Guid userId) => UserId = userId;
}
