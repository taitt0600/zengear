using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Store_Identity_Roles : Migration
    {
        /// <summary>
        /// R-SEC-01 and R-SEC-02, copied verbatim from api/db/zengear_02_schema_phase1_store.sql:
        /// the schema check compares a hash of each function definition.
        /// </summary>
        private const string UserRoleTriggers = """
            CREATE OR REPLACE FUNCTION fn_user_roles_min_one() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              PERFORM 1 FROM users WHERE id = OLD.user_id FOR UPDATE;
              IF FOUND AND NOT EXISTS (SELECT 1 FROM user_roles WHERE user_id = OLD.user_id) THEN
                RAISE EXCEPTION 'R-SEC-01: user % must keep at least 1 role', OLD.user_id USING ERRCODE = 'check_violation';
              END IF;
              RETURN NULL;
            END $$;

            CREATE CONSTRAINT TRIGGER trg_user_roles_min_one
              AFTER DELETE ON user_roles DEFERRABLE INITIALLY DEFERRED
              FOR EACH ROW EXECUTE FUNCTION fn_user_roles_min_one();

            CREATE OR REPLACE FUNCTION fn_user_roles_last_admin() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              PERFORM 1 FROM roles WHERE id = OLD.role_id AND name = 'Admin' FOR UPDATE;
              IF FOUND AND NOT EXISTS (
                   SELECT 1 FROM user_roles ur JOIN users u ON u.id = ur.user_id
                   WHERE ur.role_id = OLD.role_id AND u.status = 'ACTIVE') THEN
                RAISE EXCEPTION 'R-SEC-02: cannot remove the last active Admin' USING ERRCODE = 'check_violation';
              END IF;
              RETURN NULL;
            END $$;

            CREATE CONSTRAINT TRIGGER trg_user_roles_last_admin
              AFTER DELETE ON user_roles DEFERRABLE INITIALLY DEFERRED
              FOR EACH ROW EXECUTE FUNCTION fn_user_roles_last_admin();
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    scoping_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    max_discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.CheckConstraint("ck_roles_max_discount_percent", "max_discount_percent is null or (max_discount_percent >= 0 and max_discount_percent <= 100)");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_permissions_key",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.Sql(UserRoleTriggers.ReplaceLineEndings("\n"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_user_roles_last_admin ON user_roles;
                DROP TRIGGER IF EXISTS trg_user_roles_min_one ON user_roles;
                DROP FUNCTION IF EXISTS fn_user_roles_last_admin();
                DROP FUNCTION IF EXISTS fn_user_roles_min_one();
                """);

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
