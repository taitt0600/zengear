using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <summary>
    /// The field-level change history audit_logs (R-AUD-01, DEC-086), with its columns in the order of api/db, and
    /// fn_audit_erase_values, the one way to change its rows once api/db/zengear_08_roles.sql has revoked UPDATE and DELETE
    /// (erasing personal values on anonymization), created with SQL as api/db writes it: EF Core maps no function.
    /// </summary>
    public partial class Phase1_Store_Audit_AuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    table_name = table.Column<string>(type: "varchar", nullable: false),
                    record_key = table.Column<string>(type: "jsonb", nullable: false),
                    action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    transaction_id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "pg_current_xact_id()::text::bigint"),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => new { x.id, x.changed_at });
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_changed_by",
                table: "audit_logs",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_table_name_changed_at",
                table: "audit_logs",
                columns: new[] { "table_name", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_table_name_record_key",
                table: "audit_logs",
                columns: new[] { "table_name", "record_key" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_transaction_id",
                table: "audit_logs",
                column: "transaction_id");

            // The body is compared with api/db character for character (tools/schema_snapshot.sql hashes it), so its line
            // endings are LF whatever the checkout of this file.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION fn_audit_erase_values(p_table_name varchar, p_record_key jsonb) RETURNS integer
                LANGUAGE sql SECURITY DEFINER SET search_path = public AS $$
                  WITH u AS (
                    UPDATE audit_logs
                    SET old_values = (SELECT jsonb_object_agg(k, NULL) FROM jsonb_object_keys(old_values) k),
                        new_values = (SELECT jsonb_object_agg(k, NULL) FROM jsonb_object_keys(new_values) k)
                    WHERE table_name = p_table_name AND record_key = p_record_key
                    RETURNING 1)
                  SELECT count(*)::integer FROM u
                $$;
                """.ReplaceLineEndings("\n"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION fn_audit_erase_values(varchar, jsonb);");

            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
