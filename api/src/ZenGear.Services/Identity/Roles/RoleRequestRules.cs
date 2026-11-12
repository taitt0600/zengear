using FluentValidation;

namespace ZenGear.Services.Identity.Roles;

/// <summary>
/// The input checks shared by creating and updating a role, each with its own code (DEC-117 (2)); the API writes their
/// texts. The data checks (unknown key or role, a taken name, the rules of a system or default role) are in the service.
/// </summary>
public static class RoleRequestRules
{
    public const int NameMaxLength = 100;

    public const string NameRequired = "NAME_REQUIRED";

    public const string NameTooLong = "NAME_TOO_LONG";

    public const string PermissionKeysRequired = "PERMISSION_KEYS_REQUIRED";

    public const string MaxDiscountPercentOutOfRange = "MAX_DISCOUNT_PERCENT_OUT_OF_RANGE";

    public const string MaxDiscountPercentTooPrecise = "MAX_DISCOUNT_PERCENT_TOO_PRECISE";

    /// <summary>Required, at most <see cref="NameMaxLength"/> characters once trimmed.</summary>
    public static void RoleName<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(NameRequired)
            .Must((_, name, context) => FitsNameMaxLength(name, context)).WithErrorCode(NameTooLong);

    /// <summary>Null, or 0–100 with at most two decimals: <c>numeric(5,2)</c> and <c>ck_roles_max_discount_percent</c>.</summary>
    public static void MaxDiscountPercent<T>(this IRuleBuilderInitial<T, decimal?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(d => d is null or (>= 0 and <= 100)).WithErrorCode(MaxDiscountPercentOutOfRange)
            .Must(d => d is null || decimal.Round(d.Value, 2) == d.Value).WithErrorCode(MaxDiscountPercentTooPrecise);

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsNameMaxLength<T>(string? name, ValidationContext<T> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", NameMaxLength);
        return name!.Trim().Length <= NameMaxLength;
    }
}
