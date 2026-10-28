using FluentValidation.Results;
using Shouldly;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

public class StaffCreateRequestValidatorTests
{
    private static readonly StaffCreateRequestValidator Validator = new();

    private static readonly StaffCreateRequest Valid =
        new("an@zengear.test", "Nguyễn Văn An", "NV0012", new DateOnly(2026, 10, 1), "0901234567", [Guid.NewGuid()]);

    [Test]
    public void A_complete_request_is_valid()
    {
        Validator.Validate(Valid).IsValid.ShouldBeTrue();
        Validator.Validate(Valid with { WorkPhone = null, Email = " An@ZenGear.test " }).IsValid.ShouldBeTrue();
    }

    [Test]
    public void An_empty_role_list_is_left_to_R_SEC_01()
    {
        Validator.Validate(Valid with { RoleIds = [] }).IsValid.ShouldBeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void An_email_is_required(string? email)
    {
        ShouldFailOn(Valid with { Email = email }, "Email", StaffCreateRequestValidator.EmailRequired);
    }

    [TestCase("not-an-email")]
    [TestCase("a@localhost")]
    [TestCase("a b@zengear.test")]
    [TestCase("a@@zengear.test")]
    public void An_email_firebase_refuses_is_invalid(string email)
    {
        ShouldFailOn(Valid with { Email = email }, "Email", StaffCreateRequestValidator.EmailInvalid);
    }

    [Test]
    public void An_email_longer_than_the_column_is_invalid()
    {
        var failure = ShouldFailOn(Valid with { Email = new string('a', 250) + "@zg.vn" }, "Email", StaffCreateRequestValidator.EmailTooLong);
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(StaffCreateRequestValidator.EmailMaxLength);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void A_name_is_required(string? name)
    {
        ShouldFailOn(Valid with { Name = name }, "Name", StaffCreateRequestValidator.NameRequired);
    }

    [Test]
    public void A_name_of_more_than_100_characters_is_invalid()
    {
        var failure = ShouldFailOn(Valid with { Name = new string('a', 101) }, "Name", StaffCreateRequestValidator.NameTooLong);
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(StaffCreateRequestValidator.NameMaxLength);
    }

    [TestCase(null, StaffCreateRequestValidator.EmployeeCodeRequired)]
    [TestCase("", StaffCreateRequestValidator.EmployeeCodeRequired)]
    [TestCase(" NV0012", StaffCreateRequestValidator.EmployeeCodeUntrimmed)]
    [TestCase("NV0012 ", StaffCreateRequestValidator.EmployeeCodeUntrimmed)]
    public void An_employee_code_is_required_without_surrounding_spaces(string? code, string errorCode)
    {
        ShouldFailOn(Valid with { EmployeeCode = code }, "EmployeeCode", errorCode);
    }

    [Test]
    public void An_employee_code_is_at_most_50_characters()
    {
        var failure = ShouldFailOn(Valid with { EmployeeCode = new string('N', 51) }, "EmployeeCode",
            StaffCreateRequestValidator.EmployeeCodeTooLong);
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(StaffCreateRequestValidator.EmployeeCodeMaxLength);
    }

    [Test]
    public void A_joined_date_is_required()
    {
        ShouldFailOn(Valid with { JoinedDate = default }, "JoinedDate", StaffCreateRequestValidator.JoinedDateRequired);
    }

    [TestCase("090-123-4567")]
    [TestCase("1234567")]
    [TestCase("+8490123456789012")]
    public void A_work_phone_is_digits_only(string phone)
    {
        var failure = ShouldFailOn(Valid with { WorkPhone = phone }, "WorkPhone", StaffCreateRequestValidator.WorkPhoneInvalid);
        failure.FormattedMessagePlaceholderValues["MinDigits"].ShouldBe(StaffCreateRequestValidator.WorkPhoneMinDigits);
        failure.FormattedMessagePlaceholderValues["MaxDigits"].ShouldBe(StaffCreateRequestValidator.WorkPhoneMaxDigits);
    }

    [Test]
    public void At_most_20_roles_and_no_empty_id()
    {
        var failure = ShouldFailOn(Valid with { RoleIds = [.. Enumerable.Range(0, 21).Select(_ => Guid.NewGuid())] }, "RoleIds",
            StaffCreateRequestValidator.RoleIdsTooMany);
        failure.FormattedMessagePlaceholderValues["MaxCount"].ShouldBe(StaffCreateRequestValidator.MaxRoles);
        ShouldFailOn(Valid with { RoleIds = [Guid.Empty] }, "RoleIds[0]", StaffCreateRequestValidator.RoleIdEmpty);
    }

    [Test]
    public void A_missing_role_list_is_required()
    {
        ShouldFailOn(Valid with { RoleIds = null }, "RoleIds", StaffCreateRequestValidator.RoleIdsRequired);
    }

    /// <summary>The request fails on that property only, with that code.</summary>
    private static ValidationFailure ShouldFailOn(StaffCreateRequest request, string property, string code)
    {
        var failure = Validator.Validate(request).Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe(property);
        failure.ErrorCode.ShouldBe(code);
        return failure;
    }
}
