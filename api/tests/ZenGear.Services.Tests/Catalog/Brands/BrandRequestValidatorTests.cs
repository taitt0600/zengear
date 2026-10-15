using Shouldly;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Tests.Catalog.Brands;

/// <summary>The name of a brand, as <see cref="BrandCreateRequestValidator"/> and <see cref="BrandUpdateRequestValidator"/> check it.</summary>
public class BrandRequestValidatorTests
{
    private static bool CreateIsValid(string? name) =>
        new BrandCreateRequestValidator().Validate(new BrandCreateRequest(name!)).IsValid;

    private static bool UpdateIsValid(string? name) =>
        new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest(name!, IsActive: true)).IsValid;

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

    // The shared name check reports on the request's field, which the API returns as errors.name.
    [TestCase(null)]
    [TestCase("   ")]
    [TestCase("!!!")]
    public void A_refused_name_is_reported_on_the_name(string? name)
    {
        new BrandCreateRequestValidator().Validate(new BrandCreateRequest(name!)).Errors
            .ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(BrandCreateRequest.Name));
        new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest(name!, IsActive: true)).Errors
            .ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(BrandUpdateRequest.Name));
    }

    [Test]
    public void An_update_without_is_active_is_refused() =>
        new BrandUpdateRequestValidator().Validate(new BrandUpdateRequest("ASUS", IsActive: null)).IsValid.ShouldBeFalse();

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
