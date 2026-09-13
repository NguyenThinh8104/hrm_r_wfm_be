ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `branches` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `BranchCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `KioskAllowedIp` longtext CHARACTER SET utf8mb4 NULL,
    `KioskAllowedBrowser` longtext CHARACTER SET utf8mb4 NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_branches` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `roles` (
    `Id` tinyint unsigned NOT NULL,
    `RoleCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `RoleName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_roles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `shift_templates` (
    `Id` int unsigned NOT NULL AUTO_INCREMENT,
    `TemplateCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ShiftType` int NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `StartTime` time(6) NOT NULL,
    `EndTime` time(6) NOT NULL,
    `IsOvernight` tinyint(1) NOT NULL,
    `BreakDurationMinutes` int unsigned NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_shift_templates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `kiosks` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `BranchId` bigint unsigned NOT NULL,
    `KioskCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DeviceToken` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IpAddress` longtext CHARACTER SET utf8mb4 NULL,
    `AllowedIp` longtext CHARACTER SET utf8mb4 NULL,
    `AllowedBrowser` longtext CHARACTER SET utf8mb4 NULL,
    `LastBrowserUserAgent` longtext CHARACTER SET utf8mb4 NULL,
    `LastPingAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    CONSTRAINT `PK_kiosks` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_kiosks_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `users` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `EmployeeCode` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `FullName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Phone` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` longtext CHARACTER SET utf8mb4 NOT NULL,
    `KioskPinHash` longtext CHARACTER SET utf8mb4 NULL,
    `RoleId` tinyint unsigned NOT NULL,
    `EmploymentType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HomeBranchId` bigint unsigned NULL,
    `Status` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_users` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_users_branches_HomeBranchId` FOREIGN KEY (`HomeBranchId`) REFERENCES `branches` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_users_roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `kiosk_activation_codes` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `BranchId` bigint unsigned NOT NULL,
    `KioskName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Code` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `GeneratedBy` bigint unsigned NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `IsUsed` tinyint(1) NOT NULL,
    `CreatedKioskId` bigint unsigned NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_kiosk_activation_codes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_kiosk_activation_codes_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_kiosk_activation_codes_users_GeneratedBy` FOREIGN KEY (`GeneratedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `monthly_timesheets` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `BranchId` bigint unsigned NOT NULL,
    `PeriodMonth` tinyint unsigned NOT NULL,
    `PeriodYear` smallint unsigned NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LockedBy` bigint unsigned NULL,
    `LockedAt` datetime(6) NULL,
    `ExportedAt` datetime(6) NULL,
    CONSTRAINT `PK_monthly_timesheets` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_monthly_timesheets_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_monthly_timesheets_users_LockedBy` FOREIGN KEY (`LockedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `system_audit_logs` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `ActorId` bigint unsigned NOT NULL,
    `Action` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `TargetTable` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TargetId` bigint unsigned NOT NULL,
    `OldValues` longtext CHARACTER SET utf8mb4 NULL,
    `NewValues` longtext CHARACTER SET utf8mb4 NULL,
    `IpAddress` longtext CHARACTER SET utf8mb4 NULL,
    `Timestamp` datetime(6) NOT NULL,
    CONSTRAINT `PK_system_audit_logs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_system_audit_logs_users_ActorId` FOREIGN KEY (`ActorId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `temporary_dispatches` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `SourceBranchId` bigint unsigned NOT NULL,
    `TargetBranchId` bigint unsigned NOT NULL,
    `UserId` bigint unsigned NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `RequestedBy` bigint unsigned NOT NULL,
    `ApprovedBy` bigint unsigned NULL,
    `Status` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_temporary_dispatches` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_temporary_dispatches_branches_SourceBranchId` FOREIGN KEY (`SourceBranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_temporary_dispatches_branches_TargetBranchId` FOREIGN KEY (`TargetBranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_temporary_dispatches_users_ApprovedBy` FOREIGN KEY (`ApprovedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_temporary_dispatches_users_RequestedBy` FOREIGN KEY (`RequestedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_temporary_dispatches_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `work_schedules` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `BranchId` bigint unsigned NOT NULL,
    `ShiftTemplateId` int unsigned NOT NULL,
    `WorkDate` date NOT NULL,
    `RequiredCashier` tinyint unsigned NOT NULL,
    `RequiredSales` tinyint unsigned NOT NULL,
    `RequiredSecurity` tinyint unsigned NOT NULL,
    `Status` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedBy` bigint unsigned NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_work_schedules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_work_schedules_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_work_schedules_shift_templates_ShiftTemplateId` FOREIGN KEY (`ShiftTemplateId`) REFERENCES `shift_templates` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_work_schedules_users_CreatedBy` FOREIGN KEY (`CreatedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `shift_assignments` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `ScheduleId` bigint unsigned NOT NULL,
    `UserId` bigint unsigned NOT NULL,
    `AssignedRoleId` tinyint unsigned NOT NULL,
    `AssignmentType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_shift_assignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_shift_assignments_roles_AssignedRoleId` FOREIGN KEY (`AssignedRoleId`) REFERENCES `roles` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_shift_assignments_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_shift_assignments_work_schedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `work_schedules` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `shift_handovers` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `ScheduleId` bigint unsigned NOT NULL,
    `ShiftLeaderId` bigint unsigned NOT NULL,
    `HandoverStatus` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SignedAt` datetime(6) NULL,
    `GeneralNotes` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_shift_handovers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_shift_handovers_users_ShiftLeaderId` FOREIGN KEY (`ShiftLeaderId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_shift_handovers_work_schedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `work_schedules` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `attendance_logs` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `AssignmentId` bigint unsigned NOT NULL,
    `BranchId` bigint unsigned NOT NULL,
    `KioskId` bigint unsigned NULL,
    `CheckInTime` datetime(6) NOT NULL,
    `CheckOutTime` datetime(6) NULL,
    `OpeningFloatCash` decimal(65,30) NULL,
    `IsFraudFlagged` tinyint(1) NOT NULL,
    `FraudFlaggedBy` bigint unsigned NULL,
    `FraudReason` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_attendance_logs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_attendance_logs_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_attendance_logs_kiosks_KioskId` FOREIGN KEY (`KioskId`) REFERENCES `kiosks` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_attendance_logs_shift_assignments_AssignmentId` FOREIGN KEY (`AssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_attendance_logs_users_FraudFlaggedBy` FOREIGN KEY (`FraudFlaggedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `shift_swap_requests` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `RequestingAssignmentId` bigint unsigned NOT NULL,
    `TargetAssignmentId` bigint unsigned NOT NULL,
    `Reason` longtext CHARACTER SET utf8mb4 NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ReviewedBy` bigint unsigned NULL,
    `ReviewedAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_shift_swap_requests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_shift_swap_requests_shift_assignments_RequestingAssignmentId` FOREIGN KEY (`RequestingAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_shift_swap_requests_shift_assignments_TargetAssignmentId` FOREIGN KEY (`TargetAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_shift_swap_requests_users_ReviewedBy` FOREIGN KEY (`ReviewedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE TABLE `cash_handovers` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `ShiftHandoverId` bigint unsigned NOT NULL,
    `CashierId` bigint unsigned NOT NULL,
    `OpeningCash` decimal(65,30) NOT NULL,
    `SystemExpectedCash` decimal(65,30) NOT NULL,
    `ClosingActualCash` decimal(65,30) NOT NULL,
    `DiscrepancyReason` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_cash_handovers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_cash_handovers_shift_handovers_ShiftHandoverId` FOREIGN KEY (`ShiftHandoverId`) REFERENCES `shift_handovers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_cash_handovers_users_CashierId` FOREIGN KEY (`CashierId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `security_handovers` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `ShiftHandoverId` bigint unsigned NOT NULL,
    `SecurityGuardId` bigint unsigned NOT NULL,
    `OvernightVehicleCount` smallint unsigned NOT NULL,
    `IsWarehouseLocked` tinyint(1) NOT NULL,
    `IsShutterClosed` tinyint(1) NOT NULL,
    `SecurityNotes` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_security_handovers` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_security_handovers_shift_handovers_ShiftHandoverId` FOREIGN KEY (`ShiftHandoverId`) REFERENCES `shift_handovers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_security_handovers_users_SecurityGuardId` FOREIGN KEY (`SecurityGuardId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;


CREATE TABLE `overtime_requests` (
    `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
    `AttendanceLogId` bigint unsigned NOT NULL,
    `OtType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `RequestedMinutes` int unsigned NOT NULL,
    `ApprovedMinutes` int unsigned NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `VerifiedByLeader` bigint unsigned NULL,
    `ApprovedByManager` bigint unsigned NULL,
    `ManagerNotes` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_overtime_requests` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_overtime_requests_attendance_logs_AttendanceLogId` FOREIGN KEY (`AttendanceLogId`) REFERENCES `attendance_logs` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_overtime_requests_users_ApprovedByManager` FOREIGN KEY (`ApprovedByManager`) REFERENCES `users` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_overtime_requests_users_VerifiedByLeader` FOREIGN KEY (`VerifiedByLeader`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;


CREATE UNIQUE INDEX `IX_attendance_logs_AssignmentId` ON `attendance_logs` (`AssignmentId`);


CREATE INDEX `IX_attendance_logs_BranchId` ON `attendance_logs` (`BranchId`);


CREATE INDEX `IX_attendance_logs_CheckInTime_BranchId` ON `attendance_logs` (`CheckInTime`, `BranchId`);


CREATE INDEX `IX_attendance_logs_FraudFlaggedBy` ON `attendance_logs` (`FraudFlaggedBy`);


CREATE INDEX `IX_attendance_logs_KioskId` ON `attendance_logs` (`KioskId`);


CREATE UNIQUE INDEX `IX_branches_BranchCode` ON `branches` (`BranchCode`);


CREATE INDEX `IX_cash_handovers_CashierId` ON `cash_handovers` (`CashierId`);


CREATE INDEX `IX_cash_handovers_ShiftHandoverId` ON `cash_handovers` (`ShiftHandoverId`);


CREATE INDEX `IX_kiosk_activation_codes_BranchId` ON `kiosk_activation_codes` (`BranchId`);


CREATE INDEX `IX_kiosk_activation_codes_Code` ON `kiosk_activation_codes` (`Code`);


CREATE INDEX `IX_kiosk_activation_codes_GeneratedBy` ON `kiosk_activation_codes` (`GeneratedBy`);


CREATE INDEX `IX_kiosks_BranchId` ON `kiosks` (`BranchId`);


CREATE UNIQUE INDEX `IX_kiosks_DeviceToken` ON `kiosks` (`DeviceToken`);


CREATE UNIQUE INDEX `IX_kiosks_KioskCode` ON `kiosks` (`KioskCode`);


CREATE UNIQUE INDEX `IX_monthly_timesheets_BranchId_PeriodMonth_PeriodYear` ON `monthly_timesheets` (`BranchId`, `PeriodMonth`, `PeriodYear`);


CREATE INDEX `IX_monthly_timesheets_LockedBy` ON `monthly_timesheets` (`LockedBy`);


CREATE INDEX `IX_overtime_requests_ApprovedByManager` ON `overtime_requests` (`ApprovedByManager`);


CREATE INDEX `IX_overtime_requests_AttendanceLogId` ON `overtime_requests` (`AttendanceLogId`);


CREATE INDEX `IX_overtime_requests_VerifiedByLeader` ON `overtime_requests` (`VerifiedByLeader`);


CREATE UNIQUE INDEX `IX_roles_RoleCode` ON `roles` (`RoleCode`);


CREATE INDEX `IX_security_handovers_SecurityGuardId` ON `security_handovers` (`SecurityGuardId`);


CREATE INDEX `IX_security_handovers_ShiftHandoverId` ON `security_handovers` (`ShiftHandoverId`);


CREATE INDEX `IX_shift_assignments_AssignedRoleId` ON `shift_assignments` (`AssignedRoleId`);


CREATE UNIQUE INDEX `IX_shift_assignments_ScheduleId_UserId` ON `shift_assignments` (`ScheduleId`, `UserId`);


CREATE INDEX `IX_shift_assignments_UserId_ScheduleId` ON `shift_assignments` (`UserId`, `ScheduleId`);


CREATE UNIQUE INDEX `IX_shift_handovers_ScheduleId` ON `shift_handovers` (`ScheduleId`);


CREATE INDEX `IX_shift_handovers_ShiftLeaderId` ON `shift_handovers` (`ShiftLeaderId`);


CREATE INDEX `IX_shift_swap_requests_RequestingAssignmentId` ON `shift_swap_requests` (`RequestingAssignmentId`);


CREATE INDEX `IX_shift_swap_requests_ReviewedBy` ON `shift_swap_requests` (`ReviewedBy`);


CREATE INDEX `IX_shift_swap_requests_TargetAssignmentId` ON `shift_swap_requests` (`TargetAssignmentId`);


CREATE UNIQUE INDEX `IX_shift_templates_TemplateCode` ON `shift_templates` (`TemplateCode`);


CREATE INDEX `IX_system_audit_logs_ActorId` ON `system_audit_logs` (`ActorId`);


CREATE INDEX `IX_system_audit_logs_Timestamp_Action` ON `system_audit_logs` (`Timestamp`, `Action`);


CREATE INDEX `IX_temporary_dispatches_ApprovedBy` ON `temporary_dispatches` (`ApprovedBy`);


CREATE INDEX `IX_temporary_dispatches_RequestedBy` ON `temporary_dispatches` (`RequestedBy`);


CREATE INDEX `IX_temporary_dispatches_SourceBranchId` ON `temporary_dispatches` (`SourceBranchId`);


CREATE INDEX `IX_temporary_dispatches_TargetBranchId` ON `temporary_dispatches` (`TargetBranchId`);


CREATE INDEX `IX_temporary_dispatches_UserId_StartDate_EndDate_Status` ON `temporary_dispatches` (`UserId`, `StartDate`, `EndDate`, `Status`);


CREATE UNIQUE INDEX `IX_users_Email` ON `users` (`Email`);


CREATE UNIQUE INDEX `IX_users_EmployeeCode` ON `users` (`EmployeeCode`);


CREATE INDEX `IX_users_HomeBranchId_Status` ON `users` (`HomeBranchId`, `Status`);


CREATE UNIQUE INDEX `IX_users_Phone` ON `users` (`Phone`);


CREATE INDEX `IX_users_RoleId` ON `users` (`RoleId`);


CREATE UNIQUE INDEX `IX_work_schedules_BranchId_ShiftTemplateId_WorkDate` ON `work_schedules` (`BranchId`, `ShiftTemplateId`, `WorkDate`);


CREATE INDEX `IX_work_schedules_BranchId_WorkDate_Status` ON `work_schedules` (`BranchId`, `WorkDate`, `Status`);


CREATE INDEX `IX_work_schedules_CreatedBy` ON `work_schedules` (`CreatedBy`);


CREATE INDEX `IX_work_schedules_ShiftTemplateId` ON `work_schedules` (`ShiftTemplateId`);


