using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFraudFieldsFromAttendanceLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_logs_users_FraudFlaggedBy",
                table: "attendance_logs");

            migrationBuilder.DropIndex(
                name: "IX_attendance_logs_FraudFlaggedBy",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "FraudFlaggedBy",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "FraudReason",
                table: "attendance_logs");

            migrationBuilder.DropColumn(
                name: "IsFraudFlagged",
                table: "attendance_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "FraudFlaggedBy",
                table: "attendance_logs",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FraudReason",
                table: "attendance_logs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsFraudFlagged",
                table: "attendance_logs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_FraudFlaggedBy",
                table: "attendance_logs",
                column: "FraudFlaggedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_logs_users_FraudFlaggedBy",
                table: "attendance_logs",
                column: "FraudFlaggedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
