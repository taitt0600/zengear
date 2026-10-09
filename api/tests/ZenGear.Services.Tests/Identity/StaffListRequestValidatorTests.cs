using FluentValidation.TestHelper;
using Shouldly;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Models;
using ZenGear.Services.Identity.Validators;

namespace ZenGear.Services.Tests.Identity;

/// <summary>The parameters of the staff list (R-USR-13, DEC-105): every refusal names the rule.</summary>
public class StaffListRequestValidatorTests
{
    private readonly StaffListRequestValidator _validator = new();

    private static StaffListRequest Request(string? status = null, string? sort = null, int page = 1,
        int pageSize = PageRequest.DefaultPageSize) =>
        new(status, null, sort, page, pageSize);

    [Test]
    public void R_USR_13_no_parameter_passes() =>
        _validator.TestValidate(Request()).ShouldNotHaveAnyValidationErrors();

    [TestCase("ACTIVE")]
    [TestCase("DISABLED")]
    public void R_USR_13_accepts_ACTIVE_and_DISABLED(string status) =>
        _validator.TestValidate(Request(status: status)).ShouldNotHaveAnyValidationErrors();

    [TestCase("name")]
    [TestCase("-name")]
    [TestCase("employeeCode")]
    [TestCase("-employeeCode")]
    [TestCase("joinedDate")]
    [TestCase("-joinedDate")]
    public void R_USR_13_accepts_every_documented_sort(string sort) =>
        _validator.TestValidate(Request(sort: sort)).ShouldNotHaveAnyValidationErrors();

    [TestCase(1, 1)]
    [TestCase(1, PageRequest.MaxPageSize)]
    [TestCase(PageRequest.MaxPage, PageRequest.MaxPageSize)]
    public void R_USR_13_accepts_pages_within_bounds(int page, int pageSize) =>
        _validator.TestValidate(Request(page: page, pageSize: pageSize)).ShouldNotHaveAnyValidationErrors();

    [TestCase("ANONYMIZED")]
    [TestCase("active")]
    [TestCase("PENDING")]
    [TestCase("1")]
    public void R_USR_13_refuses_a_status_other_than_ACTIVE_or_DISABLED(string status) =>
        _validator.TestValidate(Request(status: status)).ShouldHaveValidationErrorFor(r => r.Status)
            .WithErrorCode(StaffListRequestValidator.RuleId);

    [TestCase("email")]
    [TestCase("phone")]
    [TestCase("createdAt")]
    [TestCase("Name")]
    [TestCase("--name")]
    [TestCase("name,employeeCode")]
    public void R_USR_13_refuses_an_undocumented_sort(string sort) =>
        _validator.TestValidate(Request(sort: sort)).ShouldHaveValidationErrorFor(r => r.Sort)
            .WithErrorCode(StaffListRequestValidator.RuleId);

    [TestCase(0)]
    [TestCase(PageRequest.MaxPage + 1)]
    public void R_USR_13_refuses_a_page_out_of_range(int page) =>
        _validator.TestValidate(Request(page: page)).ShouldHaveValidationErrorFor(r => r.Page)
            .WithErrorCode(StaffListRequestValidator.RuleId);

    [TestCase(0)]
    [TestCase(PageRequest.MaxPageSize + 1)]
    public void R_USR_13_refuses_a_page_size_outside_1_to_100(int pageSize) =>
        _validator.TestValidate(Request(pageSize: pageSize)).ShouldHaveValidationErrorFor(r => r.PageSize)
            .WithErrorCode(StaffListRequestValidator.RuleId);

    [Test]
    public void R_USR_13_messages_never_repeat_the_value_sent()
    {
        var result = _validator.TestValidate(Request(status: "secret-status", sort: "secret-sort"));

        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldAllBe(e => !e.ErrorMessage.Contains("secret"));
    }
}
