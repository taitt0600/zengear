using ZenGear.Data.Entities.Identity;

namespace ZenGear.Services.Identity;

/// <summary>Versions under which entries are read and written: the global epoch and the entry's own version.</summary>
public readonly record struct AccessVersion(long Epoch, long Version);

/// <summary>A user as cached: everything <see cref="UserAccess"/> needs except the permissions.</summary>
public sealed record CachedUser(
    string? FirebaseUid, UserStatus Status, DateTimeOffset? LastLoginAt, bool MustChangePassword, Guid[] RoleIds);

/// <summary>A role as cached: its scope and permission keys.</summary>
public sealed record CachedRole(ScopingType ScopingType, string[] PermissionKeys);

/// <summary>
/// Redis cache of user status and permissions per role (R-SEC-07). Entries are stored under versions:
/// invalidating means incrementing a version (<see cref="BumpAsync"/>), so an entry built from data read
/// before a change, and written after it, lands under an old version that is never read again.
/// <para>
/// EF Core changes are bumped by <see cref="AccessCacheInvalidationInterceptor"/> after commit.
/// <c>ExecuteUpdate</c>, <c>ExecuteDelete</c>, raw SQL and FK cascades bypass it: their caller must call
/// <see cref="BumpAsync"/> after its transaction commits.
/// </para>
/// Read methods throw when Redis fails; <see cref="BumpAsync"/> never throws.
/// </summary>
public interface IAccessCache
{
    /// <summary>False while Redis is not connected or a bump is pending: read PostgreSQL only.</summary>
    bool IsAvailable { get; }

    Task<Guid?> GetUserIdAsync(string firebaseUid);

    Task SetUserIdAsync(string firebaseUid, Guid userId);

    Task RemoveUserIdAsync(string firebaseUid);

    Task<AccessVersion> GetUserVersionAsync(Guid userId);

    Task<CachedUser?> GetUserAsync(Guid userId, AccessVersion version);

    Task SetUserAsync(Guid userId, AccessVersion version, CachedUser user);

    Task<AccessVersion[]> GetRoleVersionsAsync(IReadOnlyList<Guid> roleIds);

    Task<CachedRole?[]> GetRolesAsync(IReadOnlyList<Guid> roleIds, IReadOnlyList<AccessVersion> versions);

    Task SetRolesAsync(IReadOnlyList<(Guid RoleId, AccessVersion Version, CachedRole Role)> roles);

    /// <summary>
    /// Increments the versions. A failure is logged as an error and the bumps are kept in
    /// <see cref="PendingAccessBumps"/> to be retried; until then this instance reads PostgreSQL only.
    /// </summary>
    Task BumpAsync(IReadOnlyCollection<AccessBump> bumps);
}
