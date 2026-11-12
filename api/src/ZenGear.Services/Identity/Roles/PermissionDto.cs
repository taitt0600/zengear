namespace ZenGear.Services.Identity.Roles;

/// <summary>A permission the Admin may grant to a role (DEC-095): its key and its label.</summary>
public sealed record PermissionDto(string Key, string Description);
