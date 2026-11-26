using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZenGear.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_Store_Identity_StaffCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "staff_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_profiles_created_by",
                table: "staff_profiles",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "fk_staff_profiles_users_created_by",
                table: "staff_profiles",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_staff_profiles_users_created_by",
                table: "staff_profiles");

            migrationBuilder.DropIndex(
                name: "ix_staff_profiles_created_by",
                table: "staff_profiles");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "staff_profiles");
        }
    }
}
