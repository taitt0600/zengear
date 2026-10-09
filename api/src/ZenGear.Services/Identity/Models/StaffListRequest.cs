using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Identity.Models;

/// <summary>The query of the staff list (R-USR-13, DEC-105); no parameter searches by name, email or phone.</summary>
/// <param name="Status"><c>ACTIVE</c> or <c>DISABLED</c>; null for both.</param>
/// <param name="RoleId">Only staff holding this role; a role that does not exist gives an empty page.</param>
/// <param name="Sort"><c>name</c> (null), <c>employeeCode</c> or <c>joinedDate</c>, descending with a leading <c>-</c>.</param>
public sealed record StaffListRequest(
    string? Status,
    Guid? RoleId,
    string? Sort,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize);
