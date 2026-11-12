namespace ZenGear.Services.Identity.Roles;

/// <summary>A role in the manageable set of another role (R-SEC-12).</summary>
public sealed record RoleManagedRoleDto(Guid Id, string Name);
