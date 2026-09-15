-- MySQL dump 10.13  Distrib 8.0.46, for Win64 (x86_64)
--
-- Host: 127.0.0.1    Database: rwfm_db
-- ------------------------------------------------------
-- Server version	8.0.46

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `attendance_logs`
--

DROP TABLE IF EXISTS `attendance_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `attendance_logs` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `AssignmentId` bigint unsigned NOT NULL,
  `BranchId` bigint unsigned NOT NULL,
  `KioskId` bigint unsigned DEFAULT NULL,
  `CheckInTime` datetime(6) NOT NULL,
  `CheckOutTime` datetime(6) DEFAULT NULL,
  `OpeningFloatCash` decimal(65,30) DEFAULT NULL,
  `IsFraudFlagged` tinyint(1) NOT NULL,
  `FraudFlaggedBy` bigint unsigned DEFAULT NULL,
  `FraudReason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_attendance_logs_AssignmentId` (`AssignmentId`),
  KEY `IX_attendance_logs_BranchId` (`BranchId`),
  KEY `IX_attendance_logs_CheckInTime_BranchId` (`CheckInTime`,`BranchId`),
  KEY `IX_attendance_logs_FraudFlaggedBy` (`FraudFlaggedBy`),
  KEY `IX_attendance_logs_KioskId` (`KioskId`),
  CONSTRAINT `FK_attendance_logs_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_attendance_logs_kiosks_KioskId` FOREIGN KEY (`KioskId`) REFERENCES `kiosks` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_attendance_logs_shift_assignments_AssignmentId` FOREIGN KEY (`AssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_attendance_logs_users_FraudFlaggedBy` FOREIGN KEY (`FraudFlaggedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `attendance_logs`
--

LOCK TABLES `attendance_logs` WRITE;
/*!40000 ALTER TABLE `attendance_logs` DISABLE KEYS */;
/*!40000 ALTER TABLE `attendance_logs` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `branches`
--

DROP TABLE IF EXISTS `branches`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `branches` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BranchCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Name` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Address` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `KioskAllowedIp` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_branches_BranchCode` (`BranchCode`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `branches`
--

LOCK TABLES `branches` WRITE;
/*!40000 ALTER TABLE `branches` DISABLE KEYS */;
INSERT INTO `branches` VALUES (1,'CH01','Cửa hàng Tiện lợi Chi nhánh Cầu Giấy','123 Cầu Giấy, Q. Cầu Giấy, Hà Nội',NULL,'ACTIVE','2026-09-13 14:58:12.551000','2026-09-13 14:58:12.551001'),(2,'CH02','Cửa hàng Tiện lợi Chi nhánh Lê Văn Việt','456 Lê Văn Việt, TP. Thủ Đức, TP. Hồ Chí Minh',NULL,'ACTIVE','2026-09-13 14:58:12.551149','2026-09-13 14:58:12.551149');
/*!40000 ALTER TABLE `branches` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `cash_handovers`
--

DROP TABLE IF EXISTS `cash_handovers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cash_handovers` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ShiftHandoverId` bigint unsigned NOT NULL,
  `CashierId` bigint unsigned NOT NULL,
  `OpeningCash` decimal(65,30) NOT NULL,
  `SystemExpectedCash` decimal(65,30) NOT NULL,
  `ClosingActualCash` decimal(65,30) NOT NULL,
  `DiscrepancyReason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_cash_handovers_CashierId` (`CashierId`),
  KEY `IX_cash_handovers_ShiftHandoverId` (`ShiftHandoverId`),
  CONSTRAINT `FK_cash_handovers_shift_handovers_ShiftHandoverId` FOREIGN KEY (`ShiftHandoverId`) REFERENCES `shift_handovers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_cash_handovers_users_CashierId` FOREIGN KEY (`CashierId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `cash_handovers`
--

LOCK TABLES `cash_handovers` WRITE;
/*!40000 ALTER TABLE `cash_handovers` DISABLE KEYS */;
/*!40000 ALTER TABLE `cash_handovers` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `kiosk_activation_codes`
--

DROP TABLE IF EXISTS `kiosk_activation_codes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `kiosk_activation_codes` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BranchId` bigint unsigned NOT NULL,
  `KioskName` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Code` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `GeneratedBy` bigint unsigned NOT NULL,
  `ExpiresAt` datetime(6) NOT NULL,
  `IsUsed` tinyint(1) NOT NULL,
  `CreatedKioskId` bigint unsigned DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_kiosk_activation_codes_BranchId` (`BranchId`),
  KEY `IX_kiosk_activation_codes_Code` (`Code`),
  KEY `IX_kiosk_activation_codes_GeneratedBy` (`GeneratedBy`),
  CONSTRAINT `FK_kiosk_activation_codes_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_kiosk_activation_codes_users_GeneratedBy` FOREIGN KEY (`GeneratedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `kiosk_activation_codes`
--

LOCK TABLES `kiosk_activation_codes` WRITE;
/*!40000 ALTER TABLE `kiosk_activation_codes` DISABLE KEYS */;
/*!40000 ALTER TABLE `kiosk_activation_codes` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `kiosks`
--

DROP TABLE IF EXISTS `kiosks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `kiosks` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BranchId` bigint unsigned NOT NULL,
  `KioskCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Name` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DeviceToken` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `IpAddress` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `LastPingAt` datetime(6) DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_kiosks_DeviceToken` (`DeviceToken`),
  UNIQUE KEY `IX_kiosks_KioskCode` (`KioskCode`),
  KEY `IX_kiosks_BranchId` (`BranchId`),
  CONSTRAINT `FK_kiosks_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `kiosks`
--

LOCK TABLES `kiosks` WRITE;
/*!40000 ALTER TABLE `kiosks` DISABLE KEYS */;
INSERT INTO `kiosks` VALUES (1,1,'CH01-POS01','Máy Kiosk Cầu Giấy 01','ksk_tok_demo_pos01','ACTIVE',NULL,NULL,'2026-09-13 14:58:13.079365');
/*!40000 ALTER TABLE `kiosks` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `monthly_timesheets`
--

DROP TABLE IF EXISTS `monthly_timesheets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `monthly_timesheets` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BranchId` bigint unsigned NOT NULL,
  `PeriodMonth` tinyint unsigned NOT NULL,
  `PeriodYear` smallint unsigned NOT NULL,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `LockedBy` bigint unsigned DEFAULT NULL,
  `LockedAt` datetime(6) DEFAULT NULL,
  `ExportedAt` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_monthly_timesheets_BranchId_PeriodMonth_PeriodYear` (`BranchId`,`PeriodMonth`,`PeriodYear`),
  KEY `IX_monthly_timesheets_LockedBy` (`LockedBy`),
  CONSTRAINT `FK_monthly_timesheets_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_monthly_timesheets_users_LockedBy` FOREIGN KEY (`LockedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `monthly_timesheets`
--

LOCK TABLES `monthly_timesheets` WRITE;
/*!40000 ALTER TABLE `monthly_timesheets` DISABLE KEYS */;
/*!40000 ALTER TABLE `monthly_timesheets` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `overtime_requests`
--

DROP TABLE IF EXISTS `overtime_requests`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `overtime_requests` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `AttendanceLogId` bigint unsigned NOT NULL,
  `OtType` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RequestedMinutes` int unsigned NOT NULL,
  `ApprovedMinutes` int unsigned NOT NULL,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `VerifiedByLeader` bigint unsigned DEFAULT NULL,
  `ApprovedByManager` bigint unsigned DEFAULT NULL,
  `ManagerNotes` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_overtime_requests_ApprovedByManager` (`ApprovedByManager`),
  KEY `IX_overtime_requests_AttendanceLogId` (`AttendanceLogId`),
  KEY `IX_overtime_requests_VerifiedByLeader` (`VerifiedByLeader`),
  CONSTRAINT `FK_overtime_requests_attendance_logs_AttendanceLogId` FOREIGN KEY (`AttendanceLogId`) REFERENCES `attendance_logs` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_overtime_requests_users_ApprovedByManager` FOREIGN KEY (`ApprovedByManager`) REFERENCES `users` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_overtime_requests_users_VerifiedByLeader` FOREIGN KEY (`VerifiedByLeader`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `overtime_requests`
--

LOCK TABLES `overtime_requests` WRITE;
/*!40000 ALTER TABLE `overtime_requests` DISABLE KEYS */;
/*!40000 ALTER TABLE `overtime_requests` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `roles`
--

DROP TABLE IF EXISTS `roles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `roles` (
  `Id` tinyint unsigned NOT NULL,
  `RoleCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `RoleName` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Description` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_roles_RoleCode` (`RoleCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `roles`
--

LOCK TABLES `roles` WRITE;
/*!40000 ALTER TABLE `roles` DISABLE KEYS */;
INSERT INTO `roles` VALUES (1,'BUSINESS_OWNER','Chủ Doanh Nghiệp','Chỉ xem Dashboard & Audit Log toàn hệ thống'),(2,'OPERATIONS_ADMIN','Quản Trị Vận Hành','Quản trị Master Data toàn chuỗi'),(3,'STORE_MANAGER','Quản Lý Cửa Hàng','Quản lý vận hành & lập lịch ca chi nhánh'),(4,'SHIFT_LEADER','Trưởng Ca Trực','Trưởng ca trực tại chỗ & báo cáo gian lận'),(5,'CASHIER','Thu Ngân','Bán hàng, thu tiền & bàn giao két tiền'),(6,'SALES_STAFF','Nhân Viên Bán Hàng','Quản lý quầy kệ, xếp hàng hóa'),(7,'SECURITY_GUARD','Nhân Viên Bảo Vệ','An ninh, kho bãi & xe qua đêm');
/*!40000 ALTER TABLE `roles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `security_handovers`
--

DROP TABLE IF EXISTS `security_handovers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `security_handovers` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ShiftHandoverId` bigint unsigned NOT NULL,
  `SecurityGuardId` bigint unsigned NOT NULL,
  `OvernightVehicleCount` smallint unsigned NOT NULL,
  `IsWarehouseLocked` tinyint(1) NOT NULL,
  `IsShutterClosed` tinyint(1) NOT NULL,
  `SecurityNotes` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_security_handovers_SecurityGuardId` (`SecurityGuardId`),
  KEY `IX_security_handovers_ShiftHandoverId` (`ShiftHandoverId`),
  CONSTRAINT `FK_security_handovers_shift_handovers_ShiftHandoverId` FOREIGN KEY (`ShiftHandoverId`) REFERENCES `shift_handovers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_security_handovers_users_SecurityGuardId` FOREIGN KEY (`SecurityGuardId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `security_handovers`
--

LOCK TABLES `security_handovers` WRITE;
/*!40000 ALTER TABLE `security_handovers` DISABLE KEYS */;
/*!40000 ALTER TABLE `security_handovers` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `shift_assignments`
--

DROP TABLE IF EXISTS `shift_assignments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `shift_assignments` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ScheduleId` bigint unsigned NOT NULL,
  `UserId` bigint unsigned NOT NULL,
  `AssignedRoleId` tinyint unsigned NOT NULL,
  `AssignmentType` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_shift_assignments_ScheduleId_UserId` (`ScheduleId`,`UserId`),
  KEY `IX_shift_assignments_AssignedRoleId` (`AssignedRoleId`),
  KEY `IX_shift_assignments_UserId_ScheduleId` (`UserId`,`ScheduleId`),
  CONSTRAINT `FK_shift_assignments_roles_AssignedRoleId` FOREIGN KEY (`AssignedRoleId`) REFERENCES `roles` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shift_assignments_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_shift_assignments_work_schedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `work_schedules` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `shift_assignments`
--

LOCK TABLES `shift_assignments` WRITE;
/*!40000 ALTER TABLE `shift_assignments` DISABLE KEYS */;
INSERT INTO `shift_assignments` VALUES (1,1,5,5,'ASSIGNED','CONFIRMED'),(2,1,6,6,'ASSIGNED','CONFIRMED'),(3,1,7,7,'ASSIGNED','CONFIRMED'),(4,1,4,4,'ASSIGNED','CONFIRMED');
/*!40000 ALTER TABLE `shift_assignments` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `shift_handovers`
--

DROP TABLE IF EXISTS `shift_handovers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `shift_handovers` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ScheduleId` bigint unsigned NOT NULL,
  `ShiftLeaderId` bigint unsigned NOT NULL,
  `HandoverStatus` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `SignedAt` datetime(6) DEFAULT NULL,
  `GeneralNotes` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_shift_handovers_ScheduleId` (`ScheduleId`),
  KEY `IX_shift_handovers_ShiftLeaderId` (`ShiftLeaderId`),
  CONSTRAINT `FK_shift_handovers_users_ShiftLeaderId` FOREIGN KEY (`ShiftLeaderId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shift_handovers_work_schedules_ScheduleId` FOREIGN KEY (`ScheduleId`) REFERENCES `work_schedules` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `shift_handovers`
--

LOCK TABLES `shift_handovers` WRITE;
/*!40000 ALTER TABLE `shift_handovers` DISABLE KEYS */;
/*!40000 ALTER TABLE `shift_handovers` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `shift_swap_requests`
--

DROP TABLE IF EXISTS `shift_swap_requests`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `shift_swap_requests` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `RequestingAssignmentId` bigint unsigned NOT NULL,
  `TargetAssignmentId` bigint unsigned NOT NULL,
  `Reason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `Status` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ReviewedBy` bigint unsigned DEFAULT NULL,
  `ReviewedAt` datetime(6) DEFAULT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_shift_swap_requests_RequestingAssignmentId` (`RequestingAssignmentId`),
  KEY `IX_shift_swap_requests_ReviewedBy` (`ReviewedBy`),
  KEY `IX_shift_swap_requests_TargetAssignmentId` (`TargetAssignmentId`),
  CONSTRAINT `FK_shift_swap_requests_shift_assignments_RequestingAssignmentId` FOREIGN KEY (`RequestingAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shift_swap_requests_shift_assignments_TargetAssignmentId` FOREIGN KEY (`TargetAssignmentId`) REFERENCES `shift_assignments` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_shift_swap_requests_users_ReviewedBy` FOREIGN KEY (`ReviewedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `shift_swap_requests`
--

LOCK TABLES `shift_swap_requests` WRITE;
/*!40000 ALTER TABLE `shift_swap_requests` DISABLE KEYS */;
/*!40000 ALTER TABLE `shift_swap_requests` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `shift_templates`
--

DROP TABLE IF EXISTS `shift_templates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `shift_templates` (
  `Id` int unsigned NOT NULL AUTO_INCREMENT,
  `TemplateCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Name` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `StartTime` time(6) NOT NULL,
  `EndTime` time(6) NOT NULL,
  `IsOvernight` tinyint(1) NOT NULL,
  `BreakDurationMinutes` int unsigned NOT NULL,
  `IsActive` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_shift_templates_TemplateCode` (`TemplateCode`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `shift_templates`
--

LOCK TABLES `shift_templates` WRITE;
/*!40000 ALTER TABLE `shift_templates` DISABLE KEYS */;
INSERT INTO `shift_templates` VALUES (1,'CA_SANG','Ca Sáng (06:00 - 14:00)','06:00:00.000000','14:00:00.000000',0,30,1),(2,'CA_CHIEU','Ca Chiều (14:00 - 22:00)','14:00:00.000000','22:00:00.000000',0,30,1),(3,'CA_DEM','Ca Đêm (22:00 - 06:00)','22:00:00.000000','06:00:00.000000',1,45,1);
/*!40000 ALTER TABLE `shift_templates` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `system_audit_logs`
--

DROP TABLE IF EXISTS `system_audit_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `system_audit_logs` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ActorId` bigint unsigned NOT NULL,
  `Action` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TargetTable` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `TargetId` bigint unsigned NOT NULL,
  `OldValues` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `NewValues` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `IpAddress` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `Timestamp` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_system_audit_logs_ActorId` (`ActorId`),
  KEY `IX_system_audit_logs_Timestamp_Action` (`Timestamp`,`Action`),
  CONSTRAINT `FK_system_audit_logs_users_ActorId` FOREIGN KEY (`ActorId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `system_audit_logs`
--

LOCK TABLES `system_audit_logs` WRITE;
/*!40000 ALTER TABLE `system_audit_logs` DISABLE KEYS */;
/*!40000 ALTER TABLE `system_audit_logs` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `temporary_dispatches`
--

DROP TABLE IF EXISTS `temporary_dispatches`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `temporary_dispatches` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `SourceBranchId` bigint unsigned NOT NULL,
  `TargetBranchId` bigint unsigned NOT NULL,
  `UserId` bigint unsigned NOT NULL,
  `StartDate` date NOT NULL,
  `EndDate` date NOT NULL,
  `RequestedBy` bigint unsigned NOT NULL,
  `ApprovedBy` bigint unsigned DEFAULT NULL,
  `Status` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Note` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_temporary_dispatches_ApprovedBy` (`ApprovedBy`),
  KEY `IX_temporary_dispatches_RequestedBy` (`RequestedBy`),
  KEY `IX_temporary_dispatches_SourceBranchId` (`SourceBranchId`),
  KEY `IX_temporary_dispatches_TargetBranchId` (`TargetBranchId`),
  KEY `IX_temporary_dispatches_UserId_StartDate_EndDate_Status` (`UserId`,`StartDate`,`EndDate`,`Status`),
  CONSTRAINT `FK_temporary_dispatches_branches_SourceBranchId` FOREIGN KEY (`SourceBranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_temporary_dispatches_branches_TargetBranchId` FOREIGN KEY (`TargetBranchId`) REFERENCES `branches` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_temporary_dispatches_users_ApprovedBy` FOREIGN KEY (`ApprovedBy`) REFERENCES `users` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_temporary_dispatches_users_RequestedBy` FOREIGN KEY (`RequestedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_temporary_dispatches_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `temporary_dispatches`
--

LOCK TABLES `temporary_dispatches` WRITE;
/*!40000 ALTER TABLE `temporary_dispatches` DISABLE KEYS */;
/*!40000 ALTER TABLE `temporary_dispatches` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `EmployeeCode` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `FullName` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Phone` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PasswordHash` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `KioskPinHash` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci,
  `RoleId` tinyint unsigned NOT NULL,
  `EmploymentType` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `HomeBranchId` bigint unsigned DEFAULT NULL,
  `Status` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_users_Email` (`Email`),
  UNIQUE KEY `IX_users_EmployeeCode` (`EmployeeCode`),
  UNIQUE KEY `IX_users_Phone` (`Phone`),
  KEY `IX_users_HomeBranchId_Status` (`HomeBranchId`,`Status`),
  KEY `IX_users_RoleId` (`RoleId`),
  CONSTRAINT `FK_users_branches_HomeBranchId` FOREIGN KEY (`HomeBranchId`) REFERENCES `branches` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_users_roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,'OWN001','Nguyễn Văn Chủ','owner@rwfm.vn','0901000001','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',1,'FULL_TIME',NULL,'ACTIVE','2026-09-13 14:58:12.995854','2026-09-13 14:58:12.995855'),(2,'OPS001','Trần Văn Vận Hành','ops.admin@rwfm.vn','0901000002','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',2,'FULL_TIME',NULL,'ACTIVE','2026-09-13 14:58:12.996180','2026-09-13 14:58:12.996180'),(3,'MGR001','Trần Thị Mai','manager.store01@rwfm.vn','0901111111','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',3,'FULL_TIME',1,'ACTIVE','2026-09-13 14:58:12.996180','2026-09-13 14:58:12.996180'),(4,'SLD001','Phạm Gia Bảo','leader.store01@rwfm.vn','0901111112','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',4,'FULL_TIME',1,'ACTIVE','2026-09-13 14:58:12.996181','2026-09-13 14:58:12.996181'),(5,'CSH001','Đỗ Hoàng Ngân','cashier.store01@rwfm.vn','0901111113','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',5,'FULL_TIME',1,'ACTIVE','2026-09-13 14:58:12.996181','2026-09-13 14:58:12.996181'),(6,'SAL001','Võ Minh Khang','sales.store01@rwfm.vn','0901111114','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',6,'FULL_TIME',1,'ACTIVE','2026-09-13 14:58:12.996182','2026-09-13 14:58:12.996182'),(7,'SEC001','Đinh Hùng Dũng','security.store01@rwfm.vn','0901111115','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',7,'FULL_TIME',1,'ACTIVE','2026-09-13 14:58:12.996182','2026-09-13 14:58:12.996182'),(8,'MGR002','Lê Hoàng Phúc','manager.store02@rwfm.vn','0902222222','$2a$11$JaJeDQvHsoOGRLj6Ptnwru3Fuaq9UZoxA9V6UsB7o9wBm.4HJV/V.','$2a$11$hYwke8I4aWN8HqkY/wIPc.LDvg35rHnmlEO1HWmAs7YhUggD7L/mO',3,'FULL_TIME',2,'ACTIVE','2026-09-13 14:58:12.996182','2026-09-13 14:58:12.996182'),(9,'SEC002','Nguyễn Phú Thịnh','nguyenphuthinh08012004@gmail.com','0862974370','$2a$11$seZbyQDcb78KJ9Zazj0vLOxULaWcoHkh1jOAa6ilAgaOtz2eUEGJe','$2a$11$mI4eT3gP0B2rQ5xY8j1ceuR2tYvXx7b9u4C3o2s1Y0x8w7v6u5t4s',7,'FULL_TIME',2,'ACTIVE','2026-09-13 21:58:33.000000','2026-09-13 16:08:24.238193');
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `work_schedules`
--

DROP TABLE IF EXISTS `work_schedules`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `work_schedules` (
  `Id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `BranchId` bigint unsigned NOT NULL,
  `ShiftTemplateId` int unsigned NOT NULL,
  `WorkDate` date NOT NULL,
  `RequiredCashier` tinyint unsigned NOT NULL,
  `RequiredSales` tinyint unsigned NOT NULL,
  `RequiredSecurity` tinyint unsigned NOT NULL,
  `Status` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `CreatedBy` bigint unsigned NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_work_schedules_BranchId_ShiftTemplateId_WorkDate` (`BranchId`,`ShiftTemplateId`,`WorkDate`),
  KEY `IX_work_schedules_BranchId_WorkDate_Status` (`BranchId`,`WorkDate`,`Status`),
  KEY `IX_work_schedules_CreatedBy` (`CreatedBy`),
  KEY `IX_work_schedules_ShiftTemplateId` (`ShiftTemplateId`),
  CONSTRAINT `FK_work_schedules_branches_BranchId` FOREIGN KEY (`BranchId`) REFERENCES `branches` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_work_schedules_shift_templates_ShiftTemplateId` FOREIGN KEY (`ShiftTemplateId`) REFERENCES `shift_templates` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_work_schedules_users_CreatedBy` FOREIGN KEY (`CreatedBy`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `work_schedules`
--

LOCK TABLES `work_schedules` WRITE;
/*!40000 ALTER TABLE `work_schedules` DISABLE KEYS */;
INSERT INTO `work_schedules` VALUES (1,1,1,'2026-09-13',1,1,1,'PUBLISHED',3,'2026-09-13 14:58:13.105436');
/*!40000 ALTER TABLE `work_schedules` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-09-14  0:44:48
