using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Data;

#nullable disable

namespace Shared.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924085500_AddCheckInAndCheckOutStatusToAttendanceLogs")]
    public partial class AddCheckInAndCheckOutStatusToAttendanceLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "CheckInStatus",
                table: "attendance_logs",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "CheckOutStatus",
                table: "attendance_logs",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateMinutes",
                table: "attendance_logs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyLeaveMinutes",
                table: "attendance_logs",
                type: "int",
                nullable: true);

            // Cập nhật dữ liệu cũ nếu có
            migrationBuilder.Sql("UPDATE attendance_logs SET CheckInStatus = 2 WHERE Status IN (2, 4);");
            migrationBuilder.Sql("UPDATE attendance_logs SET CheckInStatus = 3 WHERE Status IN (3, 5);");
            migrationBuilder.Sql("UPDATE attendance_logs SET CheckOutStatus = 2 WHERE CheckOutTime IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyLeaveMinutes",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "LateMinutes",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "CheckOutStatus",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "CheckInStatus",
                table: "attendance_logs");
        }
    }
}
