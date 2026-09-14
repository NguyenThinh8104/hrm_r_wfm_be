using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddPureAttendanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashHandover_Employee",
                table: "CashHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_CashHandover_Session",
                table: "CashHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityHandover_Employee",
                table: "SecurityHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityHandover_Session",
                table: "SecurityHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Employee",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Schedule",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Shift",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Store",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwap_Assignment",
                table: "ShiftSwapRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwap_Requester",
                table: "ShiftSwapRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwap_Reviewer",
                table: "ShiftSwapRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSwap_Target",
                table: "ShiftSwapRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatch_ApprovedBy",
                table: "TemporaryDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatch_Employee",
                table: "TemporaryDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatch_FromStore",
                table: "TemporaryDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatch_RequestedBy",
                table: "TemporaryDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispatch_ToStore",
                table: "TemporaryDispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedules_PublishedBy",
                table: "WorkSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedules_Stores",
                table: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "AttendanceExceptions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ShiftHandoverSessions");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropPrimaryKey(
                name: "PK__Users__1788CC4CD146E62B",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ__Users__536C85E49C6B9BC8",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK__WorkSche__9C8A5B492182D683",
                table: "WorkSchedules");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedules_PublishedBy",
                table: "WorkSchedules");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedules_StoreId",
                table: "WorkSchedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK__Temporar__434DBD5501B43404",
                table: "TemporaryDispatches");

            migrationBuilder.DropIndex(
                name: "IX_Dispatch_Employee_Date",
                table: "TemporaryDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TemporaryDispatches_FromStoreId",
                table: "TemporaryDispatches");

            migrationBuilder.DropIndex(
                name: "IX_TemporaryDispatches_ToStoreId",
                table: "TemporaryDispatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK__ShiftSwa__EEF5A489E9A0BB3E",
                table: "ShiftSwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSwapRequests_AssignmentId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSwapRequests_RequesterEmployeeId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSwapRequests_TargetEmployeeId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK__ShiftAss__32499E77026D4562",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_Employee_WorkDate",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_ScheduleId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_ShiftId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_Store_WorkDate",
                table: "ShiftAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK__Security__9E160887FF713161",
                table: "SecurityHandovers");

            migrationBuilder.DropIndex(
                name: "IX_SecurityHandovers_HandoverId",
                table: "SecurityHandovers");

            migrationBuilder.DropIndex(
                name: "IX_SecurityHandovers_SecurityEmployeeId",
                table: "SecurityHandovers");

            migrationBuilder.DropPrimaryKey(
                name: "PK__CashHand__F3BBCA164C9D4280",
                table: "CashHandovers");

            migrationBuilder.DropIndex(
                name: "IX_CashHandovers_CashierEmployeeId",
                table: "CashHandovers");

            migrationBuilder.DropIndex(
                name: "IX_CashHandovers_HandoverId",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "DifferenceAmount",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "WeekEndDate",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "DispatchId",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "FromStoreId",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "ToStoreId",
                table: "TemporaryDispatches");

            migrationBuilder.DropColumn(
                name: "SwapRequestId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "RequesterEmployeeId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "TargetEmployeeId",
                table: "ShiftSwapRequests");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "SecurityHandoverId",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "HandoverId",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "ParkingCards",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "ParkingTickets",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "SecurityEmployeeId",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "SecurityNote",
                table: "SecurityHandovers");

            migrationBuilder.DropColumn(
                name: "CashHandoverId",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "ActualCash",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "CashierEmployeeId",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "DifferenceNote",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "HandoverId",
                table: "CashHandovers");

            migrationBuilder.DropColumn(
                name: "OpeningFloat",
                table: "CashHandovers");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "WorkSchedules",
                newName: "work_schedules");

            migrationBuilder.RenameTable(
                name: "TemporaryDispatches",
                newName: "temporary_dispatches");

            migrationBuilder.RenameTable(
                name: "ShiftSwapRequests",
                newName: "shift_swap_requests");

            migrationBuilder.RenameTable(
                name: "ShiftAssignments",
                newName: "shift_assignments");

            migrationBuilder.RenameTable(
                name: "SecurityHandovers",
                newName: "security_handovers");

            migrationBuilder.RenameTable(
                name: "CashHandovers",
                newName: "cash_handovers");

            migrationBuilder.RenameColumn(
                name: "WeekStartDate",
                table: "work_schedules",
                newName: "WorkDate");

            migrationBuilder.RenameIndex(
                name: "IX_TemporaryDispatches_RequestedBy",
                table: "temporary_dispatches",
                newName: "IX_temporary_dispatches_RequestedBy");

            migrationBuilder.RenameIndex(
                name: "IX_TemporaryDispatches_ApprovedBy",
                table: "temporary_dispatches",
                newName: "IX_temporary_dispatches_ApprovedBy");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftSwapRequests_ReviewedBy",
                table: "shift_swap_requests",
                newName: "IX_shift_swap_requests_ReviewedBy");

            migrationBuilder.RenameColumn(
                name: "WarehouseLocked",
                table: "security_handovers",
                newName: "IsWarehouseLocked");

            migrationBuilder.RenameColumn(
                name: "RollerDoorLocked",
                table: "security_handovers",
                newName: "IsShutterClosed");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "users",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "users",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "users",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "users",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "users",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "HomeBranchId",
                table: "users",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KioskPinHash",
                table: "users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "users",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte>(
                name: "RoleId",
                table: "users",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "users",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "work_schedules",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Draft")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "work_schedules",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "work_schedules",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<ulong>(
                name: "BranchId",
                table: "work_schedules",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "CreatedBy",
                table: "work_schedules",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<byte>(
                name: "RequiredCashier",
                table: "work_schedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "RequiredSales",
                table: "work_schedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "RequiredSecurity",
                table: "work_schedules",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<uint>(
                name: "ShiftTemplateId",
                table: "work_schedules",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "temporary_dispatches",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<ulong>(
                name: "RequestedBy",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "temporary_dispatches",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AlterColumn<ulong>(
                name: "ApprovedBy",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "temporary_dispatches",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "SourceBranchId",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "TargetBranchId",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "UserId",
                table: "temporary_dispatches",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "shift_swap_requests",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<ulong>(
                name: "ReviewedBy",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "shift_swap_requests",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "shift_swap_requests",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<ulong>(
                name: "RequestingAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "TargetAssignmentId",
                table: "shift_swap_requests",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "shift_assignments",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Scheduled")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<ulong>(
                name: "ScheduleId",
                table: "shift_assignments",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "shift_assignments",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<byte>(
                name: "AssignedRoleId",
                table: "shift_assignments",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentType",
                table: "shift_assignments",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "UserId",
                table: "shift_assignments",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "security_handovers",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "security_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<ushort>(
                name: "OvernightVehicleCount",
                table: "security_handovers",
                type: "smallint unsigned",
                nullable: false,
                defaultValue: (ushort)0);

            migrationBuilder.AddColumn<ulong>(
                name: "SecurityGuardId",
                table: "security_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<string>(
                name: "SecurityNotes",
                table: "security_handovers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<ulong>(
                name: "ShiftHandoverId",
                table: "security_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "cash_handovers",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<ulong>(
                name: "Id",
                table: "cash_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<ulong>(
                name: "CashierId",
                table: "cash_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingActualCash",
                table: "cash_handovers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyReason",
                table: "cash_handovers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningCash",
                table: "cash_handovers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<ulong>(
                name: "ShiftHandoverId",
                table: "cash_handovers",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<decimal>(
                name: "SystemExpectedCash",
                table: "cash_handovers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_work_schedules",
                table: "work_schedules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_temporary_dispatches",
                table: "temporary_dispatches",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shift_swap_requests",
                table: "shift_swap_requests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shift_assignments",
                table: "shift_assignments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_security_handovers",
                table: "security_handovers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_cash_handovers",
                table: "cash_handovers",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitude = table.Column<double>(type: "double", nullable: true),
                    Longitude = table.Column<double>(type: "double", nullable: true),
                    GeofenceRadiusMeters = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    RoleCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_handovers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScheduleId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ShiftLeaderId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    HandoverStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeneralNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_handovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_handovers_users_ShiftLeaderId",
                        column: x => x.ShiftLeaderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_handovers_work_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "work_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_templates",
                columns: table => new
                {
                    Id = table.Column<uint>(type: "int unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TemplateCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    BreakDurationMinutes = table.Column<uint>(type: "int unsigned", nullable: false),
                    IsOvernight = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_templates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "system_audit_logs",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ActorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Action = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetTable = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    OldValues = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValues = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_audit_logs_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "kiosk_activation_codes",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    KioskName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedBy = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedKioskId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiosk_activation_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kiosk_activation_codes_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_kiosk_activation_codes_users_GeneratedBy",
                        column: x => x.GeneratedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "kiosk_pairing_codes",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PairingCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "kiosks",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KioskCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AllowedIp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceToken = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AllowedBrowser = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastPingAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kiosks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kiosks_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "monthly_timesheets",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    PeriodYear = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LockedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_timesheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_timesheets_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_monthly_timesheets_users_LockedBy",
                        column: x => x.LockedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "attendance_logs",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AssignmentId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    KioskId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CheckOutTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CheckInPhotoKey = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckOutPhotoKey = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpeningFloatCash = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    IsFraudFlagged = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FraudFlaggedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    FraudReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_logs_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_logs_kiosks_KioskId",
                        column: x => x.KioskId,
                        principalTable: "kiosks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_attendance_logs_shift_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "shift_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_logs_users_FraudFlaggedBy",
                        column: x => x.FraudFlaggedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "overtime_requests",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AttendanceLogId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    OtType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedMinutes = table.Column<uint>(type: "int unsigned", nullable: false),
                    ApprovedMinutes = table.Column<uint>(type: "int unsigned", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VerifiedByLeader = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ApprovedByManager = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ManagerNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overtime_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_overtime_requests_attendance_logs_AttendanceLogId",
                        column: x => x.AttendanceLogId,
                        principalTable: "attendance_logs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_overtime_requests_users_ApprovedByManager",
                        column: x => x.ApprovedByManager,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_overtime_requests_users_VerifiedByLeader",
                        column: x => x.VerifiedByLeader,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_EmployeeCode",
                table: "users",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_HomeBranchId_Status",
                table: "users",
                columns: new[] { "HomeBranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_Phone",
                table: "users",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                table: "users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_work_schedules_BranchId_ShiftTemplateId_WorkDate",
                table: "work_schedules",
                columns: new[] { "BranchId", "ShiftTemplateId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_schedules_BranchId_WorkDate_Status",
                table: "work_schedules",
                columns: new[] { "BranchId", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_work_schedules_CreatedBy",
                table: "work_schedules",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_work_schedules_ShiftTemplateId",
                table: "work_schedules",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_dispatches_SourceBranchId",
                table: "temporary_dispatches",
                column: "SourceBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_dispatches_TargetBranchId",
                table: "temporary_dispatches",
                column: "TargetBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_temporary_dispatches_UserId_StartDate_EndDate_Status",
                table: "temporary_dispatches",
                columns: new[] { "UserId", "StartDate", "EndDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_swap_requests_RequestingAssignmentId",
                table: "shift_swap_requests",
                column: "RequestingAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_swap_requests_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_AssignedRoleId",
                table: "shift_assignments",
                column: "AssignedRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_ScheduleId_UserId",
                table: "shift_assignments",
                columns: new[] { "ScheduleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_UserId_ScheduleId",
                table: "shift_assignments",
                columns: new[] { "UserId", "ScheduleId" });

            migrationBuilder.CreateIndex(
                name: "IX_security_handovers_SecurityGuardId",
                table: "security_handovers",
                column: "SecurityGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_security_handovers_ShiftHandoverId",
                table: "security_handovers",
                column: "ShiftHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_CashierId",
                table: "cash_handovers",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_ShiftHandoverId",
                table: "cash_handovers",
                column: "ShiftHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_AssignmentId",
                table: "attendance_logs",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_BranchId",
                table: "attendance_logs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_CheckInTime_BranchId",
                table: "attendance_logs",
                columns: new[] { "CheckInTime", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_FraudFlaggedBy",
                table: "attendance_logs",
                column: "FraudFlaggedBy");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_KioskId",
                table: "attendance_logs",
                column: "KioskId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_BranchCode",
                table: "branches",
                column: "BranchCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_activation_codes_BranchId",
                table: "kiosk_activation_codes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_activation_codes_Code",
                table: "kiosk_activation_codes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_kiosk_activation_codes_GeneratedBy",
                table: "kiosk_activation_codes",
                column: "GeneratedBy");

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

            migrationBuilder.CreateIndex(
                name: "IX_kiosks_BranchId",
                table: "kiosks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_kiosks_DeviceToken",
                table: "kiosks",
                column: "DeviceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kiosks_KioskCode",
                table: "kiosks",
                column: "KioskCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_timesheets_BranchId_PeriodMonth_PeriodYear",
                table: "monthly_timesheets",
                columns: new[] { "BranchId", "PeriodMonth", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_timesheets_LockedBy",
                table: "monthly_timesheets",
                column: "LockedBy");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_requests_ApprovedByManager",
                table: "overtime_requests",
                column: "ApprovedByManager");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_requests_AttendanceLogId",
                table: "overtime_requests",
                column: "AttendanceLogId");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_requests_VerifiedByLeader",
                table: "overtime_requests",
                column: "VerifiedByLeader");

            migrationBuilder.CreateIndex(
                name: "IX_roles_RoleCode",
                table: "roles",
                column: "RoleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_handovers_ScheduleId",
                table: "shift_handovers",
                column: "ScheduleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_handovers_ShiftLeaderId",
                table: "shift_handovers",
                column: "ShiftLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_templates_TemplateCode",
                table: "shift_templates",
                column: "TemplateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_audit_logs_ActorId",
                table: "system_audit_logs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_system_audit_logs_Timestamp_Action",
                table: "system_audit_logs",
                columns: new[] { "Timestamp", "Action" });

            migrationBuilder.AddForeignKey(
                name: "FK_cash_handovers_shift_handovers_ShiftHandoverId",
                table: "cash_handovers",
                column: "ShiftHandoverId",
                principalTable: "shift_handovers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_cash_handovers_users_CashierId",
                table: "cash_handovers",
                column: "CashierId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_security_handovers_shift_handovers_ShiftHandoverId",
                table: "security_handovers",
                column: "ShiftHandoverId",
                principalTable: "shift_handovers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_security_handovers_users_SecurityGuardId",
                table: "security_handovers",
                column: "SecurityGuardId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_assignments_roles_AssignedRoleId",
                table: "shift_assignments",
                column: "AssignedRoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_assignments_users_UserId",
                table: "shift_assignments",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_assignments_work_schedules_ScheduleId",
                table: "shift_assignments",
                column: "ScheduleId",
                principalTable: "work_schedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests",
                column: "RequestingAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_users_ReviewedBy",
                table: "shift_swap_requests",
                column: "ReviewedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_temporary_dispatches_branches_SourceBranchId",
                table: "temporary_dispatches",
                column: "SourceBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_temporary_dispatches_branches_TargetBranchId",
                table: "temporary_dispatches",
                column: "TargetBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_temporary_dispatches_users_ApprovedBy",
                table: "temporary_dispatches",
                column: "ApprovedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_temporary_dispatches_users_RequestedBy",
                table: "temporary_dispatches",
                column: "RequestedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_temporary_dispatches_users_UserId",
                table: "temporary_dispatches",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_branches_HomeBranchId",
                table: "users",
                column: "HomeBranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_RoleId",
                table: "users",
                column: "RoleId",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_schedules_branches_BranchId",
                table: "work_schedules",
                column: "BranchId",
                principalTable: "branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_work_schedules_shift_templates_ShiftTemplateId",
                table: "work_schedules",
                column: "ShiftTemplateId",
                principalTable: "shift_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_work_schedules_users_CreatedBy",
                table: "work_schedules",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_handovers_shift_handovers_ShiftHandoverId",
                table: "cash_handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_cash_handovers_users_CashierId",
                table: "cash_handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_security_handovers_shift_handovers_ShiftHandoverId",
                table: "security_handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_security_handovers_users_SecurityGuardId",
                table: "security_handovers");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_assignments_roles_AssignedRoleId",
                table: "shift_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_assignments_users_UserId",
                table: "shift_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_assignments_work_schedules_ScheduleId",
                table: "shift_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_RequestingAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_users_ReviewedBy",
                table: "shift_swap_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_temporary_dispatches_branches_SourceBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_temporary_dispatches_branches_TargetBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_temporary_dispatches_users_ApprovedBy",
                table: "temporary_dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_temporary_dispatches_users_RequestedBy",
                table: "temporary_dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_temporary_dispatches_users_UserId",
                table: "temporary_dispatches");

            migrationBuilder.DropForeignKey(
                name: "FK_users_branches_HomeBranchId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_RoleId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_work_schedules_branches_BranchId",
                table: "work_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_work_schedules_shift_templates_ShiftTemplateId",
                table: "work_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_work_schedules_users_CreatedBy",
                table: "work_schedules");

            migrationBuilder.DropTable(
                name: "kiosk_activation_codes");

            migrationBuilder.DropTable(
                name: "kiosk_pairing_codes");

            migrationBuilder.DropTable(
                name: "monthly_timesheets");

            migrationBuilder.DropTable(
                name: "overtime_requests");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "shift_handovers");

            migrationBuilder.DropTable(
                name: "shift_templates");

            migrationBuilder.DropTable(
                name: "system_audit_logs");

            migrationBuilder.DropTable(
                name: "attendance_logs");

            migrationBuilder.DropTable(
                name: "kiosks");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_EmployeeCode",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_HomeBranchId_Status",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Phone",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RoleId",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_work_schedules",
                table: "work_schedules");

            migrationBuilder.DropIndex(
                name: "IX_work_schedules_BranchId_ShiftTemplateId_WorkDate",
                table: "work_schedules");

            migrationBuilder.DropIndex(
                name: "IX_work_schedules_BranchId_WorkDate_Status",
                table: "work_schedules");

            migrationBuilder.DropIndex(
                name: "IX_work_schedules_CreatedBy",
                table: "work_schedules");

            migrationBuilder.DropIndex(
                name: "IX_work_schedules_ShiftTemplateId",
                table: "work_schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_temporary_dispatches",
                table: "temporary_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_temporary_dispatches_SourceBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_temporary_dispatches_TargetBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropIndex(
                name: "IX_temporary_dispatches_UserId_StartDate_EndDate_Status",
                table: "temporary_dispatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_shift_swap_requests",
                table: "shift_swap_requests");

            migrationBuilder.DropIndex(
                name: "IX_shift_swap_requests_RequestingAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropIndex(
                name: "IX_shift_swap_requests_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_shift_assignments",
                table: "shift_assignments");

            migrationBuilder.DropIndex(
                name: "IX_shift_assignments_AssignedRoleId",
                table: "shift_assignments");

            migrationBuilder.DropIndex(
                name: "IX_shift_assignments_ScheduleId_UserId",
                table: "shift_assignments");

            migrationBuilder.DropIndex(
                name: "IX_shift_assignments_UserId_ScheduleId",
                table: "shift_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_security_handovers",
                table: "security_handovers");

            migrationBuilder.DropIndex(
                name: "IX_security_handovers_SecurityGuardId",
                table: "security_handovers");

            migrationBuilder.DropIndex(
                name: "IX_security_handovers_ShiftHandoverId",
                table: "security_handovers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_cash_handovers",
                table: "cash_handovers");

            migrationBuilder.DropIndex(
                name: "IX_cash_handovers_CashierId",
                table: "cash_handovers");

            migrationBuilder.DropIndex(
                name: "IX_cash_handovers_ShiftHandoverId",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "HomeBranchId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "KioskPinHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "RequiredCashier",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "RequiredSales",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "RequiredSecurity",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "ShiftTemplateId",
                table: "work_schedules");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "temporary_dispatches");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "temporary_dispatches");

            migrationBuilder.DropColumn(
                name: "SourceBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropColumn(
                name: "TargetBranchId",
                table: "temporary_dispatches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "temporary_dispatches");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "RequestingAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "shift_assignments");

            migrationBuilder.DropColumn(
                name: "AssignedRoleId",
                table: "shift_assignments");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "shift_assignments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "shift_assignments");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "security_handovers");

            migrationBuilder.DropColumn(
                name: "OvernightVehicleCount",
                table: "security_handovers");

            migrationBuilder.DropColumn(
                name: "SecurityGuardId",
                table: "security_handovers");

            migrationBuilder.DropColumn(
                name: "SecurityNotes",
                table: "security_handovers");

            migrationBuilder.DropColumn(
                name: "ShiftHandoverId",
                table: "security_handovers");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "CashierId",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "ClosingActualCash",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "DiscrepancyReason",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "OpeningCash",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "ShiftHandoverId",
                table: "cash_handovers");

            migrationBuilder.DropColumn(
                name: "SystemExpectedCash",
                table: "cash_handovers");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "work_schedules",
                newName: "WorkSchedules");

            migrationBuilder.RenameTable(
                name: "temporary_dispatches",
                newName: "TemporaryDispatches");

            migrationBuilder.RenameTable(
                name: "shift_swap_requests",
                newName: "ShiftSwapRequests");

            migrationBuilder.RenameTable(
                name: "shift_assignments",
                newName: "ShiftAssignments");

            migrationBuilder.RenameTable(
                name: "security_handovers",
                newName: "SecurityHandovers");

            migrationBuilder.RenameTable(
                name: "cash_handovers",
                newName: "CashHandovers");

            migrationBuilder.RenameColumn(
                name: "WorkDate",
                table: "WorkSchedules",
                newName: "WeekStartDate");

            migrationBuilder.RenameIndex(
                name: "IX_temporary_dispatches_RequestedBy",
                table: "TemporaryDispatches",
                newName: "IX_TemporaryDispatches_RequestedBy");

            migrationBuilder.RenameIndex(
                name: "IX_temporary_dispatches_ApprovedBy",
                table: "TemporaryDispatches",
                newName: "IX_TemporaryDispatches_ApprovedBy");

            migrationBuilder.RenameIndex(
                name: "IX_shift_swap_requests_ReviewedBy",
                table: "ShiftSwapRequests",
                newName: "IX_ShiftSwapRequests_ReviewedBy");

            migrationBuilder.RenameColumn(
                name: "IsWarehouseLocked",
                table: "SecurityHandovers",
                newName: "WarehouseLocked");

            migrationBuilder.RenameColumn(
                name: "IsShutterClosed",
                table: "SecurityHandovers",
                newName: "RollerDoorLocked");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WorkSchedules",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "WorkSchedules",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "ScheduleId",
                table: "WorkSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "WorkSchedules",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedBy",
                table: "WorkSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "WorkSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WeekEndDate",
                table: "WorkSchedules",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "TemporaryDispatches",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "RequestedBy",
                table: "TemporaryDispatches",
                type: "int",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TemporaryDispatches",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<int>(
                name: "ApprovedBy",
                table: "TemporaryDispatches",
                type: "int",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DispatchId",
                table: "TemporaryDispatches",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "TemporaryDispatches",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "TemporaryDispatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FromStoreId",
                table: "TemporaryDispatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TemporaryDispatches",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ToStoreId",
                table: "TemporaryDispatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ShiftSwapRequests",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewedBy",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "ShiftSwapRequests",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ShiftSwapRequests",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "SwapRequestId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequesterEmployeeId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetEmployeeId",
                table: "ShiftSwapRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ShiftAssignments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Scheduled",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "ScheduleId",
                table: "ShiftAssignments",
                type: "int",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AddColumn<int>(
                name: "AssignmentId",
                table: "ShiftAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ShiftAssignments",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "ShiftAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "ShiftAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "ShiftAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "ShiftAssignments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SecurityHandovers",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "SecurityHandoverId",
                table: "SecurityHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<int>(
                name: "HandoverId",
                table: "SecurityHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParkingCards",
                table: "SecurityHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParkingTickets",
                table: "SecurityHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SecurityEmployeeId",
                table: "SecurityHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SecurityNote",
                table: "SecurityHandovers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "CashHandovers",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<int>(
                name: "CashHandoverId",
                table: "CashHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCash",
                table: "CashHandovers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashierEmployeeId",
                table: "CashHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DifferenceNote",
                table: "CashHandovers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "HandoverId",
                table: "CashHandovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningFloat",
                table: "CashHandovers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DifferenceAmount",
                table: "CashHandovers",
                type: "decimal(19,2)",
                nullable: true,
                computedColumnSql: "(case when `ActualCash` IS NULL then NULL else `ActualCash`-`OpeningFloat` end)",
                stored: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK__Users__1788CC4CD146E62B",
                table: "Users",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__WorkSche__9C8A5B492182D683",
                table: "WorkSchedules",
                column: "ScheduleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__Temporar__434DBD5501B43404",
                table: "TemporaryDispatches",
                column: "DispatchId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__ShiftSwa__EEF5A489E9A0BB3E",
                table: "ShiftSwapRequests",
                column: "SwapRequestId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__ShiftAss__32499E77026D4562",
                table: "ShiftAssignments",
                column: "AssignmentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__Security__9E160887FF713161",
                table: "SecurityHandovers",
                column: "SecurityHandoverId");

            migrationBuilder.AddPrimaryKey(
                name: "PK__CashHand__F3BBCA164C9D4280",
                table: "CashHandovers",
                column: "CashHandoverId");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    EntityName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AuditLog__EB5F6CBDF6B5B4CE", x => x.AuditLogId);
                    table.ForeignKey(
                        name: "FK_AuditLogs_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    PositionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    PositionCode = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Position__60BB9A798B32D738", x => x.PositionId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EndTime = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsOvernight = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ShiftCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShiftName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTime = table.Column<TimeOnly>(type: "time(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Shifts__C0A83881E26348B8", x => x.ShiftId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Stores__3B82F1019E9B97C7", x => x.StoreId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PositionId = table.Column<int>(type: "int", nullable: false),
                    PrimaryStoreId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PinHash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Employee__7AD04F1113F33E18", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_Employees_Positions",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "PositionId");
                    table.ForeignKey(
                        name: "FK_Employees_Stores",
                        column: x => x.PrimaryStoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId");
                    table.ForeignKey(
                        name: "FK_Employees_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    AttendanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    CheckInMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckInTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CheckOutMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckOutTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Present")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Attendan__8B69261CBF2CADA3", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendance_Assignment",
                        column: x => x.AssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "AssignmentId");
                    table.ForeignKey(
                        name: "FK_Attendance_Employee",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_Attendance_Store",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Notifica__20CF2E12AFE5BF0A", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Employee",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShiftHandoverSessions",
                columns: table => new
                {
                    HandoverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    ClosedBy = table.Column<int>(type: "int", nullable: true),
                    OpenedBy = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ManagerNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Open")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ShiftHan__DB2A1F81EB4E7EA3", x => x.HandoverId);
                    table.ForeignKey(
                        name: "FK_Handover_Assignment",
                        column: x => x.AssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "AssignmentId");
                    table.ForeignKey(
                        name: "FK_Handover_ClosedBy",
                        column: x => x.ClosedBy,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_Handover_OpenedBy",
                        column: x => x.OpenedBy,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_Handover_Store",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AttendanceExceptions",
                columns: table => new
                {
                    ExceptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AttendanceId = table.Column<int>(type: "int", nullable: false),
                    ReportedBy = table.Column<int>(type: "int", nullable: false),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Attendan__26981D8886A6E369", x => x.ExceptionId);
                    table.ForeignKey(
                        name: "FK_Exception_Attendance",
                        column: x => x.AttendanceId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "AttendanceId");
                    table.ForeignKey(
                        name: "FK_Exception_Reporter",
                        column: x => x.ReportedBy,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_Exception_Resolver",
                        column: x => x.ResolvedBy,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__536C85E49C6B9BC8",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_PublishedBy",
                table: "WorkSchedules",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_StoreId",
                table: "WorkSchedules",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispatch_Employee_Date",
                table: "TemporaryDispatches",
                columns: new[] { "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryDispatches_FromStoreId",
                table: "TemporaryDispatches",
                column: "FromStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryDispatches_ToStoreId",
                table: "TemporaryDispatches",
                column: "ToStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequests_AssignmentId",
                table: "ShiftSwapRequests",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequests_RequesterEmployeeId",
                table: "ShiftSwapRequests",
                column: "RequesterEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSwapRequests_TargetEmployeeId",
                table: "ShiftSwapRequests",
                column: "TargetEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_Employee_WorkDate",
                table: "ShiftAssignments",
                columns: new[] { "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ScheduleId",
                table: "ShiftAssignments",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId",
                table: "ShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_Store_WorkDate",
                table: "ShiftAssignments",
                columns: new[] { "StoreId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityHandovers_HandoverId",
                table: "SecurityHandovers",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityHandovers_SecurityEmployeeId",
                table: "SecurityHandovers",
                column: "SecurityEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandovers_CashierEmployeeId",
                table: "CashHandovers",
                column: "CashierEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashHandovers_HandoverId",
                table: "CashHandovers",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_AttendanceId",
                table: "AttendanceExceptions",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_ReportedBy",
                table: "AttendanceExceptions",
                column: "ReportedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExceptions_ResolvedBy",
                table: "AttendanceExceptions",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Employee",
                table: "AttendanceRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Store",
                table: "AttendanceRecords",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AssignmentId",
                table: "AttendanceRecords",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Position",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PrimaryStore",
                table: "Employees",
                column: "PrimaryStoreId");

            migrationBuilder.CreateIndex(
                name: "UQ__Employee__1F642548DAE225BC",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Employee",
                table: "Notifications",
                columns: new[] { "EmployeeId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "UQ__Position__83745B020B5CF3B3",
                table: "Positions",
                column: "PositionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoverSessions_AssignmentId",
                table: "ShiftHandoverSessions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoverSessions_ClosedBy",
                table: "ShiftHandoverSessions",
                column: "ClosedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoverSessions_OpenedBy",
                table: "ShiftHandoverSessions",
                column: "OpenedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandoverSessions_StoreId",
                table: "ShiftHandoverSessions",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UQ__Shifts__9377D56208114F91",
                table: "Shifts",
                column: "ShiftCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Stores__02A384F8D8EB8161",
                table: "Stores",
                column: "StoreCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashHandover_Employee",
                table: "CashHandovers",
                column: "CashierEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashHandover_Session",
                table: "CashHandovers",
                column: "HandoverId",
                principalTable: "ShiftHandoverSessions",
                principalColumn: "HandoverId");

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityHandover_Employee",
                table: "SecurityHandovers",
                column: "SecurityEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityHandover_Session",
                table: "SecurityHandovers",
                column: "HandoverId",
                principalTable: "ShiftHandoverSessions",
                principalColumn: "HandoverId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Employee",
                table: "ShiftAssignments",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Schedule",
                table: "ShiftAssignments",
                column: "ScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Shift",
                table: "ShiftAssignments",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Store",
                table: "ShiftAssignments",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwap_Assignment",
                table: "ShiftSwapRequests",
                column: "AssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "AssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwap_Requester",
                table: "ShiftSwapRequests",
                column: "RequesterEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwap_Reviewer",
                table: "ShiftSwapRequests",
                column: "ReviewedBy",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSwap_Target",
                table: "ShiftSwapRequests",
                column: "TargetEmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatch_ApprovedBy",
                table: "TemporaryDispatches",
                column: "ApprovedBy",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatch_Employee",
                table: "TemporaryDispatches",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatch_FromStore",
                table: "TemporaryDispatches",
                column: "FromStoreId",
                principalTable: "Stores",
                principalColumn: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatch_RequestedBy",
                table: "TemporaryDispatches",
                column: "RequestedBy",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dispatch_ToStore",
                table: "TemporaryDispatches",
                column: "ToStoreId",
                principalTable: "Stores",
                principalColumn: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedules_PublishedBy",
                table: "WorkSchedules",
                column: "PublishedBy",
                principalTable: "Employees",
                principalColumn: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedules_Stores",
                table: "WorkSchedules",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "StoreId");
        }
    }
}
