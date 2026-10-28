using Shouldly;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Tests.Catalog.Brands;

/// <summary>The name of a brand, as <see cref="BrandCreateRequestValidator"/> and <see cref="BrandUpdateRequestValidator"/> check it.</summary>
public class BrandRequestValidatorTests
{
    private static bool CreateIsValid(string? name) =>
        new BrandCreateRequestValidator().Validate(new BrandCreateRequest(name)).IsValid;

    private static bool UpdateIsValid(string? name) =>
        new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest(name, IsActive: true)).IsValid;

    [TestCase("ASUS")]
    [TestCase("Thương hiệu Việt")]
    [TestCase("G.Skill")]
    public void A_name_with_a_latin_letter_or_digit_is_accepted(string name)
    {
        CreateIsValid(name).ShouldBeTrue();
        UpdateIsValid(name).ShouldBeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void A_blank_name_is_refused(string? name)
    {
        CreateIsValid(name).ShouldBeFalse();
        UpdateIsValid(name).ShouldBeFalse();
    }

    [Test]
    public void A_name_is_at_most_100_characters_after_trimming()
    {
        CreateIsValid($"  {new string('a', 100)}  ").ShouldBeTrue();
        CreateIsValid(new string('a', 101)).ShouldBeFalse();
        UpdateIsValid($"  {new string('a', 100)}  ").ShouldBeTrue();
        UpdateIsValid(new string('a', 101)).ShouldBeFalse();
    }

    // The shared name check reports on the request's field, which the API returns with the pointer #/name, and gives
    // each reason its own code (DEC-117 (2)).
    [TestCase(null, "NAME_REQUIRED")]
    [TestCase("", "NAME_REQUIRED")]
    [TestCase("   ", "NAME_REQUIRED")]
    [TestCase("!!!", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    [TestCase("联想", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    public void A_refused_name_is_reported_on_the_name_with_its_code(string? name, string code)
    {
        var create = new BrandCreateRequestValidator().Validate(new BrandCreateRequest(name)).Errors.ShouldHaveSingleItem();
        create.PropertyName.ShouldBe(nameof(BrandCreateRequest.Name));
        create.ErrorCode.ShouldBe(code);
        var update = new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest(name, IsActive: true)).Errors
            .ShouldHaveSingleItem();
        update.PropertyName.ShouldBe(nameof(BrandUpdateRequest.Name));
        update.ErrorCode.ShouldBe(code);
    }

    [Test]
    public void A_name_too_long_has_its_code_and_gives_its_limit_to_the_text()
    {
        var failure = new BrandCreateRequestValidator().Validate(new BrandCreateRequest(new string('a', 101))).Errors
            .ShouldHaveSingleItem();

        failure.PropertyName.ShouldBe(nameof(BrandCreateRequest.Name));
        failure.ErrorCode.ShouldBe("NAME_TOO_LONG");
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(100);
    }

    [Test]
    public void An_update_without_is_active_is_refused()
    {
        var failure = new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest("ASUS", IsActive: null)).Errors
            .ShouldHaveSingleItem();

        failure.PropertyName.ShouldBe(nameof(BrandUpdateRequest.IsActive));
        failure.ErrorCode.ShouldBe(BrandUpdateRequestValidator.IsActiveRequired);
    }

    // Such a name has no slug: refused as input (400), never left to fail in the entity (500).
    [TestCase("!!!")]
    [TestCase("联想")]
    [TestCase("Ø")]
    public void A_name_without_a_latin_letter_or_digit_is_refused(string name)
    {
        CreateIsValid(name).ShouldBeFalse();
        UpdateIsValid(name).ShouldBeFalse();
    }
}
