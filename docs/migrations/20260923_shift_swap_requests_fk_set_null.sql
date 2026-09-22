-- Migration: Update foreign keys of shift_swap_requests to ON DELETE SET NULL
-- Date: 2026-09-23

-- 1. Drop existing FKs
ALTER TABLE `shift_swap_requests` 
    DROP FOREIGN KEY `FK_shift_swap_requests_shift_assignments_RequestingAssignmentId`;

ALTER TABLE `shift_swap_requests` 
    DROP FOREIGN KEY `FK_shift_swap_requests_shift_assignments_TargetAssignmentId`;

-- 2. Re-create FKs with ON DELETE SET NULL
ALTER TABLE `shift_swap_requests` 
    ADD CONSTRAINT `FK_shift_swap_requests_shift_assignments_RequestingAssignmentId` 
    FOREIGN KEY (`RequestingAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE SET NULL;

ALTER TABLE `shift_swap_requests` 
    ADD CONSTRAINT `FK_shift_swap_requests_shift_assignments_TargetAssignmentId` 
    FOREIGN KEY (`TargetAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE SET NULL;
