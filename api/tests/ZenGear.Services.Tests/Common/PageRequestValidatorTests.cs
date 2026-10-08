using Shouldly;
using FluentValidation.TestHelper;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Common.Validators;

namespace ZenGear.Services.Tests.Common;

public class PageRequestValidatorTests
{
    private readonly PageRequestValidator _validator = new();

    [Test]
    public void Defaults_pass() => _validator.TestValidate(new PageRequest()).ShouldNotHaveAnyValidationErrors();

    [TestCase(0)]
    [TestCase(PageRequest.MaxPage + 1)]
    public void Page_outside_1_to_max_is_refused(int page) =>
        _validator.TestValidate(new PageRequest(page, PageRequest.MaxPageSize)).ShouldHaveValidationErrorFor(r => r.Page);

    [Test]
    public void The_last_page_skips_within_int() =>
        new PageRequest(PageRequest.MaxPage, PageRequest.MaxPageSize).Skip.ShouldBeGreaterThan(0);

    [TestCase(0)]
    [TestCase(PageRequest.MaxPageSize + 1)]
    public void Page_size_outside_1_to_100_is_refused(int pageSize) =>
        _validator.TestValidate(new PageRequest(1, pageSize)).ShouldHaveValidationErrorFor(r => r.PageSize);
}
