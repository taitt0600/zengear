using System.Diagnostics;
using System.Net;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Identity.Access;

/// <summary>R-SEC-07: without Redis every request reads PostgreSQL, quickly and with the same answers.</summary>
public class AccessCacheRedisUnavailableTests : IntegrationTestBase
{
    private const string Uid = "staff-uid-1";

    /// <summary>Nothing listens on port 1.</summary>
    protected override ZenGearApiFactory CreateFactory() => new(redisConnectionString: "127.0.0.1:1");

    [Test]
    public async Task R_SEC_07_redis_unavailable_falls_back_to_database()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var role = await TestRoles.InsertAsync("Warehouse Keeper");
        await TestRoles.AssignAsync(userId, role);
        await TestRoles.GrantAsync(role, TestEndpointsController.TestPermission);

        var granted = await TimedGetAsync("/api/v1/admin/test/permission");
        granted.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        granted.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));

        await Factory.SetUserStatusAsync(userId, Data.Entities.Identity.Enums.UserStatus.Disabled);

        var refused = await TimedGetAsync("/api/v1/admin/test/permission");
        await refused.Response.ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
        refused.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    private async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedGetAsync(string path)
    {
        using var request = TestTokens.Get(path, TestTokens.EmulatorToken(Uid));
        var stopwatch = Stopwatch.StartNew();
        var response = await Client.SendAsync(request);
        return (response, stopwatch.Elapsed);
    }
}
