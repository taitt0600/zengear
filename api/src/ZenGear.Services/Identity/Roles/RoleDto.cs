using System.Text.Json.Serialization;

namespace ZenGear.Services.Identity.Roles;

/// <summary>A role as the role screen edits it (AUT-05, UC-003).</summary>
/// <param name="IsSystemRole">Admin and Customer: only <paramref name="MaxDiscountPercent"/> may change (DEC-095).</param>
/// <param name="IsDefaultRole">One of the 8 default staff roles: never renamed or deleted (DEC-095, DEC-128).</param>
/// <param name="MaxDiscountPercent">Highest manual discount at the counter; null = none (R-PRM-04).</param>
/// <param name="PermissionKeys">The keys the role grants, sorted.</param>
/// <param name="ManagedRoles">The manageable set (Phạm vi quản lý, R-SEC-12), by name.</param>
/// <param name="Version"><c>roles.xmin</c>, sent as the <c>ETag</c> header and never in the body.</param>
public sealed record RoleDto(
    Guid Id,
    string Name,
    bool IsSystemRole,
    bool IsDefaultRole,
    decimal? MaxDiscountPercent,
    IReadOnlyList<string> PermissionKeys,
    IReadOnlyList<RoleManagedRoleDto> ManagedRoles,
    [property: JsonIgnore] uint Version);
