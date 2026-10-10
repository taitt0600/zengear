using System.Text.RegularExpressions;
using FluentValidation;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Input of <see cref="StaffCreateRequest"/> (400). An empty role list is not an input error but R-SEC-01 (409), checked
/// by <see cref="StaffService"/>.
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

    public const int MaxRoles = 20;

    public StaffCreateRequestValidator()
    {
        RuleFor(r => r.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(e => e.Trim().Length <= EmailMaxLength).WithMessage($"Email must be at most {EmailMaxLength} characters.")
            .Must(e => IsEmail(e.Trim()))
            .WithMessage("Email is not a valid email address.");

        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n.Trim().Length <= NameMaxLength).WithMessage($"Name must be at most {NameMaxLength} characters.");

        RuleFor(r => r.EmployeeCode).Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Employee code is required.")
            .Must(c => c.Trim() == c).WithMessage("Employee code must not start or end with a space.")
            .MaximumLength(EmployeeCodeMaxLength);

        RuleFor(r => r.JoinedDate).NotEmpty();

        RuleFor(r => r.WorkPhone!)
            .Matches(WorkPhonePattern).WithMessage("Work phone must be 8 to 15 digits, optionally starting with +.")
            .When(r => r.WorkPhone is not null);

        RuleFor(r => r.RoleIds).Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(ids => ids.Count <= MaxRoles).WithMessage($"At most {MaxRoles} roles.");
        RuleForEach(r => r.RoleIds).NotEmpty();
    }

    /// <summary>Also used for the email of the bootstrap-admin command (DEC-096).</summary>
    public static bool IsEmail(string email) =>
        email.Length <= EmailMaxLength && Regex.IsMatch(email, EmailPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
}
