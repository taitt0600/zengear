using FluentValidation.Results;
using Shouldly;
using ZenGear.Services.Identity.Roles;

namespace ZenGear.Services.Tests.Identity.Roles;

/// <summary>The input checks of creating and updating a role (UC-002, UC-003); the data checks are in the service.</summary>
public class RoleRequestValidatorTests
{
    private static readonly RoleCreateRequestValidator Create = new();
    private static readonly RoleUpdateRequestValidator Update = new();

    private static readonly RoleCreateRequest ValidCreate = new("Night Shift", ["brand.read"], 5.25m);
    private static readonly RoleUpdateRequest ValidUpdate = new("Night Shift", [], null, []);

    [Test]
    public void Complete_requests_are_valid()
    {
        Create.Validate(ValidCreate).IsValid.ShouldBeTrue();
        Create.Validate(ValidCreate with { MaxDiscountPercent = null, PermissionKeys = [] }).IsValid.ShouldBeTrue();
        Create.Validate(ValidCreate with { MaxDiscountPercent = 100m }).IsValid.ShouldBeTrue();
        Create.Validate(ValidCreate with { MaxDiscountPercent = 0m }).IsValid.ShouldBeTrue();
        Update.Validate(ValidUpdate).IsValid.ShouldBeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void A_name_is_required(string? name)
    {
        ShouldFailOn(Create.Validate(ValidCreate with { Name = name }), "Name", RoleRequestRules.NameRequired);
        ShouldFailOn(Update.Validate(ValidUpdate with { Name = name }), "Name", RoleRequestRules.NameRequired);
    }

    [Test]
    public void A_name_longer_than_100_is_refused()
    {
        Create.Validate(ValidCreate with { Name = new string('a', 100) }).IsValid.ShouldBeTrue();
        var failure = ShouldFailOn(Create.Validate(ValidCreate with { Name = new string('a', 101) }), "Name",
            RoleRequestRules.NameTooLong);
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(RoleRequestRules.NameMaxLength);
    }

    [TestCase("-0.01")]
    [TestCase("100.01")]
    public void A_discount_outside_0_to_100_is_refused(string value)
    {
        ShouldFailOn(Create.Validate(ValidCreate with { MaxDiscountPercent = Parse(value) }), "MaxDiscountPercent",
            RoleRequestRules.MaxDiscountPercentOutOfRange);
        ShouldFailOn(Update.Validate(ValidUpdate with { MaxDiscountPercent = Parse(value) }), "MaxDiscountPercent",
            RoleRequestRules.MaxDiscountPercentOutOfRange);
    }

    [Test]
    public void A_discount_with_more_than_two_decimals_is_refused()
    {
        ShouldFailOn(Create.Validate(ValidCreate with { MaxDiscountPercent = 5.555m }), "MaxDiscountPercent",
            RoleRequestRules.MaxDiscountPercentTooPrecise);
    }

    [Test]
    public void The_lists_are_required()
    {
        ShouldFailOn(Create.Validate(ValidCreate with { PermissionKeys = null }), "PermissionKeys",
            RoleRequestRules.PermissionKeysRequired);
        ShouldFailOn(Update.Validate(ValidUpdate with { PermissionKeys = null }), "PermissionKeys",
            RoleRequestRules.PermissionKeysRequired);
        ShouldFailOn(Update.Validate(ValidUpdate with { ManagedRoleIds = null }), "ManagedRoleIds",
            RoleUpdateRequestValidator.ManagedRoleIdsRequired);
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static ValidationFailure ShouldFailOn(ValidationResult result, string property, string code)
    {
        var failure = result.Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe(property);
        failure.ErrorCode.ShouldBe(code);
        return failure;
    }
}
