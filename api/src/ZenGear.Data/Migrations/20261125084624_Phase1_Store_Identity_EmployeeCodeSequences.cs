using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <summary>
    /// Generated staff identifiers (R-USR-24, DEC-145, DEC-156): staff_profiles.employee_code takes the form ZG + 6 digits,
    /// staff_profiles.username is added, unique on lower(username) (created with SQL: EF Core 10 maps no index on an
    /// expression), and the yearly counter employee_code_sequences is created. Refuses a database whose staff_profiles
    /// holds rows: they have no generated identifiers and no production data exists yet (DEC-145); recreate a
    /// development database with tools/dev_reset.ps1.
    /// </summary>
    public partial class Phase1_Store_Identity_EmployeeCodeSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM staff_profiles) THEN
                    RAISE EXCEPTION 'staff_profiles holds accounts without generated identifiers (R-USR-24): recreate the development database with tools/dev_reset.ps1';
                  END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "employee_code",
                table: "staff_profiles",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar",
                oldNullable: true);

            // No default: the table is empty (checked above), and api/db declares none.
            migrationBuilder.Sql("ALTER TABLE staff_profiles ADD username character varying(64) NOT NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ix_staff_profiles_username ON staff_profiles (lower(username));");

            migrationBuilder.CreateTable(
                name: "employee_code_sequences",
                columns: table => new
                {
                    year = table.Column<short>(type: "smallint", nullable: false),
                    last_value = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_code_sequences", x => x.year);
                    table.CheckConstraint("ck_employee_code_sequences_last_value", "last_value between 1 and 9999");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_profiles_employee_code",
                table: "staff_profiles",
                sql: "employee_code ~ '^ZG[0-9]{6}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_code_sequences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_profiles_employee_code",
                table: "staff_profiles");

            migrationBuilder.Sql("DROP INDEX ix_staff_profiles_username;");

            migrationBuilder.DropColumn(
                name: "username",
                table: "staff_profiles");

            migrationBuilder.AlterColumn<string>(
                name: "employee_code",
                table: "staff_profiles",
                type: "varchar",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);
        }
    }
}
