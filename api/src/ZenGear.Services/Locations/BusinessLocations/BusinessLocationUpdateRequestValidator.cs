using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using ZenGear.Data.Entities.Locations;

namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>
/// Input of <see cref="BusinessLocationUpdateRequest"/> (400, UC-070). Each period is checked on its own path
/// (<c>regularHours[1]</c>); once every period is well formed, the rules across periods of
/// <see cref="BusinessLocation.CheckHours"/> are checked on the list, and a closed holiday date with hours names R-LOC-01.
/// Each check has its own code (DEC-117 (2)); the API writes its text.
/// Whether the ward belongs to the province needs the database: the service checks it.
/// </summary>
public sealed partial class BusinessLocationUpdateRequestValidator : AbstractValidator<BusinessLocationUpdateRequest>
{
    public const int NameMaxLength = 150;
    public const int AddressDetailMaxLength = 255;
    public const int EmailMaxLength = 255;
    public const int MaxSpecialHours = 100;

    /// <summary>The digit counts of the hotline, for the text of its error.</summary>
    public const int PhoneMinDigits = 8;
    public const int PhoneMaxDigits = 11;

    // One code per check (DEC-117 (2)); the checks across periods return the codes of BusinessLocation.
    public const string NameRequired = "NAME_REQUIRED";
    public const string NameTooLong = "NAME_TOO_LONG";
    public const string PhoneRequired = "PHONE_REQUIRED";
    public const string PhoneInvalid = "PHONE_INVALID";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string EmailTooLong = "EMAIL_TOO_LONG";
    public const string AddressDetailRequired = "ADDRESS_DETAIL_REQUIRED";
    public const string AddressDetailTooLong = "ADDRESS_DETAIL_TOO_LONG";
    public const string ProvinceIdRequired = "PROVINCE_ID_REQUIRED";
    public const string ProvinceIdInvalid = "PROVINCE_ID_INVALID";
    public const string WardIdRequired = "WARD_ID_REQUIRED";
    public const string WardIdInvalid = "WARD_ID_INVALID";
    public const string RegularHoursRequired = "REGULAR_HOURS_REQUIRED";
    public const string PeriodRequired = "PERIOD_REQUIRED";
    public const string DayRequired = "DAY_REQUIRED";
    public const string CloseTimeInvalid = "CLOSE_TIME_INVALID";
    public const string SpecialHoursTooMany = "SPECIAL_HOURS_TOO_MANY";
    public const string DateRequired = "DATE_REQUIRED";
    public const string ClosedRequired = "CLOSED_REQUIRED";

    /// <summary>The rule a closed holiday date with hours breaks.</summary>
    public const string ClosedDateRuleId = "R-LOC-01";

    public BusinessLocationUpdateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(NameRequired)
            .Must((_, n, context) => FitsMaxLength(n!, NameMaxLength, context)).WithErrorCode(NameTooLong);
        RuleFor(r => r.Phone).Cascade(CascadeMode.Stop)
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithErrorCode(PhoneRequired)
            .Must(IsPhone).WithErrorCode(PhoneInvalid);
        RuleFor(r => r.Email!.Trim())
            .EmailAddress().WithErrorCode(EmailInvalid)
            .MaximumLength(EmailMaxLength).WithErrorCode(EmailTooLong)
            .OverridePropertyName(nameof(BusinessLocationUpdateRequest.Email))
            .When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.AddressDetail).Cascade(CascadeMode.Stop)
            .Must(a => !string.IsNullOrWhiteSpace(a)).WithErrorCode(AddressDetailRequired)
            .Must((_, a, context) => FitsMaxLength(a!, AddressDetailMaxLength, context)).WithErrorCode(AddressDetailTooLong);
        RuleFor(r => r.ProvinceId).NotNull().WithErrorCode(ProvinceIdRequired).GreaterThan(0).WithErrorCode(ProvinceIdInvalid);
        RuleFor(r => r.WardId).NotNull().WithErrorCode(WardIdRequired).GreaterThan(0).WithErrorCode(WardIdInvalid);

        RuleFor(r => r.RegularHours).NotNull().WithErrorCode(RegularHoursRequired);
        RuleForEach(r => r.RegularHours)
            .NotNull().WithErrorCode(PeriodRequired)
            .ChildRules(period =>
            {
                period.RuleFor(p => p.Day).NotNull().WithErrorCode(DayRequired)
                    .InclusiveBetween(1, 7).WithErrorCode(BusinessLocation.DayOutOfRange);
                period.RuleFor(p => p.Open).Must(IsTime).WithErrorCode(BusinessLocation.OpenTimeInvalid);
                period.RuleFor(p => p.Close).Must(IsTime).WithErrorCode(CloseTimeInvalid);
            })
            .Must(p => p is null || OpensBeforeCloses(p.Open, p.Close)).WithErrorCode(BusinessLocation.PeriodOpensAfterClosing);

        RuleFor(r => r.SpecialHours).Must(HasAtMostMaxSpecialHours).WithErrorCode(SpecialHoursTooMany)
            .When(r => r.SpecialHours is not null);
        RuleForEach(r => r.SpecialHours)
            .NotNull().WithErrorCode(PeriodRequired)
            .ChildRules(entry =>
            {
                entry.RuleFor(s => s.Date).NotNull().WithErrorCode(DateRequired);
                entry.RuleFor(s => s.Closed).NotNull().WithErrorCode(ClosedRequired);
                entry.RuleFor(s => s.Open).Must(IsTime).WithErrorCode(BusinessLocation.OpenTimeInvalid)
                    .When(s => s.Closed != true || s.Open is not null);
                entry.RuleFor(s => s.Close).Must(IsTime).WithErrorCode(CloseTimeInvalid)
                    .When(s => s.Closed != true || s.Close is not null);
            })
            .Must(s => s is null || s.Closed == true || OpensBeforeCloses(s.Open, s.Close))
            .WithErrorCode(BusinessLocation.PeriodOpensAfterClosing);

        RuleFor(r => r).Custom((request, context) =>
        {
            if (request.RegularHours is { } regular && regular.All(IsWellFormed)
                && BusinessLocation.RegularHoursProblem([.. regular.Select(p => p.ToPeriod())]) is { } regularProblem)
            {
                context.AddFailure(HoursFailure(nameof(BusinessLocationUpdateRequest.RegularHours), regularProblem));
            }

            if (request.SpecialHours is { } special && special.All(IsWellFormed)
                && BusinessLocation.SpecialHoursProblem([.. special.Select(s => s.ToPeriod())]) is { } specialProblem)
            {
                context.AddFailure(HoursFailure(nameof(BusinessLocationUpdateRequest.SpecialHours), specialProblem));
            }
        });
    }

    /// <summary>
    /// A failure of the checks across periods: its code, the rule a closed holiday date with hours breaks, and the
    /// limit of <see cref="BusinessLocation.PeriodsPerDayTooMany"/> for its text (<c>{MaxCount}</c>).
    /// </summary>
    private static ValidationFailure HoursFailure(string propertyName, string code) => new(propertyName, code)
    {
        ErrorCode = code,
        CustomState = code == BusinessLocation.ClosedDateWithHours ? ClosedDateRuleId : null,
        FormattedMessagePlaceholderValues = code == BusinessLocation.PeriodsPerDayTooMany
            ? new Dictionary<string, object> { ["MaxCount"] = BusinessLocation.MaxPeriodsPerDay }
            : null,
    };

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsMaxLength(string value, int maxLength, ValidationContext<BusinessLocationUpdateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", maxLength);
        return value.Trim().Length <= maxLength;
    }

    /// <summary>Also gives the digit counts to the text of the error (<c>{MinDigits}</c>, <c>{MaxDigits}</c>).</summary>
    private static bool IsPhone(BusinessLocationUpdateRequest request, string? phone, ValidationContext<BusinessLocationUpdateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MinDigits", PhoneMinDigits).AppendArgument("MaxDigits", PhoneMaxDigits);
        return PhonePattern().IsMatch(phone!.Trim());
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxCount}</c>).</summary>
    private static bool HasAtMostMaxSpecialHours(BusinessLocationUpdateRequest request,
        IReadOnlyList<SpecialHoursPeriodRequest>? specialHours, ValidationContext<BusinessLocationUpdateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxCount", MaxSpecialHours);
        return specialHours!.Count <= MaxSpecialHours;
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
