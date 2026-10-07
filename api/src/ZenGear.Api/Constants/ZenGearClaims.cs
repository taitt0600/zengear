using System.Globalization;
using System.Security.Claims;
using ZenGear.Api.Middlewares;

namespace ZenGear.Api.Constants;

/// <summary>Claims added by <see cref="ActiveUserMiddleware"/> once the caller is a known, ACTIVE user.</summary>
public static class ZenGearClaims
{
    /// <summary><c>users.id</c>; required by the default and fallback policies.</summary>
    public const string UserId = "zengear:user_id";

    public const string MustChangePassword = "zengear:must_change_password";

    /// <summary>Firebase ID token claim: when the user signed in, in seconds since the Unix epoch.</summary>
    public const string AuthTime = "auth_time";

    /// <summary>Id of the signed-in user; only valid on an endpoint protected by the default or fallback policy.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(UserId)
            ?? throw new InvalidOperationException($"Claim {UserId} is missing: the endpoint is not protected by a ZenGear policy."));

    /// <summary>When the user signed in (<c>auth_time</c>); null when the token has no such claim.</summary>
    public static DateTimeOffset? GetAuthTime(this ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(AuthTime), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
