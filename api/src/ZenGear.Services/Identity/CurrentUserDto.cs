namespace ZenGear.Services.Identity;

/// <summary>The signed-in user, read by the web right after sign-in.</summary>
/// <param name="IsStaff">Has a staff profile.</param>
/// <param name="MustChangePassword">Every other endpoint is refused until the password is changed (R-USR-03).</param>
public sealed record CurrentUserDto(Guid Id, string Name, string? Email, bool IsStaff, bool MustChangePassword);
