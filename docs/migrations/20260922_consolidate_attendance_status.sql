-- ====================================================================================
-- Script Migration: Tinh gọn bảng attendance_logs & hợp nhất trạng thái điểm danh
-- Ngày tạo: 2026-09-22
-- Nội dung:
--   1. Chuẩn hóa dữ liệu cũ trong attendance_logs sang mã số Enum mới (1-5)
--   2. Đổi kiểu dữ liệu cột Status từ VARCHAR/LONGTEXT sang TINYINT UNSIGNED (1 byte)
--   3. Xóa bỏ cột OpeningFloatCash và cột IsLate (nếu có)
-- ====================================================================================

-- 1. Chuẩn hóa dữ liệu cũ sang mã số Enum mới
UPDATE `attendance_logs` SET `Status` = '1' WHERE `Status` = 'PENDING' OR `Status` IS NULL OR `Status` = '';
UPDATE `attendance_logs` SET `Status` = '2' WHERE `Status` = 'COMPLETED' OR `Status` = 'PRESENT';

-- 2. Đổi kiểu cột Status thành TINYINT UNSIGNED (1 byte) với giá trị mặc định là 1 (PENDING)
ALTER TABLE `attendance_logs` 
MODIFY COLUMN `Status` TINYINT UNSIGNED NOT NULL DEFAULT 1 
COMMENT '1: PENDING, 2: PRESENT, 3: LATE, 4: COMPLETED, 5: COMPLETED_LATE';

-- 3. Xóa bỏ cột OpeningFloatCash (nếu tồn tại)
ALTER TABLE `attendance_logs` DROP COLUMN `OpeningFloatCash`;

-- 4. Nếu trước đó có tạo cột IsLate thủ công, xóa bỏ để tránh dư thừa (an toàn khi chạy)
-- (Nếu MySQL báo lỗi Unknown column 'IsLate' thì có thể bỏ qua dòng này)
-- ALTER TABLE `attendance_logs` DROP COLUMN `IsLate`;
