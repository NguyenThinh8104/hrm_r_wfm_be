using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchLockAndLogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "branches",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "branches",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "branches",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "branches",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

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

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedAt",
                table: "branches",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockedBy",
                table: "branches",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "branch_lock_logs",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PerformedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AffectedEmployeeCount = table.Column<int>(type: "int", nullable: false),
                    StaffHandlingMode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FutureShiftHandling = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransferredToBranchId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_lock_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_lock_logs_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_branch_lock_logs_branches_TransferredToBranchId",
                        column: x => x.TransferredToBranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.CreateTable(
                name: "headcount_import_requests",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BranchId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    RequestedBy = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    FilePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalRequested = table.Column<int>(type: "int", nullable: false),
                    ApprovedQuantity = table.Column<int>(type: "int", nullable: false),
                    TotalApproved = table.Column<int>(type: "int", nullable: false),
                    AdditionalQuantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_headcount_import_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_headcount_import_requests_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_headcount_import_requests_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_headcount_import_requests_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TierId",
                table: "branches",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_lock_logs_BranchId",
                table: "branch_lock_logs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_lock_logs_PerformedAt",
                table: "branch_lock_logs",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_branch_lock_logs_TransferredToBranchId",
                table: "branch_lock_logs",
                column: "TransferredToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_headcount_import_requests_BranchId",
                table: "headcount_import_requests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_headcount_import_requests_BranchId_Status",
                table: "headcount_import_requests",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_headcount_import_requests_RequestedBy",
                table: "headcount_import_requests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_headcount_import_requests_ReviewedBy",
                table: "headcount_import_requests",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_headcount_import_requests_Status",
                table: "headcount_import_requests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_branch_tiers_TierId",
                table: "branches",
                column: "TierId",
                principalTable: "branch_tiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_branch_tiers_TierId",
                table: "branches");

            migrationBuilder.DropForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests");

            migrationBuilder.DropTable(
                name: "branch_lock_logs");

            migrationBuilder.DropTable(
                name: "branch_tiers");

            migrationBuilder.DropTable(
                name: "headcount_import_requests");

            migrationBuilder.DropIndex(
                name: "IX_branches_TierId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "StaffCount",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "TierId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "UnlockedAt",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "UnlockedBy",
                table: "branches");

            migrationBuilder.AddForeignKey(
                name: "FK_shift_swap_requests_shift_assignments_TargetAssignmentId",
                table: "shift_swap_requests",
                column: "TargetAssignmentId",
                principalTable: "shift_assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
