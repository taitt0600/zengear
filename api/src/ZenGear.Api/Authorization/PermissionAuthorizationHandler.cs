using Microsoft.AspNetCore.Authorization;
using ZenGear.Api.Extensions;

namespace ZenGear.Api.Authorization;

/// <summary>Grants <see cref="PermissionRequirement"/> when a role of the caller has the key (R-SEC-03).</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is HttpContext httpContext && httpContext.GetUserAccess()?.HasPermission(requirement.Key) == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
