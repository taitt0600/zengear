using Microsoft.AspNetCore.Diagnostics;
using ZenGear.Services.Identity;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// <see cref="RecentSignInRequiredException"/> → 403 <c>/problems/recent-login-required</c> with <c>ruleId</c> R-USR-04:
/// the web signs the user in again with Firebase and retries (docs/API_CONVENTIONS.md, Authentication; DEC-094).
/// </summary>
public sealed class RecentSignInRequiredExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RecentSignInRequiredException)
        {
            return false;
        }

        await ForbiddenProblem.WriteAsync(httpContext, problemDetails, "recent-login-required", "Recent sign-in required",
            "Sign in again within 5 minutes before changing the password.", "R-USR-04");
        return true;
    }
}
