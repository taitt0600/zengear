using FluentValidation;
using ZenGear.Data.Externals.Auth;

namespace ZenGear.Services.Identity.Account;

/// <summary>
/// The staff password policy R-USR-11 (DEC-102), checked before any call to Firebase. The value is used as typed: no
/// trimming, no Unicode normalisation (Firebase compares exactly what is typed at sign-in), and no rule on upper case,
/// digits or symbols. Each check has its own code (DEC-117 (2)) and carries the rule <see cref="RuleId"/> as its state,
/// so the 400 names the rule; the value is never written back.
/// </summary>
public sealed class CurrentUserPasswordChangeRequestValidator : AbstractValidator<CurrentUserPasswordChangeRequest>
{
    public const string RuleId = "R-USR-11";

    /// <summary>In Unicode code points, not UTF-16 units.</summary>
    public const int MinLength = 12;

    public const int MaxLength = 128;

    /// <summary>Any letter case.</summary>
    public const string ForbiddenWord = "zengear";

    public const string PasswordRequired = "PASSWORD_REQUIRED";

    public const string PasswordLengthOutOfRange = "PASSWORD_LENGTH_OUT_OF_RANGE";

    public const string PasswordIsDefault = "PASSWORD_IS_DEFAULT";

    public const string PasswordTooCommon = "PASSWORD_TOO_COMMON";

    public const string PasswordContainsZenGear = "PASSWORD_CONTAINS_ZENGEAR";

    public CurrentUserPasswordChangeRequestValidator(IAuthProvider auth)
    {
        ArgumentNullException.ThrowIfNull(auth);

        // WithErrorCode and WithState apply to the check just before them, so each check carries both.
        RuleFor(r => r.NewPassword).Cascade(CascadeMode.Stop)
            // NotNull, not NotEmpty: NotEmpty treats spaces as empty, and spaces count here.
            .NotNull().WithErrorCode(PasswordRequired).WithState(_ => RuleId)
            .Must(HasAllowedLength).WithErrorCode(PasswordLengthOutOfRange).WithState(_ => RuleId)
            .Must(p => !auth.IsDefaultStaffPassword(p!)).WithErrorCode(PasswordIsDefault).WithState(_ => RuleId)
            .Must(p => !StaffPasswordBlocklist.Contains(p!)).WithErrorCode(PasswordTooCommon).WithState(_ => RuleId)
            .Must(p => !p!.Contains(ForbiddenWord, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode(PasswordContainsZenGear).WithState(_ => RuleId);
    }

    /// <summary>Also gives the limits to the text of the error (<c>{MinLength}</c>, <c>{MaxLength}</c>).</summary>
    private static bool HasAllowedLength(CurrentUserPasswordChangeRequest request, string? password,
        ValidationContext<CurrentUserPasswordChangeRequest> context)
    {
        context.MessageFormatter.AppendArgument("MinLength", MinLength).AppendArgument("MaxLength", MaxLength);
        return CodePoints(password!) is >= MinLength and <= MaxLength;
    }

    private static int CodePoints(string value) => value.EnumerateRunes().Count();
}
