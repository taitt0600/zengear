using System.Text.RegularExpressions;
using FluentValidation;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Input of <see cref="StaffCreateRequest"/> (400), one code per check (DEC-117 (2)). An empty role list is not an input
/// error but R-SEC-01 (409), checked by <see cref="StaffService"/>.
/// </summary>
public sealed class StaffCreateRequestValidator : AbstractValidator<StaffCreateRequest>
{
    /// <summary>Stricter than FluentValidation's EmailAddress: Firebase refuses addresses without a dotted domain.</summary>
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    /// <summary><c>users.email</c> is varchar(255).</summary>
    public const int EmailMaxLength = 255;

    public const int NameMaxLength = 100;

    public const int EmployeeCodeMaxLength = 50;

    /// <summary>Digits with an optional leading +; fits <c>staff_profiles.work_phone</c> varchar(20).</summary>
    public const string WorkPhonePattern = @"^\+?[0-9]{8,15}$";

    /// <summary>The digit counts of <see cref="WorkPhonePattern"/>, for the text of its error.</summary>
    public const int WorkPhoneMinDigits = 8;

    public const int WorkPhoneMaxDigits = 15;

    public const int MaxRoles = 20;

    public const string EmailRequired = "EMAIL_REQUIRED";

    public const string EmailTooLong = "EMAIL_TOO_LONG";

    public const string EmailInvalid = "EMAIL_INVALID";

    public const string NameRequired = "NAME_REQUIRED";

    public const string NameTooLong = "NAME_TOO_LONG";

    public const string EmployeeCodeRequired = "EMPLOYEE_CODE_REQUIRED";

    public const string EmployeeCodeUntrimmed = "EMPLOYEE_CODE_UNTRIMMED";

    public const string EmployeeCodeTooLong = "EMPLOYEE_CODE_TOO_LONG";

    public const string JoinedDateRequired = "JOINED_DATE_REQUIRED";

    public const string WorkPhoneInvalid = "WORK_PHONE_INVALID";

    public const string RoleIdsRequired = "ROLE_IDS_REQUIRED";

    public const string RoleIdsTooMany = "ROLE_IDS_TOO_MANY";

    public const string RoleIdEmpty = "ROLE_ID_EMPTY";

    public StaffCreateRequestValidator()
    {
        RuleFor(r => r.Email).Cascade(CascadeMode.Stop)
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithErrorCode(EmailRequired)
            .Must((_, e, context) => FitsMaxLength(e!, EmailMaxLength, context)).WithErrorCode(EmailTooLong)
            .Must(e => IsEmail(e!.Trim())).WithErrorCode(EmailInvalid);

        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(NameRequired)
            .Must((_, n, context) => FitsMaxLength(n!, NameMaxLength, context)).WithErrorCode(NameTooLong);

        RuleFor(r => r.EmployeeCode).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithErrorCode(EmployeeCodeRequired)
            .Must(c => c!.Trim() == c).WithErrorCode(EmployeeCodeUntrimmed)
            .MaximumLength(EmployeeCodeMaxLength).WithErrorCode(EmployeeCodeTooLong);

        RuleFor(r => r.JoinedDate).NotEmpty().WithErrorCode(JoinedDateRequired);

        RuleFor(r => r.WorkPhone)
            .Must(IsWorkPhone).WithErrorCode(WorkPhoneInvalid)
            .When(r => r.WorkPhone is not null);

        RuleFor(r => r.RoleIds).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(RoleIdsRequired)
            .Must(HasAtMostMaxRoles).WithErrorCode(RoleIdsTooMany);
        RuleForEach(r => r.RoleIds).NotEmpty().WithErrorCode(RoleIdEmpty);
    }

    /// <summary>Also used for the email of the bootstrap-admin command (DEC-096).</summary>
    public static bool IsEmail(string email) =>
        email.Length <= EmailMaxLength && Regex.IsMatch(email, EmailPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsMaxLength(string value, int maxLength, ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", maxLength);
        return value.Trim().Length <= maxLength;
    }

    /// <summary>Also gives the digit counts to the text of the error (<c>{MinDigits}</c>, <c>{MaxDigits}</c>).</summary>
    private static bool IsWorkPhone(StaffCreateRequest request, string? phone, ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MinDigits", WorkPhoneMinDigits).AppendArgument("MaxDigits", WorkPhoneMaxDigits);
        return Regex.IsMatch(phone!, WorkPhonePattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxCount}</c>).</summary>
    private static bool HasAtMostMaxRoles(StaffCreateRequest request, IReadOnlyList<Guid>? roleIds,
        ValidationContext<StaffCreateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxCount", MaxRoles);
        return roleIds!.Count <= MaxRoles;
    }
}
