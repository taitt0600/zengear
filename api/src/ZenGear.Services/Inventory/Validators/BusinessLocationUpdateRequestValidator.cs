using System.Text.RegularExpressions;
using FluentValidation;
using ZenGear.Data.Entities.Inventory;
using ZenGear.Services.Inventory.Models;

namespace ZenGear.Services.Inventory.Validators;

/// <summary>
/// Input of <see cref="BusinessLocationUpdateRequest"/> (400, UC-070). Each period is checked on its own path
/// (<c>regularHours[1]</c>); once every period is well formed, the rules across periods of
/// <see cref="BusinessLocation.CheckHours"/> are checked on the list, and a closed holiday date with hours names R-LOC-01.
/// Whether the ward belongs to the province needs the database: the service checks it.
/// </summary>
public sealed partial class BusinessLocationUpdateRequestValidator : AbstractValidator<BusinessLocationUpdateRequest>
{
    public const int NameMaxLength = 150;
    public const int AddressDetailMaxLength = 255;
    public const int EmailMaxLength = 255;
    public const int MaxSpecialHours = 100;

    public BusinessLocationUpdateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n!.Trim().Length <= NameMaxLength).WithMessage($"Name must be at most {NameMaxLength} characters.");
        RuleFor(r => r.Phone).Cascade(CascadeMode.Stop)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Hotline is required.")
            .Must(p => PhonePattern().IsMatch(p!.Trim())).WithMessage("Hotline must be 8 to 11 digits.");
        RuleFor(r => r.Email!.Trim()).EmailAddress().MaximumLength(EmailMaxLength).OverridePropertyName(nameof(BusinessLocationUpdateRequest.Email))
            .When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.AddressDetail).Cascade(CascadeMode.Stop)
            .Must(a => !string.IsNullOrWhiteSpace(a)).WithMessage("Address is required.")
            .Must(a => a!.Trim().Length <= AddressDetailMaxLength)
            .WithMessage($"Address must be at most {AddressDetailMaxLength} characters.");
        RuleFor(r => r.ProvinceId).NotNull().GreaterThan(0);
        RuleFor(r => r.WardId).NotNull().GreaterThan(0);

        RuleFor(r => r.RegularHours).NotNull().WithMessage("Weekly hours are required.");
        RuleForEach(r => r.RegularHours)
            .NotNull().WithMessage("A period is required.")
            .ChildRules(period =>
            {
                period.RuleFor(p => p.Day).NotNull().InclusiveBetween(1, 7).WithMessage("Day must be 1 (Monday) to 7 (Sunday).");
                period.RuleFor(p => p.Open).Must(IsTime).WithMessage("Opening time must be HH:mm.");
                period.RuleFor(p => p.Close).Must(IsTime).WithMessage("Closing time must be HH:mm.");
            })
            .Must(p => p is null || OpensBeforeCloses(p.Open, p.Close)).WithMessage("A period opens before it closes.");

        RuleFor(r => r.SpecialHours).Must(s => s!.Count <= MaxSpecialHours)
            .WithMessage($"At most {MaxSpecialHours} holiday entries.").When(r => r.SpecialHours is not null);
        RuleForEach(r => r.SpecialHours)
            .NotNull().WithMessage("An entry is required.")
            .ChildRules(entry =>
            {
                entry.RuleFor(s => s.Date).NotNull();
                entry.RuleFor(s => s.Closed).NotNull();
                entry.RuleFor(s => s.Open).Must(IsTime).WithMessage("Opening time must be HH:mm.")
                    .When(s => s.Closed != true || s.Open is not null);
                entry.RuleFor(s => s.Close).Must(IsTime).WithMessage("Closing time must be HH:mm.")
                    .When(s => s.Closed != true || s.Close is not null);
            })
            .Must(s => s is null || s.Closed == true || OpensBeforeCloses(s.Open, s.Close)).WithMessage("A period opens before it closes.");

        RuleFor(r => r).Custom((request, context) =>
        {
            if (request.RegularHours is { } regular && regular.All(IsWellFormed)
                && BusinessLocation.RegularHoursProblem([.. regular.Select(p => p.ToPeriod())]) is { } regularProblem)
            {
                context.AddFailure(nameof(BusinessLocationUpdateRequest.RegularHours), regularProblem);
            }

            if (request.SpecialHours is { } special && special.All(IsWellFormed)
                && BusinessLocation.SpecialHoursProblem([.. special.Select(s => s.ToPeriod())]) is { } specialProblem)
            {
                var failure = new FluentValidation.Results.ValidationFailure(
                    nameof(BusinessLocationUpdateRequest.SpecialHours), specialProblem);
                if (specialProblem == BusinessLocation.ClosedDateWithHours)
                {
                    failure.ErrorCode = "R-LOC-01";
                }

                context.AddFailure(failure);
            }
        });
    }

    private static bool IsWellFormed(RegularHoursPeriodRequest? p) =>
        p is { Day: >= 1 and <= 7 } && IsTime(p.Open) && IsTime(p.Close) && OpensBeforeCloses(p.Open, p.Close);

    /// <summary>A closed entry may still carry times here: <see cref="BusinessLocation.SpecialHoursProblem"/> refuses them (R-LOC-01).</summary>
    private static bool IsWellFormed(SpecialHoursPeriodRequest? s) =>
        s?.Date is not null && s.Closed is not null && (s.Open is null || IsTime(s.Open)) && (s.Close is null || IsTime(s.Close))
        && (s.Closed == true || (IsTime(s.Open) && IsTime(s.Close) && OpensBeforeCloses(s.Open, s.Close)));

    private static bool OpensBeforeCloses(string? open, string? close) =>
        !IsTime(open) || !IsTime(close) || RegularHoursPeriodRequest.ParseTime(open!) < RegularHoursPeriodRequest.ParseTime(close!);

    private static bool IsTime(string? value) => value is not null && TimePattern().IsMatch(value);

    [GeneratedRegex("^[0-9]{8,11}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex PhonePattern();

    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex TimePattern();
}
