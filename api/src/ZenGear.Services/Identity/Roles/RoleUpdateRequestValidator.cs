using FluentValidation;

namespace ZenGear.Services.Identity.Roles;

/// <summary>The input of updating a role (UC-003); unknown keys and roles and the rules of the role are checked by the service.</summary>
public sealed class RoleUpdateRequestValidator : AbstractValidator<RoleUpdateRequest>
{
    public const string ManagedRoleIdsRequired = "MANAGED_ROLE_IDS_REQUIRED";

    public RoleUpdateRequestValidator()
    {
        RuleFor(r => r.Name).RoleName();
        RuleFor(r => r.PermissionKeys).NotNull().WithErrorCode(RoleRequestRules.PermissionKeysRequired);
        RuleFor(r => r.MaxDiscountPercent).MaxDiscountPercent();
        RuleFor(r => r.ManagedRoleIds).NotNull().WithErrorCode(ManagedRoleIdsRequired);
    }
}
