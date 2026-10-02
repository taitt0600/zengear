namespace ZenGear.Services.Identity.Models;

/// <summary>A new staff account and its roles (STF-01, UC-005).</summary>
/// <param name="Email">Sign-in identity of the staff member; stored trimmed and lowercase (R-USR-02).</param>
/// <param name="Name">Display name.</param>
/// <param name="JoinedDate">First working day, Vietnam date.</param>
/// <param name="RoleIds">One or more internal roles (R-SEC-01, R-USR-01); only an Admin gives the Admin role (R-TX-11).</param>
public sealed record StaffCreateRequest(
    string Email,
    string Name,
    string EmployeeCode,
    DateOnly JoinedDate,
    string? WorkPhone,
    IReadOnlyList<Guid> RoleIds);
