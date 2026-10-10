using ZenGear.Services.Identity.Account;

namespace ZenGear.Services.Identity.Access;

/// <summary>
/// The sign-in behind the request is more than <see cref="CurrentUserService.RecentSignInWindow"/> old, or its time is
/// unknown: the caller signs in again, then retries (R-USR-04, DEC-094). Written as 403
/// <c>/problems/recent-login-required</c>. Not a <c>BusinessRuleException</c>, which would be written as 409.
/// </summary>
public sealed class RecentSignInRequiredException()
    : Exception("A sign-in within the last 5 minutes is required.");
