using System.Collections.Frozen;
using FluentValidation;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Common.Validators;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Input of <see cref="StaffListRequest"/> (400): only the documented values, compared exactly. Each check has its own
/// code (DEC-117 (2)) and carries the rule <see cref="RuleId"/> as its state, so the 400 names the rule; the value sent
/// is never written back.
/// </summary>
public sealed class StaffListRequestValidator : AbstractValidator<StaffListRequest>
{
    public const string RuleId = "R-USR-13";

    public const string StatusInvalid = "STATUS_INVALID";

    public const string SortInvalid = "SORT_INVALID";

    /// <summary>The stored values a list may filter on, and their status; ANONYMIZED accounts are never listed.</summary>
    public static readonly FrozenDictionary<string, UserStatus> Statuses = new Dictionary<string, UserStatus>
    {
        ["ACTIVE"] = UserStatus.Active,
        ["DISABLED"] = UserStatus.Disabled,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static readonly FrozenSet<string> Sorts = new[]
    {
        "name", "-name", "employeeCode", "-employeeCode", "joinedDate", "-joinedDate",
    }.ToFrozenSet(StringComparer.Ordinal);

    public StaffListRequestValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s is null || Statuses.ContainsKey(s)).WithErrorCode(StatusInvalid).WithState(_ => RuleId);
        RuleFor(r => r.Sort)
            .Must(s => s is null || Sorts.Contains(s)).WithErrorCode(SortInvalid).WithState(_ => RuleId);
        RuleFor(r => r.Page)
            .InclusiveBetween(1, PageRequest.MaxPage).WithErrorCode(PageRequestValidator.PageOutOfRange).WithState(_ => RuleId);
        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode(PageRequestValidator.PageSizeOutOfRange).WithState(_ => RuleId);
    }
}
