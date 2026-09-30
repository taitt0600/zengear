namespace ZenGear.Services.Common;

/// <summary>
/// Keys of the PostgreSQL advisory locks taken by jobs (ARCHITECTURE.md, Background jobs). Every key is
/// declared here once, so no two jobs share one.
/// </summary>
public static class AdvisoryLockKeys
{
    /// <summary><c>pg_advisory_xact_lock</c> of the startup permission sync (DEC-095). "ZGPERM".</summary>
    public const long PermissionSync = 0x5A47_5045_524D;
}
