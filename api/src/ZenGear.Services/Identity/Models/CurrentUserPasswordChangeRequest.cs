namespace ZenGear.Services.Identity.Models;

/// <summary>The new password of the signed-in staff member (DEC-094, R-USR-11). Never log it.</summary>
/// <param name="NewPassword">Used exactly as typed: not trimmed, not normalised. Nullable so the R-USR-11 validator,
/// not model binding, answers a missing value.</param>
public sealed record CurrentUserPasswordChangeRequest(string? NewPassword);
