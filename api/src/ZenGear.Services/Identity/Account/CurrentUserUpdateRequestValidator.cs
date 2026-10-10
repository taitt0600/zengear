using FluentValidation;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Identity.Account;

/// <summary>Input of <see cref="CurrentUserUpdateRequest"/> (400): the display name (R-USR-04).</summary>
public sealed class CurrentUserUpdateRequestValidator : AbstractValidator<CurrentUserUpdateRequest>
{
    public CurrentUserUpdateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n!.Trim().Length <= StaffCreateRequestValidator.NameMaxLength)
            .WithMessage($"Name must be at most {StaffCreateRequestValidator.NameMaxLength} characters.");
    }
}
