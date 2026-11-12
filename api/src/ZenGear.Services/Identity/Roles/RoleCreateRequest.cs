namespace ZenGear.Services.Identity.Roles;

/// <summary>
/// A new staff role (UC-002): scope NONE, not a system role, managing no role until the Admin grants it (R-SEC-12). The
/// name and the list are nullable so the validator, not model binding, answers a missing one.
/// </summary>
/// <param name="Name">Stored trimmed; unique among the roles whatever the case.</param>
/// <param name="PermissionKeys">Keys of <c>GET /api/v1/admin/permissions</c>; a key given twice counts once.</param>
/// <param name="MaxDiscountPercent">Highest manual discount at the counter, 0–100 with two decimals; null = none (R-PRM-04).</param>
public sealed record RoleCreateRequest(string? Name, IReadOnlyList<string>? PermissionKeys, decimal? MaxDiscountPercent);
