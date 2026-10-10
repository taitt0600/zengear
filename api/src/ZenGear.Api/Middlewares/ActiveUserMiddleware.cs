using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ZenGear.Api.Attributes;
using ZenGear.Api.Authorization;
using ZenGear.Api.Constants;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Api.Logging;
using ZenGear.Services.Identity.Access;

namespace ZenGear.Api.Middlewares;

/// <summary>
/// Runs after authentication, for requests with a valid Firebase ID token:
/// <list type="bullet">
/// <item>a user that is not ACTIVE is refused on every request and logged REJECTED (R-SEC-04, R-SEC-11);</item>
/// <item>an ACTIVE user gets the <see cref="ZenGearClaims"/> and its <c>UserAccess</c> (permissions, read by
/// <see cref="PermissionAuthorizationHandler"/>), and the first request of each sign-in
/// (<c>auth_time</c> later than <c>last_login_at</c>) records it and logs SUCCESS (R-SEC-11);</item>
/// <item>staff who must change their password are refused on protected endpoints not marked
/// <see cref="AllowWhilePasswordChangeRequiredAttribute"/> (R-USR-03).</item>
/// </list>
/// An unregistered Firebase uid gets no claim and goes on: the policies refuse it, anonymous endpoints
/// (and the AUT-02 registration endpoint) accept it.
/// </summary>
public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserAccessReader users, SignInService signIn,
        SecurityLog securityLog, IProblemDetailsService problemDetails)
    {
        var principal = context.User;
        var firebaseUid = principal.FindFirstValue("sub");
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity || firebaseUid is null)
        {
            await next(context);
            return;
        }

        var access = await users.GetAsync(firebaseUid, context.RequestAborted);
        if (access is null)
        {
            await next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

        if (!access.IsActive)
        {
            securityLog.LoginRejected(access.Id, "ACCOUNT_DISABLED", ip, userAgent);
            await ForbiddenProblem.WriteAsync(context, problemDetails, "account-disabled", "Account disabled",
                "This account is not active.", "R-SEC-04");
            return;
        }

        context.SetUserAccess(access);
        identity.AddClaim(new Claim(ZenGearClaims.UserId, access.Id.ToString()));
        identity.AddClaim(new Claim(ZenGearClaims.MustChangePassword, access.MustChangePassword ? "true" : "false"));

        if (principal.GetAuthTime() is { } authTime
            && access.IsNewSignIn(authTime)
            && await signIn.RecordSignInAsync(access.Id, authTime, context.RequestAborted))
        {
            securityLog.LoginSucceeded(access.Id, authTime, ip, userAgent);
        }

        // Public endpoints stay open: they need no account, so there is nothing to protect.
        var endpoint = context.GetEndpoint();
        if (access.MustChangePassword
            && endpoint?.Metadata.GetMetadata<AllowWhilePasswordChangeRequiredAttribute>() is null
            && endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is null)
        {
            await ForbiddenProblem.WriteAsync(context, problemDetails, "password-change-required", "Password change required",
                "Change your password before using other functions.", "R-USR-03");
            return;
        }

        await next(context);
    }
}
