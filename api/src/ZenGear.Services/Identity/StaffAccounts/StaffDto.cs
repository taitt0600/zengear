using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>A staff account, for the staff screens.</summary>
/// <param name="Username">Generated sign-in name (R-USR-24); <paramref name="Email"/> is it in lower case on the company domain.</param>
/// <param name="EmployeeCode">Generated employee code (R-USR-24); null only once the retention job erased it (DEC-156 (3)).</param>
/// <param name="LeftDate">Last working day; null while employed (R-USR-07).</param>
/// <param name="MustChangePassword">The staff member must change the default password before anything else (R-USR-03).</param>
public sealed record StaffDto(
    Guid Id,
    string Name,
    string? Email,
    string Username,
    string? EmployeeCode,
    DateOnly? JoinedDate,
    DateOnly? LeftDate,
    string? WorkPhone,
    UserStatus Status,
    bool MustChangePassword,
    IReadOnlyList<StaffRoleDto> Roles);
