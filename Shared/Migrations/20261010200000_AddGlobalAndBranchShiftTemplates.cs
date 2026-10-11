using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalAndBranchShiftTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Thêm cột ShiftMode vào bảng branches
            migrationBuilder.AddColumn<string>(
                name: "ShiftMode",
                table: "branches",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GLOBAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            // 2. Thêm cột Scope, BranchId, ShiftType, Description vào bảng shift_templates
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "shift_templates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GLOBAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "BranchId",
                table: "shift_templates",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShiftType",
                table: "shift_templates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // 3. Foreign Key: BranchId -> branches(Id) Restrict
            migrationBuilder.CreateIndex(
                name: "IX_shift_templates_Scope_BranchId_IsActive",
                table: "shift_templates",
                columns: new[] { "Scope", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_templates_BranchId",
                table: "shift_templates",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_templates_branches_BranchId",
                table: "shift_templates",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. Data Backfill & Check constraint
            migrationBuilder.Sql(@"
                UPDATE `branches`
                SET `ShiftMode` = 'GLOBAL'
                WHERE `ShiftMode` IS NULL OR `ShiftMode` = '';
            ");

            migrationBuilder.Sql(@"
                UPDATE `shift_templates`
                SET `Scope` = 'GLOBAL', `BranchId` = NULL
                WHERE `Scope` IS NULL OR `Scope` = '';
            ");

            // Backfill IsOvernight = (EndTime < StartTime)
            migrationBuilder.Sql(@"
                UPDATE `shift_templates`
                SET `IsOvernight` = CASE WHEN `EndTime` < `StartTime` THEN 1 ELSE 0 END;
            ");

            // Backfill ShiftType từ StartTime
            migrationBuilder.Sql(@"
                UPDATE `shift_templates`
                SET `ShiftType` = CASE
                    WHEN `StartTime` >= '05:00:00' AND `StartTime` < '12:00:00' THEN 'SANG'
                    WHEN `StartTime` >= '12:00:00' AND `StartTime` < '20:00:00' THEN 'CHIEU'
                    WHEN `StartTime` >= '20:00:00' OR `StartTime` < '05:00:00' THEN 'DEM'
                    ELSE 'KHAC'
                END
                WHERE `ShiftType` IS NULL OR `ShiftType` = '';
            ");

            // Check Constraint: Scope='GLOBAL' thì BranchId IS NULL; Scope='BRANCH' thì BranchId NOT NULL
            migrationBuilder.Sql(@"
                ALTER TABLE `shift_templates`
                ADD CONSTRAINT `CK_shift_templates_Scope_BranchId`
                CHECK ((`Scope` = 'GLOBAL' AND `BranchId` IS NULL) OR (`Scope` = 'BRANCH' AND `BranchId` IS NOT NULL));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `shift_templates` DROP CHECK `CK_shift_templates_Scope_BranchId`;");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_templates_branches_BranchId",
                table: "shift_templates");

            migrationBuilder.DropIndex(
                name: "IX_shift_templates_BranchId",
                table: "shift_templates");

            migrationBuilder.DropIndex(
                name: "IX_shift_templates_Scope_BranchId_IsActive",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "ShiftType",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "ShiftMode",
                table: "branches");
        }
    }
}
