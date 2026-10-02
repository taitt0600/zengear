using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.Models;

/// <summary>A staff account, for the staff screens.</summary>
/// <param name="MustChangePassword">The staff member must change the default password before anything else (R-USR-03).</param>
public sealed record StaffDto(
    Guid Id,
    string Name,
    string? Email,
    string? EmployeeCode,
    DateOnly? JoinedDate,
    string? WorkPhone,
    UserStatus Status,
    bool MustChangePassword,
    IReadOnlyList<StaffRoleDto> Roles);
