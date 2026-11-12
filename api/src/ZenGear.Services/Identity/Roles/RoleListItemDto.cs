namespace ZenGear.Services.Identity.Roles;

/// <summary>A role in the list of the role screen (AUT-05).</summary>
/// <param name="IsSystemRole">Admin and Customer: permissions from code only, never edited (DEC-095).</param>
/// <param name="IsDefaultRole">One of the 8 default staff roles: never renamed or deleted (DEC-095, DEC-128).</param>
/// <param name="MaxDiscountPercent">Highest manual discount at the counter; null = none (R-PRM-04).</param>
public sealed record RoleListItemDto(Guid Id, string Name, bool IsSystemRole, bool IsDefaultRole, decimal? MaxDiscountPercent);
