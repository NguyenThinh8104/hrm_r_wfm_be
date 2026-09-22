using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateAttendanceStatusAndDropUnusedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE attendance_logs SET Status = '1' WHERE Status = 'PENDING' OR Status IS NULL OR Status = '';");
            migrationBuilder.Sql("UPDATE attendance_logs SET Status = '2' WHERE Status = 'COMPLETED' OR Status = 'PRESENT';");

            migrationBuilder.Sql(@"
                SET @col_exists = 0;
                SELECT COUNT(*) INTO @col_exists 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'attendance_logs' 
                  AND COLUMN_NAME = 'OpeningFloatCash';

                SET @sql_stmt = IF(@col_exists > 0, 'ALTER TABLE `attendance_logs` DROP COLUMN `OpeningFloatCash`', 'SELECT 1');
                PREPARE stmt FROM @sql_stmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                SET @islate_exists = 0;
                SELECT COUNT(*) INTO @islate_exists 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'attendance_logs' 
                  AND COLUMN_NAME = 'IsLate';

                SET @sql_islate = IF(@islate_exists > 0, 'ALTER TABLE `attendance_logs` DROP COLUMN `IsLate`', 'SELECT 1');
                PREPARE stmt2 FROM @sql_islate;
                EXECUTE stmt2;
                DEALLOCATE PREPARE stmt2;
            ");

            migrationBuilder.AlterColumn<byte>(
                name: "Status",
                table: "attendance_logs",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)1,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "attendance_logs",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "tinyint unsigned",
                oldDefaultValue: (byte)1)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningFloatCash",
                table: "attendance_logs",
                type: "decimal(65,30)",
                nullable: true);
        }
    }
}
