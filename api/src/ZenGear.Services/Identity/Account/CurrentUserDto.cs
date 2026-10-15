using System.Text.Json.Serialization;

namespace ZenGear.Services.Identity.Account;

/// <summary>The signed-in user, read by the web right after sign-in.</summary>
/// <param name="IsStaff">Has a staff profile.</param>
/// <param name="MustChangePassword">Every other endpoint is refused until the password is changed (R-USR-03).</param>
/// <param name="Permissions">The permission keys granted through the user's roles, in ordinal order, without scope or
/// role names: the web only hides the menus and buttons the user may not use; the API checks every permission
/// (R-SEC-03).</param>
/// <param name="Version"><c>users.xmin</c>, sent as the <c>ETag</c> header and never in the body; it does not change
/// with <paramref name="MustChangePassword"/>, which lives in <c>staff_profiles</c>, nor with the roles.</param>
public sealed record CurrentUserDto(
    Guid Id,
    string Name,
    string? Email,
    bool IsStaff,
    bool MustChangePassword,
    IReadOnlyList<string> Permissions,
    [property: JsonIgnore] uint Version);
