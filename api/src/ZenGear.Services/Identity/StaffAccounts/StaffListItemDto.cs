using System.Globalization;
using System.Text.Json.Serialization;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>A row of the staff list (R-USR-13): lighter than <see cref="StaffDto"/>, without the work phone.</summary>
/// <param name="LeftDate">Last working day; null while employed (R-USR-07).</param>
/// <param name="CanApprove">As <see cref="StaffDto.CanApprove"/> (DEC-137 (1)).</param>
/// <param name="CanWithdraw">As <see cref="StaffDto.CanWithdraw"/> (DEC-140 (1)).</param>
/// <param name="CreatedBy">As <see cref="StaffDto.CreatedBy"/> (DEC-137 (1)).</param>
/// <param name="CreatedAt">As <see cref="StaffDto.CreatedAt"/> (DEC-137 (1)).</param>
/// <param name="RowVersion">The account's version; shown as <see cref="Version"/> for a PENDING row only.</param>
public sealed record StaffListItemDto(
    Guid Id,
    string Name,
    string? Email,
    string? EmployeeCode,
    DateOnly? JoinedDate,
    DateOnly? LeftDate,
    UserStatus Status,
    IReadOnlyList<StaffRoleDto> Roles,
    bool? CanApprove,
    bool? CanWithdraw,
    StaffCreatorDto? CreatedBy,
    DateTimeOffset? CreatedAt,
    [property: JsonIgnore] uint RowVersion)
{
    /// <summary>
    /// PENDING only: the value of the account's <c>ETag</c> (<c>"123"</c>, quotes included), which approve, reject and
    /// withdraw send back as <c>If-Match</c> (DEC-140 (2)). Null otherwise.
    /// </summary>
    public string? Version => Status == UserStatus.Pending
        ? $"\"{RowVersion.ToString(CultureInfo.InvariantCulture)}\""
        : null;
}
