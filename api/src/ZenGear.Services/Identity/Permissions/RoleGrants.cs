namespace ZenGear.Services.Identity.Permissions;

/// <summary>The permissions a role gets from code (DEC-095), and the roles it manages by default (R-SEC-12, DEC-128).</summary>
/// <param name="RoleName">The role is found by name: a role listed here must not be renamed or deleted.</param>
/// <param name="IsSystemRole">
/// True (Admin, Customer): the grants are exactly <paramref name="PermissionKeys"/>, rewritten at every sync.
/// False (default roles): only keys new to the database are granted; the Admin edits the rest (AUT-05).
/// </param>
/// <param name="ManagedRoleNames">
/// The default manageable set (Phạm vi quản lý), inserted by the sync only while <c>role_manageable_roles</c> is empty, so
/// the Admin's edits are kept; never a system role.
/// </param>
public sealed record RoleGrants(
    string RoleName,
    bool IsSystemRole,
    IReadOnlyList<string> PermissionKeys,
    IReadOnlyList<string>? ManagedRoleNames = null)
{
    public IReadOnlyList<string> ManagedRoleNames { get; } = ManagedRoleNames ?? [];
}
