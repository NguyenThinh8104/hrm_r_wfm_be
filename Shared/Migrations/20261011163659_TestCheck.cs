using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class TestCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "SnapshotEndTime",
                table: "work_schedules",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SnapshotIsOvernight",
                table: "work_schedules",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotShiftName",
                table: "work_schedules",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "SnapshotStartTime",
                table: "work_schedules",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "BranchId",
                table: "shift_templates",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "shift_templates",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "shift_templates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GLOBAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ShiftType",
                table: "shift_templates",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ShiftMode",
                table: "branches",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GLOBAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_shift_templates_BranchId",
                table: "shift_templates",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_templates_Scope_BranchId_IsActive",
                table: "shift_templates",
                columns: new[] { "Scope", "BranchId", "IsActive" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_shift_templates_Scope_BranchId",
                table: "shift_templates",
                sql: "(Scope = 'GLOBAL' AND BranchId IS NULL) OR (Scope = 'BRANCH' AND BranchId IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_templates_branches_BranchId",
                table: "shift_templates",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_templates_branches_BranchId",
                table: "shift_templates");

            migrationBuilder.DropIndex(
                name: "IX_shift_templates_BranchId",
                table: "shift_templates");

            migrationBuilder.DropIndex(
                name: "IX_shift_templates_Scope_BranchId_IsActive",
                table: "shift_templates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_shift_templates_Scope_BranchId",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "SnapshotEndTime",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "SnapshotIsOvernight",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "SnapshotShiftName",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "SnapshotStartTime",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "ShiftType",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "ShiftMode",
                table: "branches");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
