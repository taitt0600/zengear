using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using ZenGear.Data;
using ZenGear.Data.Entities.Catalog;

namespace ZenGear.Services.Tests.Catalog.Series;

/// <summary>
/// The <see cref="ProductSeries"/> entity and the model of <c>product_series</c> (api/db/zengear_02_schema_phase1_store.sql).
/// </summary>
public class ProductSeriesModelTests
{
    private static readonly Guid Asus = Guid.CreateVersion7();
    private static readonly Guid Msi = Guid.CreateVersion7();
    private static readonly Guid Creator = Guid.CreateVersion7();
    private static readonly Guid Editor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Created = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    [Test]
    public void Create_trims_belongs_to_one_brand_and_starts_active()
    {
        var series = ProductSeries.Create("  ROG Strix ", "rog-strix", Asus, Creator, Created);

        series.Name.ShouldBe("ROG Strix");
        series.Slug.ShouldBe("rog-strix");
        series.BrandId.ShouldBe(Asus);
        series.IsActive.ShouldBeTrue();
        series.CreatedBy.ShouldBe(Creator);
        series.UpdatedAt.ShouldBe(Created);
    }

    [TestCase(" ", "rog-strix")]
    [TestCase("ROG Strix", "")]
    public void Create_refuses_a_blank_name_or_slug(string name, string slug) =>
        Should.Throw<ArgumentException>(() => ProductSeries.Create(name, slug, Asus, Creator, Created));

    [Test]
    public void Create_refuses_an_empty_brand() =>
        Should.Throw<ArgumentException>(() => ProductSeries.Create("ROG Strix", "rog-strix", Guid.Empty, Creator, Created));

    [Test]
    public void R_DEL_01_update_retires_and_restores_without_removing_the_series()
    {
        var series = ProductSeries.Create("ROG Strix", "rog-strix", Asus, Creator, Created);

        series.Update("ROG Strix", Asus, isActive: false, Editor, Later);
        series.IsActive.ShouldBeFalse();
        series.Name.ShouldBe("ROG Strix");

        series.Update("ROG Strix", Asus, isActive: true, Editor, Later.AddHours(1));
        series.IsActive.ShouldBeTrue();
        series.UpdatedAt.ShouldBe(Later.AddHours(1));
    }

    [Test]
    public void Update_renames_and_keeps_the_slug()
    {
        var series = ProductSeries.Create("Strix", "strix", Asus, Creator, Created);

        series.Update(" ROG Strix ", Asus, isActive: true, Editor, Later);

        series.Name.ShouldBe("ROG Strix");
        series.Slug.ShouldBe("strix");
        series.UpdatedBy.ShouldBe(Editor);
        series.UpdatedAt.ShouldBe(Later);
    }

    [Test]
    public void Update_moves_the_series_to_another_brand()
    {
        var series = ProductSeries.Create("Ventus", "ventus", Asus, Creator, Created);

        series.Update("Ventus", Msi, isActive: true, Editor, Later);

        series.BrandId.ShouldBe(Msi);
        series.UpdatedAt.ShouldBe(Later);
    }

    [Test]
    public void Update_stamps_only_when_something_changes()
    {
        var series = ProductSeries.Create("ROG Strix", "rog-strix", Asus, Creator, Created);

        series.Update(" ROG Strix ", Asus, isActive: true, Editor, Later);

        series.UpdatedBy.ShouldBe(Creator);
        series.UpdatedAt.ShouldBe(Created);
    }

    [Test]
    public void Update_refuses_a_blank_name() =>
        Should.Throw<ArgumentException>(() =>
            ProductSeries.Create("ROG Strix", "rog-strix", Asus, Creator, Created).Update(" ", Asus, true, Editor, Later));

    [Test]
    public void Update_refuses_an_empty_brand() =>
        Should.Throw<ArgumentException>(() =>
            ProductSeries.Create("ROG Strix", "rog-strix", Asus, Creator, Created).Update("ROG Strix", Guid.Empty, true, Editor, Later));

    [Test]
    public void Product_series_table_keys_and_indexes_have_the_specified_names()
    {
        using var context = new ZenGearDbContext(new DbContextOptionsBuilder<ZenGearDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options);
        var series = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProductSeries))!;

        series.GetTableName().ShouldBe("product_series");
        series.FindPrimaryKey()!.GetName().ShouldBe("pk_product_series");

        var indexes = series.GetIndexes()
            .ToDictionary(i => i.GetDatabaseName() ?? "",
                i => (i.IsUnique, Columns: string.Join(",", i.Properties.Select(p => p.GetColumnName()))));
        indexes.ShouldBe(new Dictionary<string, (bool, string)>
        {
            ["ix_product_series_slug"] = (true, "slug"),
            ["ix_product_series_brand_id"] = (false, "brand_id"),
            ["ix_product_series_id_brand_id"] = (true, "id,brand_id"),
        }, ignoreOrder: true);

        var brand = series.GetForeignKeys().ShouldHaveSingleItem();
        brand.GetConstraintName().ShouldBe("fk_product_series_brands_brand_id");
        brand.PrincipalEntityType.ClrType.ShouldBe(typeof(Brand));
        brand.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        brand.IsRequired.ShouldBeTrue();
    }
}
