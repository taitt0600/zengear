using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>A row of the staff list (R-USR-13): lighter than <see cref="StaffDto"/>, without the work phone.</summary>
/// <param name="LeftDate">Last working day; null while employed (R-USR-07).</param>
public sealed record StaffListItemDto(
    Guid Id,
    string Name,
    string? Email,
    string? EmployeeCode,
    DateOnly? JoinedDate,
    DateOnly? LeftDate,
    UserStatus Status,
    IReadOnlyList<StaffRoleDto> Roles);
