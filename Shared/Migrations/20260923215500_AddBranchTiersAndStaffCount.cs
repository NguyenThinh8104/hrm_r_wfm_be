using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchTiersAndStaffCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_tiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TierName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinStaffCount = table.Column<int>(type: "int", nullable: false),
                    MaxStaffCount = table.Column<int>(type: "int", nullable: true),
                    OtherConditions = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Conditions = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Benefits = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_tiers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "StaffCount",
                table: "branches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TierId",
                table: "branches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_TierId",
                table: "branches",
                column: "TierId");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_branch_tiers_TierId",
                table: "branches",
                column: "TierId",
                principalTable: "branch_tiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_branch_tiers_TierId",
                table: "branches");

            migrationBuilder.DropIndex(
                name: "IX_branches_TierId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "TierId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "StaffCount",
                table: "branches");

            migrationBuilder.DropTable(
                name: "branch_tiers");
        }
    }
}
