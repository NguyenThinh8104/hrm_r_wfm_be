using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAttendanceLogStatusLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `attendance_logs` SET `Status` = 3 WHERE `Status` IN (4, 5);");
            migrationBuilder.Sql("UPDATE `attendance_logs` SET `Status` = 2 WHERE `Status` = 3 AND `CheckOutTime` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
