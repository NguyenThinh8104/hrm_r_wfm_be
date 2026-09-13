-- ==============================================================================
-- R-WFM (Retail Workforce Management) Database Schema & Initial Seed Script
-- Modules: UC 1.2 (Branch & Kiosk) and UC 1.3 (Shift Master Templates)
-- ==============================================================================

CREATE DATABASE IF NOT EXISTS `rwfm_db` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `rwfm_db`;

-- 1. Bảng branches (Quản lý danh mục Chi nhánh cửa hàng - UC 1.2)
CREATE TABLE IF NOT EXISTS `branches` (
    `Id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Code` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Name` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Address` VARCHAR(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Phone` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `KioskAllowedIp` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `KioskAllowedBrowser` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `Status` VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE', -- ACTIVE / INACTIVE
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_branches_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Bảng kiosks / branch_kiosks (Quản lý thiết bị & cấu hình trạm Kiosk quầy - UC 1.2)
CREATE TABLE IF NOT EXISTS `kiosks` (
    `Id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `BranchId` BIGINT UNSIGNED NOT NULL,
    `KioskCode` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `DeviceName` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `IpWhitelist` VARCHAR(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `KioskToken` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `UserAgentPattern` VARCHAR(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `Status` VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE', -- ACTIVE / BLOCKED / INACTIVE
    `LastPingAt` DATETIME(6) NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_kiosks_KioskCode` (`KioskCode`),
    UNIQUE KEY `UX_kiosks_KioskToken` (`KioskToken`),
    KEY `IX_kiosks_BranchId` (`BranchId`),
    CONSTRAINT `FK_kiosks_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Bảng shift_templates (Chuẩn hóa bộ khung ca mẫu toàn hệ thống - UC 1.3)
CREATE TABLE IF NOT EXISTS `shift_templates` (
    `Id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Code` VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Name` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `Description` TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `StartTime` TIME NOT NULL,
    `EndTime` TIME NOT NULL,
    `BreakMinutes` INT UNSIGNED NOT NULL DEFAULT 0,
    `IsOvernight` TINYINT(1) NOT NULL DEFAULT 0,
    `Status` VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE', -- ACTIVE / INACTIVE
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_shift_templates_Code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==============================================================================
-- SEED DATA CHUẨN
-- ==============================================================================

-- 1. Seed Branches
INSERT INTO `branches` (`Id`, `Code`, `Name`, `Address`, `Phone`, `Status`, `CreatedAt`, `UpdatedAt`)
VALUES 
(1, 'CH01', 'Cửa hàng Tiện lợi Chi nhánh Cầu Giấy', '123 Cầu Giấy, Q. Cầu Giấy, Hà Nội', '02438888888', 'ACTIVE', NOW(), NOW()),
(2, 'CH02', 'Cửa hàng Tiện lợi Chi nhánh Lê Văn Việt', '456 Lê Văn Việt, TP. Thủ Đức, TP. Hồ Chí Minh', '02839999999', 'ACTIVE', NOW(), NOW())
ON DUPLICATE KEY UPDATE `Name` = VALUES(`Name`), `Address` = VALUES(`Address`), `Phone` = VALUES(`Phone`);

-- 2. Seed Kiosks
INSERT INTO `kiosks` (`Id`, `BranchId`, `KioskCode`, `DeviceName`, `IpWhitelist`, `KioskToken`, `UserAgentPattern`, `Status`, `CreatedAt`, `UpdatedAt`)
VALUES 
(1, 1, 'CH01-POS01', 'Máy Kiosk Cầu Giấy 01', '192.168.1.100,127.0.0.1,::1', 'ksk_tok_demo_pos01', 'Chrome,Edge,KioskBrowser', 'ACTIVE', NOW(), NOW())
ON DUPLICATE KEY UPDATE `DeviceName` = VALUES(`DeviceName`), `IpWhitelist` = VALUES(`IpWhitelist`);

-- 3. Seed Shift Master Templates
INSERT INTO `shift_templates` (`Id`, `Code`, `Name`, `Description`, `StartTime`, `EndTime`, `BreakMinutes`, `IsOvernight`, `Status`, `CreatedAt`, `UpdatedAt`)
VALUES 
(1, 'CA_SANG', 'Ca Sáng (06:00 - 14:00)', 'Ca sáng tiêu chuẩn từ 06:00 đến 14:00 (nghỉ 30 phút)', '06:00:00', '14:00:00', 30, 0, 'ACTIVE', NOW(), NOW()),
(2, 'CA_CHIEU', 'Ca Chiều (14:00 - 22:00)', 'Ca chiều tiêu chuẩn từ 14:00 đến 22:00 (nghỉ 30 phút)', '14:00:00', '22:00:00', 30, 0, 'ACTIVE', NOW(), NOW()),
(3, 'CA_DEM', 'Ca Đêm (22:00 - 06:00)', 'Ca đêm xuyên đêm từ 22:00 đến 06:00 hôm sau (nghỉ 45 phút)', '22:00:00', '06:00:00', 45, 1, 'ACTIVE', NOW(), NOW())
ON DUPLICATE KEY UPDATE `Name` = VALUES(`Name`), `StartTime` = VALUES(`StartTime`), `EndTime` = VALUES(`EndTime`), `BreakMinutes` = VALUES(`BreakMinutes`), `IsOvernight` = VALUES(`IsOvernight`);
