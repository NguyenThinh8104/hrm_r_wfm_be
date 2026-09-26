-- ====================================================================================
-- Script Migration: Bổ sung trạng thái chi tiết Check-in, Check-out & Số phút đi muộn / về sớm
-- Ngày tạo: 2026-09-24
-- Database: rwfm_db
-- Bảng áp dụng: attendance_logs
-- Nội dung:
--   1. Thêm cột CheckInStatus (TINYINT UNSIGNED NOT NULL DEFAULT 1)
--      (1: PENDING, 2: ON_TIME, 3: LATE, 4: EARLY)
--   2. Thêm cột CheckOutStatus (TINYINT UNSIGNED NULL)
--      (1: PENDING, 2: ON_TIME, 3: EARLY_LEAVE, 4: LATE_LEAVE)
--   3. Thêm cột LateMinutes (INT NULL) - Số phút đi muộn
--   4. Thêm cột EarlyLeaveMinutes (INT NULL) - Số phút về sớm
--   5. Đồng bộ dữ liệu cũ (CheckInStatus và CheckOutStatus từ Status hiện hành)
-- ====================================================================================

USE `rwfm_db`;

-- 1. Thêm cột CheckInStatus (nếu chưa có)
SET @col_checkin = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                      AND TABLE_NAME = 'attendance_logs' 
                      AND COLUMN_NAME = 'CheckInStatus');
SET @sql_checkin = IF(@col_checkin = 0, 
    'ALTER TABLE `attendance_logs` ADD COLUMN `CheckInStatus` TINYINT UNSIGNED NOT NULL DEFAULT 1 COMMENT ''1: PENDING, 2: ON_TIME, 3: LATE, 4: EARLY'' AFTER `Status`;', 
    'SELECT 1;');
PREPARE stmt_checkin FROM @sql_checkin;
EXECUTE stmt_checkin;
DEALLOCATE PREPARE stmt_checkin;

-- 2. Thêm cột CheckOutStatus (nếu chưa có)
SET @col_checkout = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                     WHERE TABLE_SCHEMA = DATABASE() 
                       AND TABLE_NAME = 'attendance_logs' 
                       AND COLUMN_NAME = 'CheckOutStatus');
SET @sql_checkout = IF(@col_checkout = 0, 
    'ALTER TABLE `attendance_logs` ADD COLUMN `CheckOutStatus` TINYINT UNSIGNED NULL COMMENT ''1: PENDING, 2: ON_TIME, 3: EARLY_LEAVE, 4: LATE_LEAVE'' AFTER `CheckInStatus`;', 
    'SELECT 1;');
PREPARE stmt_checkout FROM @sql_checkout;
EXECUTE stmt_checkout;
DEALLOCATE PREPARE stmt_checkout;

-- 3. Thêm cột LateMinutes (nếu chưa có)
SET @col_late = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                 WHERE TABLE_SCHEMA = DATABASE() 
                   AND TABLE_NAME = 'attendance_logs' 
                   AND COLUMN_NAME = 'LateMinutes');
SET @sql_late = IF(@col_late = 0, 
    'ALTER TABLE `attendance_logs` ADD COLUMN `LateMinutes` INT NULL COMMENT ''Số phút đi muộn lúc Check-in'' AFTER `CheckOutStatus`;', 
    'SELECT 1;');
PREPARE stmt_late FROM @sql_late;
EXECUTE stmt_late;
DEALLOCATE PREPARE stmt_late;

-- 4. Thêm cột EarlyLeaveMinutes (nếu chưa có)
SET @col_earlyleave = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                       WHERE TABLE_SCHEMA = DATABASE() 
                         AND TABLE_NAME = 'attendance_logs' 
                         AND COLUMN_NAME = 'EarlyLeaveMinutes');
SET @sql_earlyleave = IF(@col_earlyleave = 0, 
    'ALTER TABLE `attendance_logs` ADD COLUMN `EarlyLeaveMinutes` INT NULL COMMENT ''Số phút về sớm lúc Check-out'' AFTER `LateMinutes`;', 
    'SELECT 1;');
PREPARE stmt_earlyleave FROM @sql_earlyleave;
EXECUTE stmt_earlyleave;
DEALLOCATE PREPARE stmt_earlyleave;

-- 5. Đồng bộ dữ liệu cũ (nếu có)
-- Nếu Status = 2 (PRESENT) hoặc 4 (COMPLETED) -> CheckInStatus = 2 (ON_TIME)
UPDATE `attendance_logs` SET `CheckInStatus` = 2 WHERE `Status` IN (2, 4);

-- Nếu Status = 3 (LATE) hoặc 5 (COMPLETED_LATE) -> CheckInStatus = 3 (LATE)
UPDATE `attendance_logs` SET `CheckInStatus` = 3 WHERE `Status` IN (3, 5);

-- Nếu đã có CheckOutTime -> Gán CheckOutStatus = 2 (ON_TIME) cho các ca cũ
UPDATE `attendance_logs` SET `CheckOutStatus` = 2 WHERE `CheckOutTime` IS NOT NULL AND `CheckOutStatus` IS NULL;

-- 6. Ghi nhận vào bảng __EFMigrationsHistory (nếu sử dụng EF Core)
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260924085500_AddCheckInAndCheckOutStatusToAttendanceLogs', '8.0.11'
WHERE NOT EXISTS (
    SELECT 1 FROM `__EFMigrationsHistory` 
    WHERE `MigrationId` = '20260924085500_AddCheckInAndCheckOutStatusToAttendanceLogs'
);
