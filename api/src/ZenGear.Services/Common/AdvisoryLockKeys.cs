namespace ZenGear.Services.Common;

/// <summary>
/// Keys of the PostgreSQL advisory locks taken by jobs (ARCHITECTURE.md, Background jobs) and by the bootstrap-admin
/// command. Every key is declared here once, so no two of them share one.
/// </summary>
public static class AdvisoryLockKeys
{
    /// <summary><c>pg_advisory_xact_lock</c> of the startup permission sync (DEC-095). "ZGPERM".</summary>
    public const long PermissionSync = 0x5A47_5045_524D;

    /// <summary><c>pg_advisory_xact_lock</c> of the bootstrap-admin command (DEC-096). "ZGBOOT".</summary>
    public const long AdminBootstrap = 0x5A47_424F_4F54;
}
