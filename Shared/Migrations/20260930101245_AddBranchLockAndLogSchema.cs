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
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `__ef_temp_migration_helper`;
CREATE PROCEDURE `__ef_temp_migration_helper`()
BEGIN
    -- Drop FK if exists
    IF EXISTS (
        SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_swap_requests' AND CONSTRAINT_NAME = 'FK_shift_swap_requests_shift_assignments_TargetAssignmentId' AND CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) THEN
        ALTER TABLE `shift_swap_requests` DROP FOREIGN KEY `FK_shift_swap_requests_shift_assignments_TargetAssignmentId`;
    END IF;

    -- Add columns to branches if not exist
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'LockReason') THEN
        ALTER TABLE `branches` ADD `LockReason` varchar(1000) CHARACTER SET utf8mb4 NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'LockedAt') THEN
        ALTER TABLE `branches` ADD `LockedAt` datetime(6) NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'LockedBy') THEN
        ALTER TABLE `branches` ADD `LockedBy` varchar(255) CHARACTER SET utf8mb4 NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'RowVersion') THEN
        ALTER TABLE `branches` ADD `RowVersion` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'StaffCount') THEN
        ALTER TABLE `branches` ADD `StaffCount` int NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'TierId') THEN
        ALTER TABLE `branches` ADD `TierId` int NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'UnlockedAt') THEN
        ALTER TABLE `branches` ADD `UnlockedAt` datetime(6) NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'UnlockedBy') THEN
        ALTER TABLE `branches` ADD `UnlockedBy` varchar(255) CHARACTER SET utf8mb4 NULL;
    END IF;

    -- Create tables if not exist
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branch_lock_logs') THEN
        CREATE TABLE `branch_lock_logs` (
            `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
            `BranchId` bigint unsigned NOT NULL,
            `Action` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
            `Reason` varchar(1000) CHARACTER SET utf8mb4 NULL,
            `PerformedBy` varchar(255) CHARACTER SET utf8mb4 NULL,
            `PerformedAt` datetime(6) NOT NULL,
            `AffectedEmployeeCount` int NOT NULL,
            `StaffHandlingMode` varchar(100) CHARACTER SET utf8mb4 NULL,
            `FutureShiftHandling` varchar(100) CHARACTER SET utf8mb4 NULL,
            `TransferredToBranchId` bigint unsigned NULL,
            CONSTRAINT `PK_branch_lock_logs` PRIMARY KEY (`Id`),
            CONSTRAINT `FK_branch_lock_logs_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_branch_lock_logs_branches_TransferredToBranchId` FOREIGN KEY (`TransferredToBranchId`) REFERENCES `branches` (`Id`) ON DELETE SET NULL
        ) CHARACTER SET=utf8mb4;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branch_tiers') THEN
        CREATE TABLE `branch_tiers` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `TierName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `Description` longtext CHARACTER SET utf8mb4 NULL,
            `MinStaffCount` int NOT NULL,
            `MaxStaffCount` int NULL,
            `OtherConditions` longtext CHARACTER SET utf8mb4 NULL,
            `Conditions` longtext CHARACTER SET utf8mb4 NULL,
            `Benefits` longtext CHARACTER SET utf8mb4 NULL,
            `CreatedAt` datetime(6) NOT NULL,
            `UpdatedAt` datetime(6) NOT NULL,
            CONSTRAINT `PK_branch_tiers` PRIMARY KEY (`Id`)
        ) CHARACTER SET=utf8mb4;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests') THEN
        CREATE TABLE `headcount_import_requests` (
            `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
            `BranchId` bigint unsigned NOT NULL,
            `RequestedBy` bigint unsigned NOT NULL,
            `FilePath` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
            `FileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `Status` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'PENDING',
            `TotalRequested` int NOT NULL,
            `ApprovedQuantity` int NOT NULL,
            `TotalApproved` int NOT NULL,
            `AdditionalQuantity` int NOT NULL,
            `Reason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
            `AdminNotes` varchar(1000) CHARACTER SET utf8mb4 NULL,
            `ReviewedBy` bigint unsigned NULL,
            `ReviewedAt` datetime(6) NULL,
            `ExpiresAt` datetime(6) NULL,
            `CreatedAt` datetime(6) NOT NULL,
            `UpdatedAt` datetime(6) NOT NULL,
            CONSTRAINT `PK_headcount_import_requests` PRIMARY KEY (`Id`),
            CONSTRAINT `FK_headcount_import_requests_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
            CONSTRAINT `FK_headcount_import_requests_users_RequestedBy` FOREIGN KEY (`RequestedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
            CONSTRAINT `FK_headcount_import_requests_users_ReviewedBy` FOREIGN KEY (`ReviewedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
        ) CHARACTER SET=utf8mb4;
    END IF;

    -- Create indexes if not exist
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND INDEX_NAME = 'IX_branches_TierId') THEN
        CREATE INDEX `IX_branches_TierId` ON `branches` (`TierId`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branch_lock_logs' AND INDEX_NAME = 'IX_branch_lock_logs_BranchId') THEN
        CREATE INDEX `IX_branch_lock_logs_BranchId` ON `branch_lock_logs` (`BranchId`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branch_lock_logs' AND INDEX_NAME = 'IX_branch_lock_logs_PerformedAt') THEN
        CREATE INDEX `IX_branch_lock_logs_PerformedAt` ON `branch_lock_logs` (`PerformedAt`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branch_lock_logs' AND INDEX_NAME = 'IX_branch_lock_logs_TransferredToBranchId') THEN
        CREATE INDEX `IX_branch_lock_logs_TransferredToBranchId` ON `branch_lock_logs` (`TransferredToBranchId`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests' AND INDEX_NAME = 'IX_headcount_import_requests_BranchId') THEN
        CREATE INDEX `IX_headcount_import_requests_BranchId` ON `headcount_import_requests` (`BranchId`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests' AND INDEX_NAME = 'IX_headcount_import_requests_BranchId_Status') THEN
        CREATE INDEX `IX_headcount_import_requests_BranchId_Status` ON `headcount_import_requests` (`BranchId`, `Status`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests' AND INDEX_NAME = 'IX_headcount_import_requests_RequestedBy') THEN
        CREATE INDEX `IX_headcount_import_requests_RequestedBy` ON `headcount_import_requests` (`RequestedBy`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests' AND INDEX_NAME = 'IX_headcount_import_requests_ReviewedBy') THEN
        CREATE INDEX `IX_headcount_import_requests_ReviewedBy` ON `headcount_import_requests` (`ReviewedBy`);
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'headcount_import_requests' AND INDEX_NAME = 'IX_headcount_import_requests_Status') THEN
        CREATE INDEX `IX_headcount_import_requests_Status` ON `headcount_import_requests` (`Status`);
    END IF;

    -- Add Foreign keys if not exist
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND CONSTRAINT_NAME = 'FK_branches_branch_tiers_TierId') THEN
        ALTER TABLE `branches` ADD CONSTRAINT `FK_branches_branch_tiers_TierId` FOREIGN KEY (`TierId`) REFERENCES `branch_tiers` (`Id`) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_swap_requests' AND CONSTRAINT_NAME = 'FK_shift_swap_requests_shift_assignments_TargetAssignmentId') THEN
        ALTER TABLE `shift_swap_requests` ADD CONSTRAINT `FK_shift_swap_requests_shift_assignments_TargetAssignmentId` FOREIGN KEY (`TargetAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE SET NULL;
    END IF;
END;
CALL `__ef_temp_migration_helper`();
DROP PROCEDURE IF EXISTS `__ef_temp_migration_helper`;
");
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
