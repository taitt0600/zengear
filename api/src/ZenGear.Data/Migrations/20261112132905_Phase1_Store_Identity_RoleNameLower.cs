using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <summary>
    /// ix_roles_name becomes unique on lower(name) (R-SEC-14, DEC-129 (3)). EF Core 10 maps no index on an expression, so
    /// the model leaves it out and this migration creates it with SQL. Fails on a database that holds two roles whose
    /// names differ only in case (none is deployed yet, DEC-129).
    /// </summary>
    public partial class Phase1_Store_Identity_RoleNameLower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roles_name",
                table: "roles");

            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_roles_name ON roles (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roles_name",
                table: "roles");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);
        }
    }
}
