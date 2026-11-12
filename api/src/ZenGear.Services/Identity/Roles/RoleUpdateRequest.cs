namespace ZenGear.Services.Identity.Roles;

/// <summary>
/// The role as the Admin edits it (UC-003): every field is sent. A system role (Admin, Customer) changes only its discount;
/// a default role keeps its name (DEC-095).
/// </summary>
/// <param name="Name">Stored trimmed; unique among the roles whatever the case.</param>
/// <param name="PermissionKeys">The whole set of keys the role is to have.</param>
/// <param name="MaxDiscountPercent">Highest manual discount at the counter, 0–100 with two decimals; null = none (R-PRM-04).</param>
/// <param name="ManagedRoleIds">The whole manageable set (R-SEC-12); never Admin or Customer; empty for a system role.</param>
public sealed record RoleUpdateRequest(
    string? Name,
    IReadOnlyList<string>? PermissionKeys,
    decimal? MaxDiscountPercent,
    IReadOnlyList<Guid>? ManagedRoleIds);
