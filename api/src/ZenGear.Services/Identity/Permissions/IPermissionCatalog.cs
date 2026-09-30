namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// Permissions and role grants defined in code, applied by <see cref="PermissionSyncService"/> at startup.
/// <para>
/// A default role receives a key only in the sync that first inserts that key. Adding an <b>existing</b>
/// key to a default role's list has no effect on existing databases; that change needs a data migration.
/// </para>
/// </summary>
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionDefinition> Permissions { get; }

    IReadOnlyList<RoleGrants> Roles { get; }
}
