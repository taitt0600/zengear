using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ZenGear.Api.Attributes;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Api.Middlewares;

namespace ZenGear.Api.IntegrationTests.Authorization;

public class PermissionTests : IntegrationTestBase
{
    private const string Uid = "staff-uid-1";
    private const string Path = "/api/v1/admin/test/permission";

    [Test]
    public async Task R_SEC_03_endpoint_without_granted_permission_is_403_permission_denied()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var role = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(userId, role);
        await TestRoles.GrantAsync(role, "order.confirm");

        var response = await GetAsync(Path);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_SEC_03_permission_granted_through_any_role_is_200()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var cashier = await TestRoles.InsertAsync("Cashier");
        var warehouse = await TestRoles.InsertAsync("Warehouse Keeper");
        await TestRoles.AssignAsync(userId, cashier);
        await TestRoles.AssignAsync(userId, warehouse);
        await TestRoles.GrantAsync(warehouse, TestEndpointsController.TestPermission);

        var response = await GetAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScopeAsync(response)).ShouldBe("None");
    }

    [Test]
    public async Task R_SEC_03_role_named_Admin_without_the_grant_is_403()
    {
        var userId = await TestUsers.InsertAsync(Uid);
        var admin = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.AssignAsync(userId, admin);

        var response = await GetAsync(Path);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_SEC_03_SELF_scoped_role_exposes_SELF_scope()
    {
        var userId = await TestUsers.InsertAsync(Uid, isStaff: false);
        var customer = await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        await TestRoles.AssignAsync(userId, customer);
        await TestRoles.GrantAsync(customer, TestEndpointsController.TestPermission);

        var response = await GetAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScopeAsync(response)).ShouldBe("Self");
    }

    [Test]
    public async Task R_SEC_03_user_without_roles_is_403_permission_denied()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await GetAsync(Path);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_SEC_03_user_without_roles_still_gets_endpoints_open_to_any_registered_user()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await GetAsync("/api/v1/admin/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_SEC_03_unregistered_uid_on_permission_endpoint_is_403_account_not_registered()
    {
        var response = await GetAsync(Path);

        await response.ShouldBeForbiddenProblemAsync("/problems/account-not-registered", ruleId: null);
    }

    [Test]
    public async Task R_SEC_03_no_token_on_permission_endpoint_is_401()
    {
        using var request = TestTokens.Get(Path, token: null);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public void R_SEC_03_every_action_declares_its_access()
    {
        // Minimal-API endpoints (/health, OpenAPI, Scalar) are not controller actions and are out of scope on purpose.
        var apiAssembly = typeof(ActiveUserMiddleware).Assembly;
        var actions = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<ControllerActionDescriptor>())
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.ControllerTypeInfo.Assembly == apiAssembly)
            .ToList();

        actions.ShouldNotBeEmpty();
        var undeclared = actions
            .Where(a => !Declares(a.MethodInfo) && !Declares(a.ControllerTypeInfo)
                && !a.ControllerTypeInfo.IsDefined(typeof(ShopEndpointAttribute), inherit: true))
            .Select(a => $"{a.ControllerName}.{a.ActionName}")
            .ToList();
        undeclared.ShouldBeEmpty(
            "Every action needs [RequirePermission] or [AnyRegisteredUser], or its controller [ShopEndpoint] (AUT-04, R-SEC-03).");
    }

    private static bool Declares(MemberInfo member) =>
        member.IsDefined(typeof(RequirePermissionAttribute), inherit: true)
        || member.IsDefined(typeof(AnyRegisteredUserAttribute), inherit: true);

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = TestTokens.Get(path, TestTokens.EmulatorToken(Uid));
        return await Client.SendAsync(request);
    }

    private static async Task<string?> ScopeAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("scope").GetString();
}
