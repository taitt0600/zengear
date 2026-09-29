using ZenGear.Services.Identity;

namespace ZenGear.Api.Auth;

/// <summary>The <see cref="UserAccess"/> of the caller, stored by <see cref="ActiveUserMiddleware"/> for an ACTIVE user.</summary>
public static class UserAccessHttpContextExtensions
{
    public static UserAccess? GetUserAccess(this HttpContext context) =>
        context.Features.Get<UserAccessFeature>()?.Access;

    internal static void SetUserAccess(this HttpContext context, UserAccess access) =>
        context.Features.Set(new UserAccessFeature(access));

    private sealed record UserAccessFeature(UserAccess Access);
}
