using System.Text.Json.Serialization;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>A staff account, for the staff screens.</summary>
/// <param name="Username">Generated sign-in name (R-USR-24); <paramref name="Email"/> is it in lower case on the company domain.</param>
/// <param name="EmployeeCode">Generated employee code (R-USR-24); null only once the retention job erased it (DEC-156 (3)).</param>
/// <param name="LeftDate">Last working day; null while employed (R-USR-07).</param>
/// <param name="MustChangePassword">The staff member must change the default password before anything else (R-USR-03).</param>
/// <param name="CanApprove">PENDING only: the caller holds <c>staff.approve</c>, did not create the account and manages
/// all its roles (R-USR-17, R-SEC-12, DEC-138); it only shows the buttons, the API checks again. Null otherwise.</param>
/// <param name="CanWithdraw">PENDING only: the caller created the account and holds <c>staff.create</c> (DEC-140 (1)).
/// Null otherwise.</param>
/// <param name="CreatedBy">PENDING only: who created the account; null for a bootstrap or seed account, a creator
/// anonymized, and every other status (DEC-138).</param>
/// <param name="CreatedAt">PENDING only: when the account was created, UTC. Null otherwise.</param>
/// <param name="Version">The account's version, sent as the <c>ETag</c> (DEC-140 (2)).</param>
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
    IReadOnlyList<StaffRoleDto> Roles,
    bool? CanApprove,
    bool? CanWithdraw,
    StaffCreatorDto? CreatedBy,
    DateTimeOffset? CreatedAt,
    [property: JsonIgnore] uint Version);
