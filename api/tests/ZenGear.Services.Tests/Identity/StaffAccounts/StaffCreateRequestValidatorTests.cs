using FluentValidation.Results;
using Shouldly;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

public class StaffCreateRequestValidatorTests
{
    private static readonly StaffCreateRequestValidator Validator = new();

    private static readonly StaffCreateRequest Valid =
        new("Nguyễn Văn An", new DateOnly(2026, 10, 1), "0901234567", [Guid.NewGuid()]);

    [Test]
    public void A_complete_request_is_valid()
    {
        Validator.Validate(Valid).IsValid.ShouldBeTrue();
        Validator.Validate(Valid with { WorkPhone = null }).IsValid.ShouldBeTrue();
    }

    [Test]
    public void An_empty_role_list_is_left_to_R_SEC_01()
    {
        Validator.Validate(Valid with { RoleIds = [] }).IsValid.ShouldBeTrue();
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

    [TestCase("12345")]
    [TestCase("--- ...")]
    [TestCase("李小龍")]
    public void R_USR_24_name_without_a_letter_is_refused(string name)
    {
        ShouldFailOn(Valid with { Name = name }, "Name", StaffCreateRequestValidator.NameNoLetter);
    }

    [Test]
    public void R_USR_24_username_over_64_is_refused()
    {
        // Given name of 55 letters + initial N + 8 of the code = 64 is the limit; one letter more is refused.
        var atLimit = "Nguyễn " + new string('a', 55);
        Validator.Validate(Valid with { Name = atLimit }).IsValid.ShouldBeTrue();

        var failure = ShouldFailOn(Valid with { Name = atLimit + "a" }, "Name", StaffCreateRequestValidator.NameUsernameTooLong);
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(StaffCreateRequestValidator.UsernameMaxLength);
    }

    [Test]
    public void A_joined_date_is_required()
    {
        ShouldFailOn(Valid with { JoinedDate = default }, "JoinedDate", StaffCreateRequestValidator.JoinedDateRequired);
    }

    [TestCase(2000, 1, 1)]
    [TestCase(2099, 12, 31)]
    public void R_USR_24_a_joined_year_within_2000_to_2099_is_valid(int year, int month, int day)
    {
        Validator.Validate(Valid with { JoinedDate = new DateOnly(year, month, day) }).IsValid.ShouldBeTrue();
    }

    [TestCase(1999, 12, 31)]
    [TestCase(1926, 3, 2)]
    [TestCase(2100, 1, 1)]
    [TestCase(2126, 3, 2)]
    public void R_USR_24_a_joined_year_outside_2000_to_2099_is_refused(int year, int month, int day)
    {
        // DEC-156 (4): the code keeps 2 digits of the year, so a year 100 years off would take the codes of the real one.
        var failure = ShouldFailOn(Valid with { JoinedDate = new DateOnly(year, month, day) }, "JoinedDate",
            StaffCreateRequestValidator.JoinedDateOutOfRange);
        failure.FormattedMessagePlaceholderValues["From"].ShouldBe("2000");
        failure.FormattedMessagePlaceholderValues["To"].ShouldBe("2099");
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
