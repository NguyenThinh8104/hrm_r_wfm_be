-- Migration: Create headcount_import_requests table
-- Date: 2026-09-23
-- Reference: Shared/Migrations/20260922201000_AddHeadcountImportRequestSchema.cs

CREATE TABLE IF NOT EXISTS `headcount_import_requests` (
    `Id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `BranchId` BIGINT UNSIGNED NOT NULL,
    `RequestedBy` BIGINT UNSIGNED NOT NULL,
    `FilePath` VARCHAR(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `FileName` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Status` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
    `TotalRequested` INT NOT NULL,
    `ApprovedQuantity` INT NOT NULL DEFAULT 0,
    `TotalApproved` INT NOT NULL DEFAULT 0,
    `AdditionalQuantity` INT NOT NULL DEFAULT 0,
    `Reason` VARCHAR(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `AdminNotes` VARCHAR(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `ReviewedBy` BIGINT UNSIGNED NULL,
    `ReviewedAt` DATETIME(6) NULL,
    `ExpiresAt` DATETIME(6) NULL,
    `CreatedAt` DATETIME(6) NOT NULL,
    `UpdatedAt` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_headcount_import_requests_branches_BranchId`
        FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_headcount_import_requests_users_RequestedBy`
        FOREIGN KEY (`RequestedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_headcount_import_requests_users_ReviewedBy`
        FOREIGN KEY (`ReviewedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_headcount_import_requests_BranchId` ON `headcount_import_requests` (`BranchId`);
CREATE INDEX `IX_headcount_import_requests_Status` ON `headcount_import_requests` (`Status`);
CREATE INDEX `IX_headcount_import_requests_BranchId_Status` ON `headcount_import_requests` (`BranchId`, `Status`);
CREATE INDEX `IX_headcount_import_requests_RequestedBy` ON `headcount_import_requests` (`RequestedBy`);
CREATE INDEX `IX_headcount_import_requests_ReviewedBy` ON `headcount_import_requests` (`ReviewedBy`);
