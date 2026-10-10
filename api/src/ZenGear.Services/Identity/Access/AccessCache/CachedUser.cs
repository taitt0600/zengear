using ZenGear.Data.Entities.Identity.Enums;




namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>A user as cached: everything <see cref="UserAccess"/> needs except the permissions.</summary>
public sealed record CachedUser(
    string? FirebaseUid, UserStatus Status, DateTimeOffset? LastLoginAt, bool MustChangePassword, Guid[] RoleIds);
