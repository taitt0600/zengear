using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ZenGear.Api.Auth;

/// <summary>
/// A valid Firebase token whose uid is linked to no user is refused by the policies (no
/// <see cref="ZenGearClaims.UserId"/>); answer 403 <c>/problems/account-not-registered</c> so the web can
/// tell it from a disabled account. Every other result goes to the default handler.
/// </summary>
public sealed class AccountNotRegisteredResultHandler(IProblemDetailsService problemDetails) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.User.Identity?.IsAuthenticated == true
            && !context.User.HasClaim(c => c.Type == ZenGearClaims.UserId))
        {
            await ForbiddenProblem.WriteAsync(context, problemDetails, "account-not-registered", "Account not registered",
                "No ZenGear account is linked to this sign-in.", ruleId: null);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
