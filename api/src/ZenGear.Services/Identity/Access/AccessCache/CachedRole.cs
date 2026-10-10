using ZenGear.Data.Entities.Identity.Enums;




namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>A role as cached: its scope and permission keys.</summary>
public sealed record CachedRole(ScopingType ScopingType, string[] PermissionKeys);
