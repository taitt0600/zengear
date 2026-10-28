using Microsoft.AspNetCore.Diagnostics;
using ZenGear.Services.Identity.Access;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// <see cref="RecentSignInRequiredException"/> → 403 <c>/problems/recent-login-required</c> with <c>ruleId</c> R-USR-04.
/// </summary>
public sealed class RecentSignInRequiredExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RecentSignInRequiredException)
        {
            return false;
        }

        await ForbiddenProblem.WriteAsync(httpContext, problemDetails, "recent-login-required", "R-USR-04");
        return true;
    }
}
