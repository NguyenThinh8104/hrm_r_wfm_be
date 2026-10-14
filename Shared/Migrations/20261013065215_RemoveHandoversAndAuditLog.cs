using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHandoversAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_handovers");

            migrationBuilder.DropTable(
                name: "security_handovers");

            migrationBuilder.DropTable(
                name: "system_audit_logs");

            migrationBuilder.DropTable(
                name: "shift_handovers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shift_handovers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ScheduleId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ShiftLeaderId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GeneralNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoverStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                name: "system_audit_logs",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ActorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Action = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValues = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValues = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    TargetTable = table.Column<string>(type: "longtext", nullable: false)
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
                name: "cash_handovers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CashierId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ShiftHandoverId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ClosingActualCash = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DiscrepancyReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpeningCash = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SystemExpectedCash = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_handovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cash_handovers_shift_handovers_ShiftHandoverId",
                        column: x => x.ShiftHandoverId,
                        principalTable: "shift_handovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cash_handovers_users_CashierId",
                        column: x => x.CashierId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "security_handovers",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SecurityGuardId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ShiftHandoverId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsShutterClosed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsWarehouseLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OvernightVehicleCount = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    SecurityNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_handovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_handovers_shift_handovers_ShiftHandoverId",
                        column: x => x.ShiftHandoverId,
                        principalTable: "shift_handovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_security_handovers_users_SecurityGuardId",
                        column: x => x.SecurityGuardId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_CashierId",
                table: "cash_handovers",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_ShiftHandoverId",
                table: "cash_handovers",
                column: "ShiftHandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_security_handovers_SecurityGuardId",
                table: "security_handovers",
                column: "SecurityGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_security_handovers_ShiftHandoverId",
                table: "security_handovers",
                column: "ShiftHandoverId");

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
                name: "IX_system_audit_logs_ActorId",
                table: "system_audit_logs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_system_audit_logs_Timestamp_Action",
                table: "system_audit_logs",
                columns: new[] { "Timestamp", "Action" });
        }
    }
}
