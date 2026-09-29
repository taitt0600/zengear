using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZenGear.Services.Integrations.Redis;

namespace ZenGear.Services.Identity;

/// <summary>
/// <see cref="IAccessCache"/> on Redis. Keys (prefix <c>zengear:access:</c>):
/// <list type="bullet">
/// <item><c>epoch</c>, <c>ver:user:{id}</c>, <c>ver:role:{id}</c>: versions, no TTL (missing = 0). Redis must
/// not evict them (<c>maxmemory-policy</c> <c>volatile-*</c> or <c>noeviction</c>), or an old entry comes back;</item>
/// <item><c>user:{id}:{epoch}:{ver}</c>, <c>role:{id}:{epoch}:{ver}</c>: entries, TTL <see cref="EntryTtl"/>;</item>
/// <item><c>uid:{firebaseUid}</c>: the user id only, TTL <see cref="EntryTtl"/>.</item>
/// </list>
/// The TTL is only a safety net; changes take effect through the versions.
/// </summary>
public sealed class RedisAccessCache(RedisConnection redis, PendingAccessBumps pending, ILogger<RedisAccessCache> logger)
    : IAccessCache
{
    public static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(5);

    private const string Prefix = "zengear:access:";
    private static readonly RedisKey EpochKey = Prefix + "epoch";

    public bool IsAvailable => !pending.HasAny && redis.TryGetDatabase(out _);

    public static RedisKey UserVersionKey(Guid userId) => $"{Prefix}ver:user:{userId}";

    public static RedisKey RoleVersionKey(Guid roleId) => $"{Prefix}ver:role:{roleId}";

    public static RedisKey EpochVersionKey => EpochKey;

    public static RedisKey UserKey(Guid userId, AccessVersion version) =>
        $"{Prefix}user:{userId}:{version.Epoch}:{version.Version}";

    public static RedisKey RoleKey(Guid roleId, AccessVersion version) =>
        $"{Prefix}role:{roleId}:{version.Epoch}:{version.Version}";

    public static RedisKey UidKey(string firebaseUid) => $"{Prefix}uid:{firebaseUid}";

    public async Task<Guid?> GetUserIdAsync(string firebaseUid)
    {
        var value = await Database().StringGetAsync(UidKey(firebaseUid));
        return value.HasValue && Guid.TryParse(value.ToString(), out var id) ? id : null;
    }

    public Task SetUserIdAsync(string firebaseUid, Guid userId) =>
        Database().StringSetAsync(UidKey(firebaseUid), userId.ToString(), EntryTtl);

    public Task RemoveUserIdAsync(string firebaseUid) => Database().KeyDeleteAsync(UidKey(firebaseUid));

    public async Task<AccessVersion> GetUserVersionAsync(Guid userId)
    {
        var values = await Database().StringGetAsync([EpochKey, UserVersionKey(userId)]);
        return new AccessVersion(ToLong(values[0]), ToLong(values[1]));
    }

    public async Task<CachedUser?> GetUserAsync(Guid userId, AccessVersion version) =>
        Deserialize<CachedUser>(await Database().StringGetAsync(UserKey(userId, version)));

    public Task SetUserAsync(Guid userId, AccessVersion version, CachedUser user) =>
        Database().StringSetAsync(UserKey(userId, version), JsonSerializer.Serialize(user), EntryTtl);

    public async Task<AccessVersion[]> GetRoleVersionsAsync(IReadOnlyList<Guid> roleIds)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var keys = new RedisKey[roleIds.Count + 1];
        keys[0] = EpochKey;
        for (var i = 0; i < roleIds.Count; i++)
        {
            keys[i + 1] = RoleVersionKey(roleIds[i]);
        }

        var values = await Database().StringGetAsync(keys);
        var epoch = ToLong(values[0]);
        return values.Skip(1).Select(v => new AccessVersion(epoch, ToLong(v))).ToArray();
    }

    public async Task<CachedRole?[]> GetRolesAsync(IReadOnlyList<Guid> roleIds, IReadOnlyList<AccessVersion> versions)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var keys = roleIds.Select((id, i) => RoleKey(id, versions[i])).ToArray();
        var values = await Database().StringGetAsync(keys);
        return values.Select(Deserialize<CachedRole>).ToArray();
    }

    public async Task SetRolesAsync(IReadOnlyList<(Guid RoleId, AccessVersion Version, CachedRole Role)> roles)
    {
        var database = Database();
        await Task.WhenAll(roles.Select(r =>
            database.StringSetAsync(RoleKey(r.RoleId, r.Version), JsonSerializer.Serialize(r.Role), EntryTtl)));
    }

    public async Task BumpAsync(IReadOnlyCollection<AccessBump> bumps)
    {
        if (bumps.Count == 0)
        {
            return;
        }

        if (!redis.TryGetDatabase(out _))
        {
            pending.Add(bumps);
            return;
        }

        try
        {
            await IncrementAsync(bumps);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Access cache: {Count} version bump(s) failed, retrying; PostgreSQL is read meanwhile",
                bumps.Count);
            pending.Add(bumps);
        }
    }

    /// <summary>INCR of every version in one batch; throws on failure (used by the retry job).</summary>
    public async Task IncrementAsync(IReadOnlyCollection<AccessBump> bumps)
    {
        var batch = Database().CreateBatch();
        var increments = bumps.Select(b => batch.StringIncrementAsync(VersionKey(b))).ToList();
        batch.Execute();
        await Task.WhenAll(increments);
    }

    private static RedisKey VersionKey(AccessBump bump) => bump.Kind switch
    {
        AccessBumpKind.User => UserVersionKey(bump.Id),
        AccessBumpKind.Role => RoleVersionKey(bump.Id),
        _ => EpochKey,
    };

    private IDatabase Database() =>
        redis.TryGetDatabase(out var database) ? database : throw new InvalidOperationException("Redis is not connected.");

    private static long ToLong(RedisValue value) => value.HasValue ? (long)value : 0;

    private static T? Deserialize<T>(RedisValue value) where T : class =>
        value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString()) : null;
}
