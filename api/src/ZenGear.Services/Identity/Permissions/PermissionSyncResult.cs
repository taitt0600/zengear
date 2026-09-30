namespace ZenGear.Services.Identity.Permissions;

public enum PermissionSyncResult
{
    Synchronized,

    /// <summary>A role of the catalog does not exist yet: nothing was written.</summary>
    RolesMissing,
}
