using Shouldly;
using ZenGear.Services.Identity.Models;
using ZenGear.Services.Identity.Validators;

namespace ZenGear.Services.Tests.Identity;

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

    [TestCase("")]
    [TestCase("not-an-email")]
    [TestCase("a@localhost")]
    [TestCase("a b@zengear.test")]
    [TestCase("a@@zengear.test")]
    public void An_email_firebase_refuses_is_invalid(string email)
    {
        ShouldFailOn(Valid with { Email = email }, "Email");
    }

    [Test]
    public void An_email_longer_than_the_column_is_invalid()
    {
        ShouldFailOn(Valid with { Email = new string('a', 250) + "@zg.vn" }, "Email");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void A_name_is_required(string name)
    {
        ShouldFailOn(Valid with { Name = name }, "Name");
    }

    [Test]
    public void A_name_of_more_than_100_characters_is_invalid()
    {
        ShouldFailOn(Valid with { Name = new string('a', 101) }, "Name");
    }

    [TestCase("")]
    [TestCase(" NV0012")]
    public void An_employee_code_is_required_without_surrounding_spaces(string code)
    {
        ShouldFailOn(Valid with { EmployeeCode = code }, "EmployeeCode");
    }

    [Test]
    public void A_joined_date_is_required()
    {
        ShouldFailOn(Valid with { JoinedDate = default }, "JoinedDate");
    }

    [TestCase("090-123-4567")]
    [TestCase("1234567")]
    [TestCase("+8490123456789012")]
    public void A_work_phone_is_digits_only(string phone)
    {
        ShouldFailOn(Valid with { WorkPhone = phone }, "WorkPhone");
    }

    [Test]
    public void At_most_20_roles_and_no_empty_id()
    {
        ShouldFailOn(Valid with { RoleIds = [.. Enumerable.Range(0, 21).Select(_ => Guid.NewGuid())] }, "RoleIds");
        ShouldFailOn(Valid with { RoleIds = [Guid.Empty] }, "RoleIds[0]");
    }

    private static void ShouldFailOn(StaffCreateRequest request, string property)
    {
        var result = Validator.Validate(request);
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(property);
    }
}
