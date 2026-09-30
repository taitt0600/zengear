using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.ExceptionHandling;

namespace ZenGear.Api.Authorization;

/// <summary>
/// Problem Details for the two ZenGear refusals of an authenticated caller; every other result goes to the
/// default handler (401 without a valid token):
/// <list type="bullet">
/// <item>a valid Firebase token whose uid is linked to no user (no <see cref="ZenGearClaims.UserId"/>):
/// 403 <c>/problems/account-not-registered</c>, checked first so the web can tell it from a disabled account;</item>
/// <item>a registered user without the permission of a <see cref="RequirePermissionAttribute"/>:
/// 403 <c>/problems/permission-denied</c> (R-SEC-03).</item>
/// </list>
/// </summary>
public sealed class ZenGearAuthorizationResultHandler(IProblemDetailsService problemDetails) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            if (!context.User.HasClaim(c => c.Type == ZenGearClaims.UserId))
            {
                await ForbiddenProblem.WriteAsync(context, problemDetails, "account-not-registered", "Account not registered",
                    "No ZenGear account is linked to this sign-in.", ruleId: null);
                return;
            }

            var missing = authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PermissionRequirement>().ToList();
            if (missing is { Count: > 0 })
            {
                await ForbiddenProblem.WriteAsync(context, problemDetails, "permission-denied", "Permission denied",
                    $"Permission {string.Join(", ", missing.Select(r => r.Key))} is required.", "R-SEC-03");
                return;
            }
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
