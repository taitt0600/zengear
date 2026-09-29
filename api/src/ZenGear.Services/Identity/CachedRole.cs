using ZenGear.Data.Entities.Identity;

namespace ZenGear.Services.Identity;

/// <summary>A role as cached: its scope and permission keys.</summary>
public sealed record CachedRole(ScopingType ScopingType, string[] PermissionKeys);
