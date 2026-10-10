using System.Collections.Frozen;
using FluentValidation;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Input of <see cref="StaffListRequest"/> (400): only the documented values, compared exactly. Every failure carries the
/// error code <see cref="RuleId"/>, so the 400 names the rule; no message repeats the value sent.
/// </summary>
public sealed class StaffListRequestValidator : AbstractValidator<StaffListRequest>
{
    public const string RuleId = "R-USR-13";

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
            .Must(s => s is null || Statuses.ContainsKey(s))
            .WithMessage("Status must be ACTIVE or DISABLED.").WithErrorCode(RuleId);
        RuleFor(r => r.Sort)
            .Must(s => s is null || Sorts.Contains(s))
            .WithMessage("Sort must be name, employeeCode or joinedDate, with a leading - for descending.").WithErrorCode(RuleId);
        RuleFor(r => r.Page)
            .InclusiveBetween(1, PageRequest.MaxPage)
            .WithMessage($"Page must be from 1 to {PageRequest.MaxPage}.").WithErrorCode(RuleId);
        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"Page size must be from 1 to {PageRequest.MaxPageSize}.").WithErrorCode(RuleId);
    }
}
