using Shouldly;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Tests.Catalog.Brands;

/// <summary>
/// The slug of a catalog name, by the algorithm of R-PRD-06. The tests are not named after R-PRD-06: that rule belongs to
/// PRD-08 (manual slug edits, redirects), which a test named after it here would count as covered.
/// </summary>
public class CatalogSlugTests
{
    [TestCase("Thương hiệu Đồng Nai", "thuong-hieu-dong-nai")]
    [TestCase("ĐẶNG Ốc Vít", "dang-oc-vit")]
    [TestCase("Phụ kiện số Ưu Việt", "phu-kien-so-uu-viet")]
    public void Slug_removes_vietnamese_marks_and_the_d_stroke(string name, string slug) =>
        CatalogSlug.From(name).ShouldBe(slug);

    [TestCase("Cooler Master", "cooler-master")]
    [TestCase("G.Skill", "g-skill")]
    [TestCase("  Lian   Li  ", "lian-li")]
    [TestCase("--Be Quiet!--", "be-quiet")]
    [TestCase("A & B / C", "a-b-c")]
    [TestCase("Model 2026", "model-2026")]
    public void Slug_joins_words_and_symbols_with_one_hyphen(string name, string slug) =>
        CatalogSlug.From(name).ShouldBe(slug);

    // Pairs of api/seed/07_categories_brands.sql: a brand created through the API gets the slug the seed would give it.
    [TestCase("ASUS", "asus")]
    [TestCase("G.Skill", "g-skill")]
    [TestCase("Cooler Master", "cooler-master")]
    [TestCase("Lian Li", "lian-li")]
    [TestCase("ID-Cooling", "id-cooling")]
    [TestCase("E-Dra", "e-dra")]
    [TestCase("WD", "wd")]
    public void Slug_reproduces_the_seed_slugs(string name, string slug) => CatalogSlug.From(name).ShouldBe(slug);

    [TestCase("联想")]
    [TestCase("Ø")]
    [TestCase("ß")]
    [TestCase("!!!")]
    [TestCase("   ")]
    public void Slug_of_a_name_without_latin_letters_or_digits_is_empty(string name) => CatalogSlug.From(name).ShouldBeEmpty();
}
