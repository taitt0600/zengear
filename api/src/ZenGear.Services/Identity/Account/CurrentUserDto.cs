using System.Text.Json.Serialization;

namespace ZenGear.Services.Identity.Account;

/// <summary>The signed-in user, read by the web right after sign-in.</summary>
/// <param name="IsStaff">Has a staff profile.</param>
/// <param name="MustChangePassword">Every other endpoint is refused until the password is changed (R-USR-03).</param>
/// <param name="Version"><c>users.xmin</c>, sent as the <c>ETag</c> header and never in the body; it does not change
/// with <paramref name="MustChangePassword"/>, which lives in <c>staff_profiles</c>.</param>
public sealed record CurrentUserDto(
    Guid Id,
    string Name,
    string? Email,
    bool IsStaff,
    bool MustChangePassword,
    [property: JsonIgnore] uint Version);
