using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using ZenGear.Data;
using ZenGear.Data.Entities.Catalog;

namespace ZenGear.Services.Tests.Catalog.Brands;

/// <summary>The <see cref="Brand"/> entity and the model of <c>brands</c> (api/db/zengear_02_schema_phase1_store.sql).</summary>
public class BrandModelTests
{
    private static readonly Guid Creator = Guid.CreateVersion7();
    private static readonly Guid Editor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Created = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    [Test]
    public void Create_trims_and_starts_active()
    {
        var brand = Brand.Create("  ASUS ", "asus", Creator, Created);

        brand.Name.ShouldBe("ASUS");
        brand.Slug.ShouldBe("asus");
        brand.IsActive.ShouldBeTrue();
        brand.CreatedBy.ShouldBe(Creator);
        brand.UpdatedAt.ShouldBe(Created);
    }

    [TestCase(" ", "asus")]
    [TestCase("ASUS", "")]
    public void Create_refuses_a_blank_name_or_slug(string name, string slug) =>
        Should.Throw<ArgumentException>(() => Brand.Create(name, slug, Creator, Created));

    [Test]
    public void R_DEL_01_update_retires_and_restores_without_removing_the_brand()
    {
        var brand = Brand.Create("ASUS", "asus", Creator, Created);

        brand.Update("ASUS", isActive: false, Editor, Later);
        brand.IsActive.ShouldBeFalse();
        brand.Name.ShouldBe("ASUS");

        brand.Update("ASUS", isActive: true, Editor, Later.AddHours(1));
        brand.IsActive.ShouldBeTrue();
        brand.UpdatedAt.ShouldBe(Later.AddHours(1));
    }

    [Test]
    public void Update_renames_and_keeps_the_slug()
    {
        var brand = Brand.Create("WD", "wd", Creator, Created);

        brand.Update(" Western Digital ", isActive: true, Editor, Later);

        brand.Name.ShouldBe("Western Digital");
        brand.Slug.ShouldBe("wd");
        brand.UpdatedBy.ShouldBe(Editor);
        brand.UpdatedAt.ShouldBe(Later);
    }

    [Test]
    public void Update_stamps_only_when_something_changes()
    {
        var brand = Brand.Create("ASUS", "asus", Creator, Created);

        brand.Update(" ASUS ", isActive: true, Editor, Later);

        brand.UpdatedBy.ShouldBe(Creator);
        brand.UpdatedAt.ShouldBe(Created);
    }

    [Test]
    public void Update_refuses_a_blank_name() =>
        Should.Throw<ArgumentException>(() => Brand.Create("ASUS", "asus", Creator, Created).Update(" ", true, Editor, Later));

    [Test]
    public void Brands_table_key_and_index_have_the_specified_names()
    {
        using var context = new ZenGearDbContext(new DbContextOptionsBuilder<ZenGearDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options);
        var brands = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Brand))!;

        brands.GetTableName().ShouldBe("brands");
        brands.FindPrimaryKey()!.GetName().ShouldBe("pk_brands");
        var slug = brands.GetIndexes().ShouldHaveSingleItem();
        slug.GetDatabaseName().ShouldBe("ix_brands_slug");
        slug.IsUnique.ShouldBeTrue();
    }
}
