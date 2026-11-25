using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Input of <see cref="StaffCreateRequest"/> (400), one code per check (DEC-117 (2)). An empty role list is not an input
/// error but R-SEC-01 (409), checked by <see cref="StaffService"/>.
/// </summary>
public sealed class StaffCreateRequestValidator : AbstractValidator<StaffCreateRequest>
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// The longest username (R-USR-24): the email's local part is the username in lower case, at most 64 characters
    /// (RFC 5321), and <c>staff_profiles.username</c> is varchar(64).
    /// </summary>
    public const int UsernameMaxLength = 64;

    /// <summary>Digits with an optional leading +; fits <c>staff_profiles.work_phone</c> varchar(20).</summary>
    public const string WorkPhonePattern = @"^\+?[0-9]{8,15}$";

    /// <summary>The digit counts of <see cref="WorkPhonePattern"/>, for the text of its error.</summary>
    public const int WorkPhoneMinDigits = 8;

    public const int WorkPhoneMaxDigits = 15;

    public const int MaxRoles = 20;

    public const string NameRequired = "NAME_REQUIRED";

    public const string NameTooLong = "NAME_TOO_LONG";

    public const string NameNoLetter = "NAME_NO_LETTER";

    public const string NameUsernameTooLong = "NAME_USERNAME_TOO_LONG";

    public const string JoinedDateRequired = "JOINED_DATE_REQUIRED";

    public const string JoinedDateOutOfRange = "JOINED_DATE_OUT_OF_RANGE";

    public const string WorkPhoneInvalid = "WORK_PHONE_INVALID";

    public const string RoleIdsRequired = "ROLE_IDS_REQUIRED";

    public const string RoleIdsTooMany = "ROLE_IDS_TOO_MANY";

    public const string RoleIdEmpty = "ROLE_ID_EMPTY";

    public StaffCreateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(NameRequired)
            .Must((_, n, context) => FitsMaxLength(n?.Trim().Length ?? 0, NameMaxLength, context)).WithErrorCode(NameTooLong)
            .Must(n => n is not null && StaffIdentityGenerator.UsernameLength(n) is not null).WithErrorCode(NameNoLetter)
            .Must((_, n, context) => FitsMaxLength(StaffIdentityGenerator.UsernameLength(n ?? "") ?? 0, UsernameMaxLength, context))
            .WithErrorCode(NameUsernameTooLong);

        RuleFor(r => r.JoinedDate).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(JoinedDateRequired)
            .Must(IsInCodeCentury).WithErrorCode(JoinedDateOutOfRange);

        RuleFor(r => r.WorkPhone)
            .Must(IsWorkPhone).WithErrorCode(WorkPhoneInvalid)
            .When(r => r.WorkPhone is not null);

        RuleFor(r => r.RoleIds).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(RoleIdsRequired)
            .Must(HasAtMostMaxRoles).WithErrorCode(RoleIdsTooMany);
        RuleForEach(r => r.RoleIds).NotEmpty().WithErrorCode(RoleIdEmpty);
    }

    /// <summary>
    /// The failures of a staff member's full name and joined date under the rules above, the first of each: the name
    /// required, at most <see cref="NameMaxLength"/> characters after trimming, with a Latin letter to build the username
    /// from, and a username of at most <see cref="UsernameMaxLength"/> characters; the joined date in
    /// <see cref="StaffIdentityGenerator.MinYear"/>–<see cref="StaffIdentityGenerator.MaxYear"/> (R-USR-24, DEC-156 (4)).
    /// Used by the bootstrap-admin command (DEC-096, DEC-156), so both ways of creating a staff account check them alike.
    /// </summary>
    public static IReadOnlyList<ValidationFailure> IdentityFailures(string? name, DateOnly joinedDate) =>
        new StaffCreateRequestValidator()
            .Validate(new StaffCreateRequest(name, joinedDate, null, []),
                o => o.IncludeProperties(nameof(StaffCreateRequest.Name), nameof(StaffCreateRequest.JoinedDate)))
            .Errors;

    /// <summary>
    /// Also gives the years to the text of the error (<c>{From}</c>, <c>{To}</c>), as text so that they are not written
    /// with a thousands separator (2.000) in Vietnamese.
    /// </summary>
    private static bool IsInCodeCentury(StaffCreateRequest request, DateOnly joinedDate, ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter
            .AppendArgument("From", StaffIdentityGenerator.MinYear.ToString(CultureInfo.InvariantCulture))
            .AppendArgument("To", StaffIdentityGenerator.MaxYear.ToString(CultureInfo.InvariantCulture));
        return joinedDate.Year is >= StaffIdentityGenerator.MinYear and <= StaffIdentityGenerator.MaxYear;
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsMaxLength(int length, int maxLength, ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", maxLength);
        return length <= maxLength;
    }

    /// <summary>Also gives the digit counts to the text of the error (<c>{MinDigits}</c>, <c>{MaxDigits}</c>).</summary>
    private static bool IsWorkPhone(StaffCreateRequest request, string? phone, ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MinDigits", WorkPhoneMinDigits).AppendArgument("MaxDigits", WorkPhoneMaxDigits);
        return phone is not null && Regex.IsMatch(phone, WorkPhonePattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxCount}</c>).</summary>
    private static bool HasAtMostMaxRoles(StaffCreateRequest request, IReadOnlyList<Guid>? roleIds,
        ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxCount", MaxRoles);
        return roleIds is not null && roleIds.Count <= MaxRoles;
    }
}
