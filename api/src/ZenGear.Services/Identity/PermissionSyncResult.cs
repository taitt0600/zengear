namespace ZenGear.Services.Identity;

public enum PermissionSyncResult
{
    Synchronized,

    /// <summary>A role of the catalog does not exist yet: nothing was written.</summary>
    RolesMissing,
}
