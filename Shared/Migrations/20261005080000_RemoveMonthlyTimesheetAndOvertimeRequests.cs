using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Data;

#nullable disable

namespace Shared.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261005080000_RemoveMonthlyTimesheetAndOvertimeRequests")]
    public partial class RemoveMonthlyTimesheetAndOvertimeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `overtime_requests`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `monthly_timesheets`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
