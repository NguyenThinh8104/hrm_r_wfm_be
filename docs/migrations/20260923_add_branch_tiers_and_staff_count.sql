-- Migration: Add branch_tiers table, add StaffCount and TierId to branches, and create headcount_import_requests
-- Date: 2026-09-23

SET NAMES utf8mb4;
SET CHARACTER SET utf8mb4;
USE `rwfm_db`;

-- 1. Bảng branch_tiers
CREATE TABLE IF NOT EXISTS `branch_tiers` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `TierName` VARCHAR(100) NOT NULL,
    `Description` TEXT NULL,
    `MinStaffCount` INT NOT NULL DEFAULT 0,
    `MaxStaffCount` INT NULL,
    `OtherConditions` TEXT NULL,
    `Conditions` TEXT NULL,
    `Benefits` TEXT NULL,
    `CreatedAt` DATETIME(6) NOT NULL,
    `UpdatedAt` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Dữ liệu mẫu khởi tạo cho branch_tiers (nếu chưa có)
INSERT INTO `branch_tiers` (`Id`, `TierName`, `Description`, `MinStaffCount`, `MaxStaffCount`, `Conditions`, `Benefits`, `CreatedAt`, `UpdatedAt`)
VALUES 
(1, 'Tier 1 - Đại Siêu Thị / Flagship', 'Chi nhánh quy mô lớn / đại siêu thị, định biên chuẩn tối đa 30 nhân sự', 20, 30, 'Diện tích sàn > 250m², doanh thu mục tiêu > 500tr/tháng, tối thiểu 20 nhân sự trực ca.', 'Được trang bị 2-4 máy Kiosk iPad, ưu tiên tiếp nhận nhân sự điều động tăng cường vào ca cao điểm, định biên chuẩn 30 nhân sự, phê duyệt nhanh các đề xuất tăng ca.', NOW(6), NOW(6)),
(2, 'Tier 2 - Siêu Thị Tiêu Chuẩn / Standard', 'Chi nhánh tiêu chuẩn, định biên chuẩn tối đa 15 nhân sự', 10, 15, 'Diện tích sàn 100m² - 250m², doanh thu mục tiêu 200tr - 500tr/tháng, nhân sự từ 10 - 15 người.', 'Được cấp 1-2 máy Kiosk, định biên chuẩn 15 nhân sự, tự động lập lịch phân ca tối ưu công chuẩn 8 tiếng/ngày.', NOW(6), NOW(6)),
(3, 'Tier 3 - Cửa Hàng Tiện Lợi Mini / Compact', 'Chi nhánh nhỏ / cửa hàng tiện lợi, định biên chuẩn tối đa 8 nhân sự', 4, 8, 'Diện tích sàn < 100m², nhân sự từ 4 - 8 người, trực ca theo mô hình kiêm nhiệm thu ngân & bán hàng.', 'Cấu hình Kiosk siêu tốc, quản lý linh hoạt ca gãy và chấm công mã OTP cá nhân linh động.', NOW(6), NOW(6))
ON DUPLICATE KEY UPDATE `TierName` = VALUES(`TierName`), `MinStaffCount` = VALUES(`MinStaffCount`), `MaxStaffCount` = VALUES(`MaxStaffCount`), `Conditions` = VALUES(`Conditions`), `Benefits` = VALUES(`Benefits`);

-- 3. Bổ sung các cột mới vào bảng branches
-- Cột BranchTier (nếu chưa có)
SET @col_tier = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'rwfm_db' AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'BranchTier');
SET @sql_tier = IF(@col_tier = 0, 'ALTER TABLE `branches` ADD COLUMN `BranchTier` INT NOT NULL DEFAULT 2 AFTER `GeofenceRadiusMeters`;', 'SELECT 1;');
PREPARE stmt1 FROM @sql_tier;
EXECUTE stmt1;
DEALLOCATE PREPARE stmt1;

-- Cột StaffCount
SET @col_staff = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'rwfm_db' AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'StaffCount');
SET @sql_staff = IF(@col_staff = 0, 'ALTER TABLE `branches` ADD COLUMN `StaffCount` INT NOT NULL DEFAULT 0 AFTER `BranchTier`;', 'SELECT 1;');
PREPARE stmt2 FROM @sql_staff;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;

-- Cột TierId
SET @col_tierid = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'rwfm_db' AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'TierId');
SET @sql_tierid = IF(@col_tierid = 0, 'ALTER TABLE `branches` ADD COLUMN `TierId` INT NULL AFTER `StaffCount`;', 'SELECT 1;');
PREPARE stmt3 FROM @sql_tierid;
EXECUTE stmt3;
DEALLOCATE PREPARE stmt3;

-- Khóa ngoại branches.TierId -> branch_tiers.Id
SET @fk_tier = (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = 'rwfm_db' AND TABLE_NAME = 'branches' AND CONSTRAINT_NAME = 'FK_branches_branch_tiers_TierId');
SET @sql_fktier = IF(@fk_tier = 0, 'ALTER TABLE `branches` ADD CONSTRAINT `FK_branches_branch_tiers_TierId` FOREIGN KEY (`TierId`) REFERENCES `branch_tiers` (`Id`) ON DELETE SET NULL;', 'SELECT 1;');
PREPARE stmt4 FROM @sql_fktier;
EXECUTE stmt4;
DEALLOCATE PREPARE stmt4;

-- 4. Bảng headcount_import_requests (nếu chưa tạo)
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
