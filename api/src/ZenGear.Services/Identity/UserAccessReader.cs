using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Services.Identity.AccessCache;
using ZenGear.Services.Identity.Models;

namespace ZenGear.Services.Identity;

/// <summary>
/// Reads the caller through the access cache (R-SEC-07), PostgreSQL on a miss. Every entry is written
/// under the version read <b>before</b> its query, so data read before a concurrent change commits can
/// only land under a version that the change has already made obsolete. When Redis is unavailable or a
/// bump is pending, PostgreSQL is read and nothing is written.
/// </summary>
public sealed class UserAccessReader(ZenGearDbContext db, IAccessCache cache, ILogger<UserAccessReader> logger)
    : IUserAccessReader
{
    public async Task<UserAccess?> GetAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        if (!cache.IsAvailable)
        {
            return await ReadDatabaseAsync(firebaseUid, cancellationToken);
        }

        try
        {
            return await ReadThroughCacheAsync(firebaseUid, retryStaleMapping: true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Access cache read failed; reading PostgreSQL");
            return await ReadDatabaseAsync(firebaseUid, cancellationToken);
        }
    }

    private async Task<UserAccess?> ReadThroughCacheAsync(string firebaseUid, bool retryStaleMapping,
        CancellationToken cancellationToken)
    {
        // 1. uid → id: holds only the id, never state.
        var userId = await cache.GetUserIdAsync(firebaseUid);
        if (userId is null)
        {
            userId = await db.Users.AsNoTracking()
                .Where(u => u.FirebaseUid == firebaseUid)
                .Select(u => (Guid?)u.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (userId is null)
            {
                return null;
            }

            await cache.SetUserIdAsync(firebaseUid, userId.Value);
        }

        // 2–3. The user, under the version read before the query.
        var userVersion = await cache.GetUserVersionAsync(userId.Value);
        var user = await cache.GetUserAsync(userId.Value, userVersion);
        if (user is null)
        {
            user = await LoadUserAsync(userId.Value, cancellationToken);
            if (user is not null)
            {
                await cache.SetUserAsync(userId.Value, userVersion, user);
            }
        }

        // 4. A stale mapping (the uid moved or the user is gone) is never trusted.
        if (user is null || user.FirebaseUid != firebaseUid)
        {
            await cache.RemoveUserIdAsync(firebaseUid);
            return retryStaleMapping
                ? await ReadThroughCacheAsync(firebaseUid, retryStaleMapping: false, cancellationToken)
                : await ReadDatabaseAsync(firebaseUid, cancellationToken);
        }

        // 5–6. The roles, missing ones in one query, each under the version read before it.
        var roleIds = user.RoleIds;
        var roleVersions = await cache.GetRoleVersionsAsync(roleIds);
        var roles = await cache.GetRolesAsync(roleIds, roleVersions);
        var missing = roleIds.Where((_, i) => roles[i] is null).ToList();
        if (missing.Count > 0)
        {
            var loaded = await LoadRolesAsync(missing, cancellationToken);
            var toCache = new List<(Guid, AccessVersion, CachedRole)>();
            for (var i = 0; i < roleIds.Length; i++)
            {
                if (roles[i] is null && loaded.TryGetValue(roleIds[i], out var role))
                {
                    roles[i] = role;
                    toCache.Add((roleIds[i], roleVersions[i], role));
                }
            }

            await cache.SetRolesAsync(toCache);
        }

        return ToAccess(userId.Value, user, roles.OfType<CachedRole>());
    }

    private async Task<UserAccess?> ReadDatabaseAsync(string firebaseUid, CancellationToken cancellationToken)
    {
        var userId = await db.Users.AsNoTracking()
            .Where(u => u.FirebaseUid == firebaseUid)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var user = userId is null ? null : await LoadUserAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await LoadRolesAsync(user.RoleIds, cancellationToken);
        return ToAccess(userId!.Value, user, roles.Values);
    }

    private Task<CachedUser?> LoadUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new CachedUser(
                u.FirebaseUid,
                u.Status,
                u.LastLoginAt,
                u.StaffProfile != null && u.StaffProfile.MustChangePassword,
                u.UserRoles.Select(ur => ur.RoleId).ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Dictionary<Guid, CachedRole>> LoadRolesAsync(IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var roles = await db.Roles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.ScopingType,
                Keys = db.RolePermissions
                    .Where(rp => rp.RoleId == r.Id)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Key)
                    .ToArray(),
            })
            .ToListAsync(cancellationToken);
        return roles.ToDictionary(r => r.Id, r => new CachedRole(r.ScopingType, r.Keys));
    }

    private static UserAccess ToAccess(Guid userId, CachedUser user, IEnumerable<CachedRole> roles) =>
        new(userId, user.Status, user.LastLoginAt, user.MustChangePassword)
        {
            Permissions = UserAccess.MergeRoles(roles.Select(r => (r.ScopingType, (IEnumerable<string>)r.PermissionKeys))),
        };
}
