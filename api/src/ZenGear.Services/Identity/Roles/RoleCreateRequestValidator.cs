using FluentValidation;

namespace ZenGear.Services.Identity.Roles;

/// <summary>The input of creating a role (UC-002); unknown keys and a taken name are checked by the service.</summary>
public sealed class RoleCreateRequestValidator : AbstractValidator<RoleCreateRequest>
{
    public RoleCreateRequestValidator()
    {
        RuleFor(r => r.Name).RoleName();
        RuleFor(r => r.PermissionKeys).NotNull().WithErrorCode(RoleRequestRules.PermissionKeysRequired);
        RuleFor(r => r.MaxDiscountPercent).MaxDiscountPercent();
    }
}
