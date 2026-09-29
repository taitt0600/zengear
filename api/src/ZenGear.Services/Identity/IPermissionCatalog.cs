namespace ZenGear.Services.Identity;

/// <summary>A permission defined in code: key <c>resource.action</c> and its label.</summary>
public sealed record PermissionDefinition(string Key, string Description);

/// <summary>The permissions a role gets from code (DEC-095).</summary>
/// <param name="RoleName">The role is found by name: a role listed here must not be renamed or deleted.</param>
/// <param name="IsSystemRole">
/// True (Admin, Customer): the grants are exactly <paramref name="PermissionKeys"/>, rewritten at every sync.
/// False (default roles): only keys new to the database are granted; the Admin edits the rest (AUT-05).
/// </param>
public sealed record RoleGrants(string RoleName, bool IsSystemRole, IReadOnlyList<string> PermissionKeys);

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
