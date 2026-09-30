-- ============================================================
-- Fix migration: Add missing columns/tables to match
-- 20260930101245_AddBranchLockAndLogSchema
-- ============================================================
-- Chạy khi DB đã có một số cột từ migration cũ chưa được track,
-- tránh lỗi "Duplicate column name" khi chạy dotnet ef database update.
-- ============================================================

-- 1. Add UnlockedAt if not exists
SET @col_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'UnlockedAt'
);
SET @sql = IF(@col_exists = 0,
  'ALTER TABLE branches ADD COLUMN UnlockedAt datetime(6) NULL',
  'SELECT ''UnlockedAt already exists'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. Add UnlockedBy if not exists
SET @col_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND COLUMN_NAME = 'UnlockedBy'
);
SET @sql = IF(@col_exists = 0,
  'ALTER TABLE branches ADD COLUMN UnlockedBy varchar(255) NULL',
  'SELECT ''UnlockedBy already exists'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3. Create branch_lock_logs if not exists
CREATE TABLE IF NOT EXISTS branch_lock_logs (
  Id bigint unsigned NOT NULL AUTO_INCREMENT,
  BranchId bigint unsigned NOT NULL,
  Action varchar(50) NOT NULL,
  Reason varchar(1000) NULL,
  PerformedBy varchar(255) NULL,
  PerformedAt datetime(6) NOT NULL,
  AffectedEmployeeCount int NOT NULL DEFAULT 0,
  StaffHandlingMode varchar(100) NULL,
  FutureShiftHandling varchar(100) NULL,
  TransferredToBranchId bigint unsigned NULL,
  PRIMARY KEY (Id),
  KEY IX_branch_lock_logs_BranchId (BranchId),
  KEY IX_branch_lock_logs_PerformedAt (PerformedAt),
  KEY IX_branch_lock_logs_TransferredToBranchId (TransferredToBranchId),
  CONSTRAINT FK_branch_lock_logs_branches_BranchId
    FOREIGN KEY (BranchId) REFERENCES branches (Id) ON DELETE CASCADE,
  CONSTRAINT FK_branch_lock_logs_branches_TransferredToBranchId
    FOREIGN KEY (TransferredToBranchId) REFERENCES branches (Id) ON DELETE SET NULL
) CHARACTER SET utf8mb4;

-- 4. Create headcount_import_requests if not exists
CREATE TABLE IF NOT EXISTS headcount_import_requests (
  Id bigint unsigned NOT NULL AUTO_INCREMENT,
  BranchId bigint unsigned NOT NULL,
  RequestedBy bigint unsigned NOT NULL,
  FilePath varchar(500) NOT NULL,
  FileName varchar(255) NOT NULL,
  Status varchar(50) NOT NULL DEFAULT 'PENDING',
  TotalRequested int NOT NULL DEFAULT 0,
  ApprovedQuantity int NOT NULL DEFAULT 0,
  TotalApproved int NOT NULL DEFAULT 0,
  AdditionalQuantity int NOT NULL DEFAULT 0,
  Reason varchar(1000) NOT NULL DEFAULT '',
  AdminNotes varchar(1000) NULL,
  ReviewedBy bigint unsigned NULL,
  ReviewedAt datetime(6) NULL,
  ExpiresAt datetime(6) NULL,
  CreatedAt datetime(6) NOT NULL,
  UpdatedAt datetime(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_headcount_import_requests_BranchId (BranchId),
  KEY IX_headcount_import_requests_BranchId_Status (BranchId, Status),
  KEY IX_headcount_import_requests_RequestedBy (RequestedBy),
  KEY IX_headcount_import_requests_ReviewedBy (ReviewedBy),
  KEY IX_headcount_import_requests_Status (Status),
  CONSTRAINT FK_headcount_import_requests_branches_BranchId
    FOREIGN KEY (BranchId) REFERENCES branches (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_headcount_import_requests_users_RequestedBy
    FOREIGN KEY (RequestedBy) REFERENCES users (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_headcount_import_requests_users_ReviewedBy
    FOREIGN KEY (ReviewedBy) REFERENCES users (Id) ON DELETE SET NULL
) CHARACTER SET utf8mb4;

-- 5. Fix shift_swap_requests FK: drop Restrict, re-add SetNull
SET @fk_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'shift_swap_requests'
    AND CONSTRAINT_NAME = 'FK_shift_swap_requests_shift_assignments_TargetAssignmentId'
);
SET @sql = IF(@fk_exists > 0,
  'ALTER TABLE shift_swap_requests DROP FOREIGN KEY FK_shift_swap_requests_shift_assignments_TargetAssignmentId',
  'SELECT ''FK not found, skip drop'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @fk_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'shift_swap_requests'
    AND CONSTRAINT_NAME = 'FK_shift_swap_requests_shift_assignments_TargetAssignmentId'
);
SET @sql = IF(@fk_exists = 0,
  'ALTER TABLE shift_swap_requests ADD CONSTRAINT FK_shift_swap_requests_shift_assignments_TargetAssignmentId FOREIGN KEY (TargetAssignmentId) REFERENCES shift_assignments (Id) ON DELETE SET NULL',
  'SELECT ''FK already exists'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 6. Add IX_branches_TierId index if not exists
SET @idx_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'branches' AND INDEX_NAME = 'IX_branches_TierId'
);
SET @sql = IF(@idx_exists = 0,
  'ALTER TABLE branches ADD INDEX IX_branches_TierId (TierId)',
  'SELECT ''Index already exists'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 7. Add FK branches -> branch_tiers if not exists
SET @fk_exists = (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'branches'
    AND CONSTRAINT_NAME = 'FK_branches_branch_tiers_TierId'
);
SET @sql = IF(@fk_exists = 0,
  'ALTER TABLE branches ADD CONSTRAINT FK_branches_branch_tiers_TierId FOREIGN KEY (TierId) REFERENCES branch_tiers (Id) ON DELETE SET NULL',
  'SELECT ''FK already exists'' AS info'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 8. Mark all pending migrations as applied (safe with INSERT IGNORE)
INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
  ('20260920110000_AddBranchTierToBranches',                          '8.0.0'),
  ('20260921142834_AddAttendanceLogStatus',                           '8.0.0'),
  ('20260922072855_ConsolidateAttendanceStatusAndDropUnusedColumns',  '8.0.0'),
  ('20260930101245_AddBranchLockAndLogSchema',                        '8.0.0');

-- Verify
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
