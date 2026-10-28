using FluentValidation.Results;
using Shouldly;
using ZenGear.Services.Catalog.Series;

namespace ZenGear.Services.Tests.Catalog.Series;

/// <summary>The input of a series, as <see cref="ProductSeriesCreateRequestValidator"/> and <see cref="ProductSeriesUpdateRequestValidator"/> check it.</summary>
public class ProductSeriesRequestValidatorTests
{
    private static readonly Guid Asus = Guid.CreateVersion7();

    private static IEnumerable<string> CreateErrors(string? name, Guid? brandId) =>
        CreateFailures(name, brandId).Select(e => e.PropertyName);

    private static IEnumerable<string> UpdateErrors(string? name, Guid? brandId, bool? isActive = true) =>
        UpdateFailures(name, brandId, isActive).Select(e => e.PropertyName);

    private static IEnumerable<string> CreateCodes(string? name, Guid? brandId) =>
        CreateFailures(name, brandId).Select(e => e.ErrorCode);

    private static IEnumerable<string> UpdateCodes(string? name, Guid? brandId, bool? isActive = true) =>
        UpdateFailures(name, brandId, isActive).Select(e => e.ErrorCode);

    private static List<ValidationFailure> CreateFailures(string? name, Guid? brandId) =>
        new ProductSeriesCreateRequestValidator().Validate(new ProductSeriesCreateRequest(name, brandId)).Errors;

    private static List<ValidationFailure> UpdateFailures(string? name, Guid? brandId, bool? isActive) =>
        new ProductSeriesUpdateRequestValidator().Validate(new ProductSeriesUpdateRequest(name, brandId, isActive)).Errors;

    [TestCase("ROG Strix")]
    [TestCase("Dòng Việt")]
    [TestCase("990 PRO")]
    public void A_name_with_a_latin_letter_or_digit_and_a_brand_is_accepted(string name)
    {
        CreateErrors(name, Asus).ShouldBeEmpty();
        UpdateErrors(name, Asus).ShouldBeEmpty();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("!!!")]
    [TestCase("联想")]
    public void A_blank_name_or_one_without_a_slug_is_refused_on_the_name(string? name)
    {
        CreateErrors(name, Asus).ShouldBe([nameof(ProductSeriesCreateRequest.Name)]);
        UpdateErrors(name, Asus).ShouldBe([nameof(ProductSeriesUpdateRequest.Name)]);
    }

    [TestCase(null, "NAME_REQUIRED")]
    [TestCase("   ", "NAME_REQUIRED")]
    [TestCase("!!!", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "NAME_TOO_LONG")]
    public void Each_name_check_has_its_own_code(string? name, string code)
    {
        CreateCodes(name, Asus).ShouldBe([code]);
        UpdateCodes(name, Asus).ShouldBe([code]);
    }

    [Test]
    public void A_name_is_at_most_100_characters_after_trimming()
    {
        CreateErrors($"  {new string('a', 100)}  ", Asus).ShouldBeEmpty();
        CreateErrors(new string('a', 101), Asus).ShouldBe([nameof(ProductSeriesCreateRequest.Name)]);
        UpdateErrors(new string('a', 101), Asus).ShouldBe([nameof(ProductSeriesUpdateRequest.Name)]);
    }

    // A series belongs to exactly one brand (PRD-03): no brand, or the empty id, is refused as input.
    [Test]
    public void A_series_without_a_brand_is_refused()
    {
        CreateErrors("ROG Strix", null).ShouldBe([nameof(ProductSeriesCreateRequest.BrandId)]);
        CreateErrors("ROG Strix", Guid.Empty).ShouldBe([nameof(ProductSeriesCreateRequest.BrandId)]);
        UpdateErrors("ROG Strix", null).ShouldBe([nameof(ProductSeriesUpdateRequest.BrandId)]);
        UpdateErrors("ROG Strix", Guid.Empty).ShouldBe([nameof(ProductSeriesUpdateRequest.BrandId)]);
        CreateCodes("ROG Strix", Guid.Empty).ShouldBe([ProductSeriesCreateRequestValidator.BrandIdRequired]);
        UpdateCodes("ROG Strix", null).ShouldBe([ProductSeriesCreateRequestValidator.BrandIdRequired]);
    }

    [Test]
    public void An_update_without_is_active_is_refused()
    {
        UpdateErrors("ROG Strix", Asus, isActive: null).ShouldBe([nameof(ProductSeriesUpdateRequest.IsActive)]);
        UpdateCodes("ROG Strix", Asus, isActive: null).ShouldBe(["IS_ACTIVE_REQUIRED"]);
    }
}
