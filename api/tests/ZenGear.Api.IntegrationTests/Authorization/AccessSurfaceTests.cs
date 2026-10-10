using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ZenGear.Api.Attributes;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Api.Middlewares;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Api.IntegrationTests.Authorization;

/// <summary>DEC-106 (6): the routes of the production controllers and who may call them without a token.</summary>
public class AccessSurfaceTests : IntegrationTestBase
{
    private const string AdminPrefix = "api/v1/admin/";
    private const string ShopPrefix = "api/v1/shop/";

    [Test] // the checks below read the real endpoints; an empty or one-sided set would let them pass on nothing
    public void The_endpoints_of_both_prefixes_are_found()
    {
        var endpoints = ControllerEndpoints();

        endpoints.ShouldContain(e => e.Route.StartsWith(AdminPrefix, StringComparison.Ordinal));
        endpoints.ShouldContain(e => e.Route.StartsWith(ShopPrefix, StringComparison.Ordinal));
    }

    [Test]
    public void Every_route_is_under_admin_or_shop()
    {
        ControllerEndpoints().Select(e => RouteProblem(e.Route)).OfType<string>().ShouldBeEmpty();
    }

    [Test]
    public void A_route_outside_the_prefixes_fails()
    {
        RouteProblem("api/v1/staff").ShouldNotBeNull();
        RouteProblem("staff").ShouldNotBeNull();
        RouteProblem("api/v1/admin/api/v1/staff").ShouldNotBeNull();
        RouteProblem("api/v1/shop/products/api").ShouldNotBeNull();
        RouteProblem("api/v1/admin/staff/{id:guid}").ShouldBeNull();
        RouteProblem("api/v1/shop/business-locations").ShouldBeNull();
    }

    [Test]
    public void Only_shop_endpoints_are_anonymous_and_only_through_ShopEndpoint()
    {
        ControllerEndpoints().Select(e => AnonymityProblem(e.Route, e.Metadata)).OfType<string>().ShouldBeEmpty();
    }

    [Test]
    public void An_anonymous_admin_endpoint_or_a_shop_endpoint_without_ShopEndpoint_fails()
    {
        AnonymityProblem("api/v1/admin/staff", []).ShouldBeNull();
        AnonymityProblem("api/v1/admin/staff", [new AllowAnonymousAttribute()]).ShouldNotBeNull();
        AnonymityProblem("api/v1/admin/staff", [new ShopEndpointAttribute()]).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", []).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", [new AllowAnonymousAttribute()]).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", [new ShopEndpointAttribute(), new AllowAnonymousAttribute()]).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", [new ShopEndpointAttribute(), new RequirePermissionAttribute("order.read")]).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", [new ShopEndpointAttribute(), new AnyRegisteredUserAttribute()]).ShouldNotBeNull();
        AnonymityProblem("api/v1/shop/products", [new ShopEndpointAttribute()]).ShouldBeNull();
    }

    [Test] // R-SEC-10 is not in the name: this checks the type only, not what the DTO selects
    public void Shop_actions_return_only_shop_dtos()
    {
        ControllerEndpoints()
            .Where(e => e.Route.StartsWith(ShopPrefix, StringComparison.Ordinal))
            .Select(e => ShopReturnProblem(e.Action))
            .OfType<string>()
            .ShouldBeEmpty("A shop action returns a *ShopDto or PagedDto<*ShopDto> (R-SEC-10).");
    }

    [Test]
    public void A_shop_action_returning_another_type_fails()
    {
        ShopReturnProblem(Sample(nameof(ISampleActions.StaffDto))).ShouldNotBeNull();
        ShopReturnProblem(Sample(nameof(ISampleActions.StaffList))).ShouldNotBeNull();
        ShopReturnProblem(Sample(nameof(ISampleActions.Untyped))).ShouldNotBeNull();
        ShopReturnProblem(Sample(nameof(ISampleActions.ShopList))).ShouldBeNull();
        ShopReturnProblem(Sample(nameof(ISampleActions.ShopItem))).ShouldBeNull();
    }

    [Test]
    public async Task Admin_me_without_token_is_401()
    {
        using var request = TestTokens.Get("/api/v1/admin/me", token: null);

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private List<ControllerEndpoint> ControllerEndpoints()
    {
        // The test host's own controller (TestEndpointsController) is left out: only production endpoints count.
        var apiAssembly = typeof(ActiveUserMiddleware).Assembly;
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint, Action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(x => x.Action?.ControllerTypeInfo.Assembly == apiAssembly)
            .Select(x => new ControllerEndpoint(x.Endpoint.RoutePattern.RawText?.TrimStart('/') ?? "", x.Endpoint.Metadata,
                x.Action!.MethodInfo))
            .ToList();
        endpoints.ShouldNotBeEmpty();
        return endpoints;
    }

    private static string? RouteProblem(string route)
    {
        var prefix = new[] { AdminPrefix, ShopPrefix }.FirstOrDefault(p => route.StartsWith(p, StringComparison.Ordinal));
        if (prefix is null)
        {
            return $"{route}: not under /{AdminPrefix} or /{ShopPrefix}";
        }

        return route[prefix.Length..].Split('/').Any(segment => segment.Equals("api", StringComparison.OrdinalIgnoreCase))
            ? $"{route}: a second api/ segment after the prefix"
            : null;
    }

    private static string? AnonymityProblem(string route, IReadOnlyList<object> metadata)
    {
        var anonymous = metadata.OfType<IAllowAnonymous>().ToList();
        var shop = route.StartsWith(ShopPrefix, StringComparison.Ordinal);
        if (shop != anonymous.Count > 0)
        {
            return shop ? $"{route}: a shop route without [ShopEndpoint]" : $"{route}: an admin route open to anonymous callers";
        }

        if (anonymous.Any(a => a is not ShopEndpointAttribute))
        {
            return $"{route}: anonymous through something other than [ShopEndpoint]";
        }

        // [ShopEndpoint] is class-level, so it silently wins over any access an action declares.
        return shop && (metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<AnyRegisteredUserAttribute>().Any())
            ? $"{route}: declares an access that [ShopEndpoint] overrides"
            : null;
    }

    private static string? ShopReturnProblem(MethodInfo action)
    {
        var payload = Payload(action.ReturnType);
        var allowed = payload is not null && (IsShopDto(payload)
            || (payload.IsGenericType && payload.GetGenericTypeDefinition() == typeof(PagedDto<>) && IsShopDto(payload.GetGenericArguments()[0])));
        return allowed ? null : $"{action.DeclaringType!.Name}.{action.Name} returns {action.ReturnType.Name}";
    }

    /// <summary>Task&lt;ActionResult&lt;X&gt;&gt; → X; null when the action does not say what it returns.</summary>
    private static Type? Payload(Type type)
    {
        while (type.IsGenericType
               && type.GetGenericTypeDefinition() is var definition
               && (definition == typeof(Task<>) || definition == typeof(ValueTask<>) || definition == typeof(ActionResult<>)))
        {
            type = type.GetGenericArguments()[0];
        }

        return typeof(IActionResult).IsAssignableFrom(type) || type == typeof(Task) || type == typeof(ValueTask) || type == typeof(void)
            ? null
            : type;
    }

    private static bool IsShopDto(Type type) => type.Name.EndsWith("ShopDto", StringComparison.Ordinal);

    private static MethodInfo Sample(string name) => typeof(ISampleActions).GetMethod(name)!;

    private sealed record ControllerEndpoint(string Route, IReadOnlyList<object> Metadata, MethodInfo Action);

    /// <summary>Return types of shop actions, for <see cref="A_shop_action_returning_another_type_fails"/>.</summary>
    private interface ISampleActions
    {
        BusinessLocationDto StaffDto();

        Task<PagedDto<BusinessLocationDto>> StaffList();

        Task<IActionResult> Untyped();

        Task<PagedDto<BusinessLocationShopDto>> ShopList();

        Task<ActionResult<BusinessLocationShopDto>> ShopItem();
    }
}
