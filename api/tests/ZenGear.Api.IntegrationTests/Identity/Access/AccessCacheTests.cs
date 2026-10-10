using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using StackExchange.Redis;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Data.Externals.Redis;
using ZenGear.Services.Identity.Access.AccessCache;

namespace ZenGear.Api.IntegrationTests.Identity.Access;

/// <summary>R-SEC-07: user status and permissions per role are cached in Redis; a change applies on the next request.</summary>
public class AccessCacheTests : IntegrationTestBase
{
    private const string Uid = "staff-uid-1";
    private const string PermissionPath = "/test/permission";

    [Test]
    public async Task R_SEC_07_second_request_is_served_from_cache()
    {
        var (_, roleId) = await InsertUserWithTestPermissionAsync();
        (await GetAsync(PermissionPath)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Raw SQL bypasses the invalidation: a cached answer proves the cache is read.
        await ExecuteSqlAsync("DELETE FROM role_permissions WHERE role_id = @id", roleId);

        (await GetAsync(PermissionPath)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_SEC_07_disabled_user_is_refused_on_the_next_request()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        await WarmCacheAsync("/test/me");

        await Factory.SetUserStatusAsync(userId, UserStatus.Disabled);

        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_07_stale_write_after_status_change_is_not_served()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        await WarmCacheAsync("/test/me");
        var cache = Factory.AccessCache();

        // A request read the version and the ACTIVE row, then the change committed, then the request wrote.
        var versionBefore = await cache.GetUserVersionAsync(userId);
        await Factory.SetUserStatusAsync(userId, UserStatus.Disabled);
        await cache.SetUserAsync(userId, versionBefore, ActiveEntry());

        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_07_stale_write_on_uid_miss_is_not_served()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var cache = Factory.AccessCache();
        (await cache.GetUserIdAsync(Uid)).ShouldBeNull();

        // uid miss: the request resolved the id, read the version, then the change committed, then it wrote.
        await cache.SetUserIdAsync(Uid, userId);
        var versionBefore = await cache.GetUserVersionAsync(userId);
        await Factory.SetUserStatusAsync(userId, UserStatus.Disabled);
        await cache.SetUserAsync(userId, versionBefore, ActiveEntry());

        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_07_failed_bump_is_retried_before_stale_entry_is_served()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        await WarmCacheAsync("/test/me");
        var versionKey = RedisAccessCache.UserVersionKey(userId);
        var cachedVersion = await ApiScope.VersionAsync(versionKey);
        await RedisContainer.Database.StringSetAsync(versionKey, "not-a-number"); // INCR now fails

        await Factory.SetUserStatusAsync(userId, UserStatus.Disabled);

        // The bump is pending: PostgreSQL is read meanwhile, although the ACTIVE entry is still cached.
        Factory.Services.GetRequiredService<PendingAccessBumps>().HasAny.ShouldBeTrue();
        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");

        await RedisContainer.Database.StringSetAsync(versionKey, cachedVersion); // Redis works again
        await Factory.WaitForPendingBumpsAsync();

        (await ApiScope.VersionAsync(versionKey)).ShouldBe(cachedVersion + 1);
        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_07_connection_restored_bumps_epoch()
    {
        (await ApiScope.VersionAsync(RedisAccessCache.EpochVersionKey)).ShouldBe(0);

        Factory.Services.GetRequiredService<RedisConnection>().RaiseConnectionRestored();
        await Factory.WaitForPendingBumpsAsync();

        (await ApiScope.VersionAsync(RedisAccessCache.EpochVersionKey)).ShouldBe(1);
    }

    [Test]
    public async Task R_SEC_07_new_staff_profile_with_must_change_password_applies_on_the_next_request()
    {
        var userId = await TestUsers.InsertAsync(Uid, isStaff: false);
        await WarmCacheAsync("/test/me");

        await Factory.WithDbAsync(async db =>
        {
            var profile = (StaffProfile)Activator.CreateInstance(typeof(StaffProfile), nonPublic: true)!;
            db.StaffProfiles.Add(profile);
            db.Entry(profile).Property(p => p.UserId).CurrentValue = userId;
            db.Entry(profile).Property(p => p.MustChangePassword).CurrentValue = true;
            await db.SaveChangesAsync();
        });

        await (await GetAsync("/test/me")).ShouldBeForbiddenProblemAsync("/problems/password-change-required", "R-USR-03");
    }

    [Test]
    public async Task R_SEC_07_removing_user_role_applies_on_the_next_request()
    {
        var (userId, roleId) = await InsertUserWithTestPermissionAsync();
        await TestRoles.AssignAsync(userId, await TestRoles.InsertAsync("Cashier")); // R-SEC-01: keeps one role
        await WarmCacheAsync(PermissionPath);

        await Factory.WithDbAsync(async db =>
        {
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(ur => ur.UserId == userId && ur.RoleId == roleId));
            await db.SaveChangesAsync();
        });

        await (await GetAsync(PermissionPath)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_SEC_07_revoking_role_permission_applies_on_the_next_request()
    {
        var (_, roleId) = await InsertUserWithTestPermissionAsync();
        await WarmCacheAsync(PermissionPath);

        await Factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.Include(r => r.RolePermissions).SingleAsync(r => r.Id == roleId);
            role.SetPermissions([]);
            await db.SaveChangesAsync();
        });

        await (await GetAsync(PermissionPath)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_SEC_07_version_is_bumped_after_commit_not_before()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var versionKey = RedisAccessCache.UserVersionKey(userId);

        await Factory.WithDbAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            db.Entry(user).Property(u => u.Status).CurrentValue = UserStatus.Disabled;
            await db.SaveChangesAsync();

            (await ApiScope.VersionAsync(versionKey)).ShouldBe(0);

            await transaction.CommitAsync();
        });

        (await ApiScope.VersionAsync(versionKey)).ShouldBe(1);
    }

    [Test]
    public async Task R_SEC_07_entries_expire_after_ttl()
    {
        await InsertUserWithTestPermissionAsync();
        (await GetAsync(PermissionPath)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(PermissionPath)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var server = RedisContainer.Database.Multiplexer.GetServers()[0];
        var entries = await server.KeysAsync(pattern: "zengear:access:*").ToListAsync();
        var versions = entries.Where(k => k.ToString().StartsWith("zengear:access:ver:", StringComparison.Ordinal)).ToList();
        var cached = entries.Except(versions).ToList();

        cached.ShouldContain(k => k.ToString().StartsWith("zengear:access:user:", StringComparison.Ordinal));
        cached.ShouldContain(k => k.ToString().StartsWith("zengear:access:role:", StringComparison.Ordinal));
        cached.ShouldContain(k => k.ToString().StartsWith("zengear:access:uid:", StringComparison.Ordinal));
        foreach (var key in cached)
        {
            var ttl = await RedisContainer.Database.KeyTimeToLiveAsync(key);
            ttl.ShouldNotBeNull();
            ttl.Value.ShouldBeLessThanOrEqualTo(RedisAccessCache.EntryTtl);
        }

        versions.ShouldNotBeEmpty();
        foreach (var key in versions)
        {
            (await RedisContainer.Database.KeyTimeToLiveAsync(key)).ShouldBeNull();
        }
    }

    [Test]
    public async Task R_SEC_11_sign_in_recorded_once_with_cache()
    {
        await TestUsers.InsertAsync(Uid);
        Factory.SecurityLogs.Clear();
        var token = TestTokens.EmulatorToken(Uid);

        for (var i = 0; i < 3; i++)
        {
            using var request = TestTokens.Get("/test/me", token);
            (await Client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        Factory.SecurityLogs.Entries.ShouldHaveSingleItem().EventName.ShouldBe("LoginSucceeded");
    }

    /// <summary>
    /// Two requests: the first records the sign-in, which bumps the user version, so only the second one
    /// leaves an entry that the next request reads from the cache.
    /// </summary>
    private async Task WarmCacheAsync(string path)
    {
        (await GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static CachedUser ActiveEntry() => new(Uid, UserStatus.Active, null, false, []);

    private static async Task<(Guid UserId, Guid RoleId)> InsertUserWithTestPermissionAsync()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var roleId = await TestRoles.InsertAsync("Warehouse Keeper");
        await TestRoles.AssignAsync(userId, roleId);
        await TestRoles.GrantAsync(roleId, TestEndpointsController.TestPermission);
        return (userId, roleId);
    }

    private static async Task ExecuteSqlAsync(string sql, Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = TestTokens.Get(path, TestTokens.EmulatorToken(Uid));
        return await Client.SendAsync(request);
    }
}
