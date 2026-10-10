using FluentValidation;
using ZenGear.Data.Externals.Auth;

namespace ZenGear.Services.Identity.Account;

/// <summary>
/// The staff password policy R-USR-11 (DEC-102), checked before any call to Firebase. The value is used as typed: no
/// trimming, no Unicode normalisation (Firebase compares exactly what is typed at sign-in), and no rule on upper case,
/// digits or symbols. Every failure carries the error code <see cref="RuleId"/>, so the 400 names the rule; no message
/// quotes the value.
/// </summary>
public sealed class CurrentUserPasswordChangeRequestValidator : AbstractValidator<CurrentUserPasswordChangeRequest>
{
    public const string RuleId = "R-USR-11";

    /// <summary>In Unicode code points, not UTF-16 units.</summary>
    public const int MinLength = 12;

    public const int MaxLength = 128;

    /// <summary>Any letter case.</summary>
    public const string ForbiddenWord = "zengear";

    public CurrentUserPasswordChangeRequestValidator(IAuthProvider auth)
    {
        ArgumentNullException.ThrowIfNull(auth);

        // WithErrorCode applies to the check just before it, so each check carries it.
        RuleFor(r => r.NewPassword).Cascade(CascadeMode.Stop)
            // NotNull, not NotEmpty: NotEmpty treats spaces as empty, and spaces count here.
            .NotNull().WithMessage("New password is required.").WithErrorCode(RuleId)
            .Must(p => CodePoints(p!) is >= MinLength and <= MaxLength)
            .WithMessage($"New password must be {MinLength} to {MaxLength} characters.").WithErrorCode(RuleId)
            .Must(p => !auth.IsDefaultStaffPassword(p!))
            .WithMessage("New password must not be the default staff password.").WithErrorCode(RuleId)
            .Must(p => !StaffPasswordBlocklist.Contains(p!))
            .WithMessage("New password is too common.").WithErrorCode(RuleId)
            .Must(p => !p!.Contains(ForbiddenWord, StringComparison.OrdinalIgnoreCase))
            .WithMessage("New password must not contain the word ZenGear.").WithErrorCode(RuleId);
    }

    private static int CodePoints(string value) => value.EnumerateRunes().Count();
}
