using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// One staff account as <see cref="StaffViewQuery"/> projects it, in SQL, before <see cref="StaffDto"/> or
/// <see cref="StaffListItemDto"/> takes its fields: a class with init properties, which EF Core can compose further.
/// </summary>
internal sealed class StaffRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public string? Email { get; init; }

    public string Username { get; init; } = "";

    public string? EmployeeCode { get; init; }

    public DateOnly? JoinedDate { get; init; }

    public DateOnly? LeftDate { get; init; }

    public string? WorkPhone { get; init; }

    public UserStatus Status { get; init; }

    public bool MustChangePassword { get; init; }

    public List<StaffRoleDto> Roles { get; init; } = [];

    public bool? CanApprove { get; init; }

    public bool? CanWithdraw { get; init; }

    public StaffCreatorDto? CreatedBy { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public uint Version { get; init; }
}
