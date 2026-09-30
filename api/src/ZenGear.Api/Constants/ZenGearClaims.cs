using System.Security.Claims;
using ZenGear.Api.Middlewares;

namespace ZenGear.Api.Constants;

/// <summary>Claims added by <see cref="ActiveUserMiddleware"/> once the caller is a known, ACTIVE user.</summary>
public static class ZenGearClaims
{
    /// <summary><c>users.id</c>; required by the default and fallback policies.</summary>
    public const string UserId = "zengear:user_id";

    public const string MustChangePassword = "zengear:must_change_password";

    /// <summary>Id of the signed-in user; only valid on an endpoint protected by the default or fallback policy.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(UserId)
            ?? throw new InvalidOperationException($"Claim {UserId} is missing: the endpoint is not protected by a ZenGear policy."));
}
