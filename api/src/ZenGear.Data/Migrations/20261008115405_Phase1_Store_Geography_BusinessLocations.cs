using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Store_Geography_BusinessLocations : Migration
    {
        /// <summary>
        /// R-DB-09: the ward of the business location belongs to its province. Copied from
        /// api/db/zengear_02_schema_phase1_store.sql; created with SQL because its target is the unique index
        /// ix_wards_id_province_id, not a key, and an EF Core relationship needs a key (BusinessLocationConfiguration).
        /// </summary>
        private const string WardProvinceForeignKey = """
            ALTER TABLE business_locations ADD CONSTRAINT fk_business_locations_wards_ward_id_province_id
              FOREIGN KEY (ward_id, province_id) REFERENCES wards (id, province_id) ON DELETE RESTRICT;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_code3 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    country_number = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    common_name = table.Column<string>(type: "varchar", nullable: true),
                    formal_name = table.Column<string>(type: "varchar", nullable: true),
                    country_type = table.Column<string>(type: "varchar", nullable: true),
                    country_sub_type = table.Column<string>(type: "varchar", nullable: true),
                    sovereignty = table.Column<string>(type: "varchar", nullable: true),
                    capital = table.Column<string>(type: "varchar", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    currency_name = table.Column<string>(type: "varchar", nullable: true),
                    telephone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    internet_country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    flag_url = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provinces",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    country_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provinces", x => x.id);
                    table.ForeignKey(
                        name: "fk_provinces_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "varchar", nullable: true),
                    address_detail = table.Column<string>(type: "text", nullable: false),
                    regular_hours = table.Column<string>(type: "jsonb", nullable: false),
                    special_hours = table.Column<string>(type: "jsonb", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    province_id = table.Column<int>(type: "integer", nullable: false),
                    ward_id = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_locations", x => x.id);
                    table.CheckConstraint("ck_business_locations_regular_hours", "jsonb_typeof(regular_hours) = 'array'");
                    table.CheckConstraint("ck_business_locations_special_hours", "special_hours is null or jsonb_typeof(special_hours) = 'array'");
                    table.ForeignKey(
                        name: "fk_business_locations_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wards",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    province_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wards", x => x.id);
                    table.ForeignKey(
                        name: "fk_wards_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_locations_name",
                table: "business_locations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_business_locations_province_id",
                table: "business_locations",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_locations_ward_id",
                table: "business_locations",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "ix_countries_country_code",
                table: "countries",
                column: "country_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_country_code3",
                table: "countries",
                column: "country_code3",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provinces_code",
                table: "provinces",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provinces_country_id",
                table: "provinces",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ix_provinces_id_country_id",
                table: "provinces",
                columns: new[] { "id", "country_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wards_code",
                table: "wards",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wards_id_province_id",
                table: "wards",
                columns: new[] { "id", "province_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wards_province_id",
                table: "wards",
                column: "province_id");

            migrationBuilder.Sql(WardProvinceForeignKey);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE business_locations DROP CONSTRAINT fk_business_locations_wards_ward_id_province_id;");

            migrationBuilder.DropTable(
                name: "business_locations");

            migrationBuilder.DropTable(
                name: "wards");

            migrationBuilder.DropTable(
                name: "provinces");

            migrationBuilder.DropTable(
                name: "countries");
        }
    }
}
