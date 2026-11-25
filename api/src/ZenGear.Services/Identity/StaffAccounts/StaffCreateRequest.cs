namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// A new staff account and its roles (STF-01, UC-005). The employee code, the username and the company email are not
/// part of it: the API generates them from the name and the joined date (R-USR-24). The texts and the list are nullable
/// so the validator, not model binding, answers a missing one.
/// </summary>
/// <param name="Name">Full name, the display name; written with its diacritics, family name first (R-USR-24).</param>
/// <param name="JoinedDate">First working day, Vietnam date; its year is the year of the employee code (R-USR-24).</param>
/// <param name="RoleIds">One or more internal roles (R-SEC-01, R-USR-01); only an Admin gives the Admin role (R-TX-11).</param>
public sealed record StaffCreateRequest(
    string? Name,
    DateOnly JoinedDate,
    string? WorkPhone,
    IReadOnlyList<Guid>? RoleIds);
