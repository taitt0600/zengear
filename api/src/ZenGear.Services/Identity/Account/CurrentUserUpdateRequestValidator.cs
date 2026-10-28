using FluentValidation;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Identity.Account;

/// <summary>Input of <see cref="CurrentUserUpdateRequest"/> (400): the display name (R-USR-04).</summary>
public sealed class CurrentUserUpdateRequestValidator : AbstractValidator<CurrentUserUpdateRequest>
{
    public CurrentUserUpdateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(StaffCreateRequestValidator.NameRequired)
            .Must(FitsNameMaxLength).WithErrorCode(StaffCreateRequestValidator.NameTooLong);
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsNameMaxLength(CurrentUserUpdateRequest request, string? name, ValidationContext<CurrentUserUpdateRequest> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", StaffCreateRequestValidator.NameMaxLength);
        return name!.Trim().Length <= StaffCreateRequestValidator.NameMaxLength;
    }
}
