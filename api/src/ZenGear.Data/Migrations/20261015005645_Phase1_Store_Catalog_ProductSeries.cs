using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Store_Catalog_ProductSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    slug = table.Column<string>(type: "varchar", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_series_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_series_brand_id",
                table: "product_series",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_series_id_brand_id",
                table: "product_series",
                columns: new[] { "id", "brand_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_series_slug",
                table: "product_series",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_series");
        }
    }
}
