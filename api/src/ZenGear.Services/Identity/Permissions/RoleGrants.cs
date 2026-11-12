namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// The permissions a role gets from code (DEC-095). Its default manageable set is not here: <c>api/seed/01_roles.sql</c>
/// writes it once (R-SEC-12, DEC-129).
/// </summary>
/// <param name="RoleName">The role is found by name: a role listed here must not be renamed or deleted.</param>
/// <param name="IsSystemRole">
/// True (Admin, Customer): the grants are exactly <paramref name="PermissionKeys"/>, rewritten at every sync.
/// False (default roles): only keys new to the database are granted; the Admin edits the rest (AUT-05).
/// </param>
public sealed record RoleGrants(string RoleName, bool IsSystemRole, IReadOnlyList<string> PermissionKeys);
