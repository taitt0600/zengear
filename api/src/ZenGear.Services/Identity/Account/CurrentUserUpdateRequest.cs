namespace ZenGear.Services.Identity.Account;

/// <summary>
/// What a signed-in user may change on their own account: the display name only (R-USR-04). Other properties of the
/// body, such as an email, are ignored.
/// </summary>
/// <param name="Name">Nullable so the validator, not model binding, answers a missing name.</param>
public sealed record CurrentUserUpdateRequest(string? Name);
