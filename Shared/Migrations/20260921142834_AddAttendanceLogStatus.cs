using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLogStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropTable(
                name: "kiosk_pairing_codes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "shift_templates");

            migrationBuilder.DropColumn(
                name: "AllowedBrowser",
                table: "kiosks");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "branches");

            migrationBuilder.RenameColumn(
                name: "AllowedIp",
                table: "kiosks",
                newName: "IpAddress");

            migrationBuilder.AlterColumn<ulong>(
                name: "TargetAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AlterColumn<ulong>(
                name: "RequestingAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "shift_swap_requests",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "RequesterUserId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "ScheduleId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "TargetUserId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "branches",
                type: "point",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "attendance_logs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_shift_swap_requests_RequesterUserId",
                table: "shift_swap_requests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_swap_requests_ScheduleId",
                table: "shift_swap_requests",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_swap_requests_TargetUserId",
                table: "shift_swap_requests",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests",
                column: "RequestingAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_users_RequesterUserId",
                table: "shift_swap_requests",
                column: "RequesterUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_users_TargetUserId",
                table: "shift_swap_requests",
                column: "TargetUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_work_schedules_ScheduleId",
                table: "shift_swap_requests",
                column: "ScheduleId",
                principalTable: "work_schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_users_RequesterUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_users_TargetUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_work_schedules_ScheduleId",
                table: "shift_swap_requests");

            migrationBuilder.DropIndex(
                name: "IX_shift_swap_requests_RequesterUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropIndex(
                name: "IX_shift_swap_requests_ScheduleId",
                table: "shift_swap_requests");

            migrationBuilder.DropIndex(
                name: "IX_shift_swap_requests_TargetUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "RequesterUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "attendance_logs");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "kiosks",
                newName: "AllowedIp");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "shift_templates",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<ulong>(
                name: "TargetAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AlterColumn<ulong>(
                name: "RequestingAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedBrowser",
                table: "kiosks",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "branches",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "branches",
                type: "double",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "kiosk_pairing_codes",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedBy = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PairingCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiosk_pairing_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kiosk_pairing_codes_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kiosk_pairing_codes_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_pairing_codes_BranchId",
                table: "kiosk_pairing_codes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_pairing_codes_CreatedBy",
                table: "kiosk_pairing_codes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_pairing_codes_PairingCode",
                table: "kiosk_pairing_codes",
                column: "PairingCode");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests",
                column: "RequestingAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
