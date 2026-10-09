using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Shouldly;
using ZenGear.Data.Common;

namespace ZenGear.Services.Tests.Data;

/// <summary>The composite foreign keys of api/db/ (R-DB-09) as the migrations write them.</summary>
public class MigrationBuilderExtensionsTests
{
    [Test]
    public void R_DB_09_a_composite_foreign_key_on_a_unique_index_restricts_delete()
    {
        var sql = SqlOf(b => b.AddCompositeForeignKey("fk_business_locations_wards_ward_id_province_id", "business_locations",
            ["ward_id", "province_id"], "wards", ["id", "province_id"], CompositeForeignKeyOnDelete.Restrict));

        sql.ShouldBe("""ALTER TABLE "business_locations" ADD CONSTRAINT "fk_business_locations_wards_ward_id_province_id" FOREIGN KEY ("ward_id", "province_id") REFERENCES "wards" ("id", "province_id") ON DELETE RESTRICT;""");
    }

    [Test]
    public void R_DB_09_set_null_can_name_the_columns_it_clears()
    {
        var sql = SqlOf(b => b.AddCompositeForeignKey("fk_product_images_product_skus_sku_id", "product_images",
            ["sku_id", "product_id"], "product_skus", ["id", "product_id"], CompositeForeignKeyOnDelete.SetNullColumns("sku_id")));

        sql.ShouldBe("""ALTER TABLE "product_images" ADD CONSTRAINT "fk_product_images_product_skus_sku_id" FOREIGN KEY ("sku_id", "product_id") REFERENCES "product_skus" ("id", "product_id") ON DELETE SET NULL ("sku_id");""");
    }

    [Test]
    public void R_DB_09_cascade_and_set_null_write_their_clause()
    {
        SqlOf(b => b.AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id", "b"], CompositeForeignKeyOnDelete.Cascade))
            .ShouldEndWith(") ON DELETE CASCADE;");
        SqlOf(b => b.AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id", "b"], CompositeForeignKeyOnDelete.SetNull))
            .ShouldEndWith(") ON DELETE SET NULL;");
    }

    [Test]
    public void R_DB_09_down_drops_the_constraint()
    {
        SqlOf(b => b.DropCompositeForeignKey("fk_business_locations_wards_ward_id_province_id", "business_locations"))
            .ShouldBe("""ALTER TABLE "business_locations" DROP CONSTRAINT "fk_business_locations_wards_ward_id_province_id";""");
    }

    [Test]
    public void R_DB_09_a_quote_inside_an_identifier_is_doubled()
    {
        SqlOf(b => b.AddCompositeForeignKey("f\"k", "t", ["a\"b", "c"], "p", ["id", "c"], CompositeForeignKeyOnDelete.Restrict))
            .ShouldBe("""ALTER TABLE "t" ADD CONSTRAINT "f""k" FOREIGN KEY ("a""b", "c") REFERENCES "p" ("id", "c") ON DELETE RESTRICT;""");
    }

    [Test]
    public void R_DB_09_refuses_a_column_count_mismatch()
    {
        Should.Throw<ArgumentException>(() => new MigrationBuilder(Provider)
            .AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id"], CompositeForeignKeyOnDelete.Restrict));
    }

    [Test]
    public void R_DB_09_refuses_empty_columns()
    {
        Should.Throw<ArgumentException>(() => new MigrationBuilder(Provider)
            .AddCompositeForeignKey("fk", "t", [], "p", [], CompositeForeignKeyOnDelete.Restrict));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void R_DB_09_refuses_a_blank_identifier(string blank)
    {
        var builder = new MigrationBuilder(Provider);

        Should.Throw<ArgumentException>(() => builder.AddCompositeForeignKey(blank, "t", ["a", "b"], "p", ["id", "b"], CompositeForeignKeyOnDelete.Restrict));
        Should.Throw<ArgumentException>(() => builder.AddCompositeForeignKey("fk", "t", ["a", blank], "p", ["id", "b"], CompositeForeignKeyOnDelete.Restrict));
        Should.Throw<ArgumentException>(() => builder.AddCompositeForeignKey("fk", "t", ["a", "b"], blank, ["id", "b"], CompositeForeignKeyOnDelete.Restrict));
        Should.Throw<ArgumentException>(() => builder.AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id", blank], CompositeForeignKeyOnDelete.Restrict));
        Should.Throw<ArgumentException>(() => builder.DropCompositeForeignKey("fk", blank));
        builder.Operations.ShouldBeEmpty();
    }

    [Test]
    public void R_DB_09_refuses_a_missing_on_delete()
    {
        Should.Throw<ArgumentNullException>(() => new MigrationBuilder(Provider)
            .AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id", "b"], null!));
    }

    [Test]
    public void R_DB_09_set_null_columns_names_at_least_one_column()
    {
        Should.Throw<ArgumentException>(() => CompositeForeignKeyOnDelete.SetNullColumns());
        Should.Throw<ArgumentException>(() => CompositeForeignKeyOnDelete.SetNullColumns(" "));
    }

    [Test]
    public void R_DB_09_set_null_columns_are_columns_of_the_key()
    {
        Should.Throw<ArgumentException>(() => new MigrationBuilder(Provider)
            .AddCompositeForeignKey("fk", "t", ["a", "b"], "p", ["id", "b"], CompositeForeignKeyOnDelete.SetNullColumns("c")));
    }

    private const string Provider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static string SqlOf(Action<MigrationBuilder> write)
    {
        var builder = new MigrationBuilder(Provider);
        write(builder);
        return builder.Operations.OfType<SqlOperation>().Single().Sql;
    }
}
