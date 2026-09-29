using ZenGear.Data.Entities.Identity;

namespace ZenGear.Services.Identity;

/// <summary>What the API needs about the caller on every request.</summary>
/// <param name="LastLoginAt"><c>auth_time</c> of the last recorded sign-in.</param>
/// <param name="MustChangePassword">Staff only; false for a customer.</param>
public sealed record UserAccess(Guid Id, UserStatus Status, DateTimeOffset? LastLoginAt, bool MustChangePassword)
{
    /// <summary>Only an ACTIVE user may sign in and use the system (R-SEC-04).</summary>
    public bool IsActive => Status == UserStatus.Active;

    /// <summary>A token whose <c>auth_time</c> is later than <c>last_login_at</c> starts a new session (R-SEC-11).</summary>
    public bool IsNewSignIn(DateTimeOffset authTime) => LastLoginAt is null || authTime > LastLoginAt;
}
