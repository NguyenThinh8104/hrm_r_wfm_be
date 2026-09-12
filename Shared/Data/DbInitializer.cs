using Domain.Entities;
using Shared.Security;

namespace Shared.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // Recreate Database with new schema
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // 1. Seed Roles
        if (!context.Roles.Any())
        {
            var roles = new List<Role>
            {
                new Role { Id = 1, RoleCode = "BUSINESS_OWNER", RoleName = "Chủ Doanh Nghiệp", Description = "Chỉ xem Dashboard & Audit Log toàn hệ thống" },
                new Role { Id = 2, RoleCode = "OPERATIONS_ADMIN", RoleName = "Quản Trị Vận Hành", Description = "Quản trị Master Data toàn chuỗi" },
                new Role { Id = 3, RoleCode = "STORE_MANAGER", RoleName = "Quản Lý Cửa Hàng", Description = "Quản lý vận hành & lập lịch ca chi nhánh" },
                new Role { Id = 4, RoleCode = "SHIFT_LEADER", RoleName = "Trưởng Ca Trực", Description = "Trưởng ca trực tại chỗ & báo cáo gian lận" },
                new Role { Id = 5, RoleCode = "CASHIER", RoleName = "Thu Ngân", Description = "Bán hàng, thu tiền & bàn giao két tiền" },
                new Role { Id = 6, RoleCode = "SALES_STAFF", RoleName = "Nhân Viên Bán Hàng", Description = "Quản lý quầy kệ, xếp hàng hóa" },
                new Role { Id = 7, RoleCode = "SECURITY_GUARD", RoleName = "Nhân Viên Bảo Vệ", Description = "An ninh, kho bãi & xe qua đêm" },
            };
            context.Roles.AddRange(roles);
            context.SaveChanges();
        }

        // 2. Seed Branches
        if (!context.Branches.Any())
        {
            var branches = new List<Branch>
            {
                new Branch
                {
                    Id = 1,
                    BranchCode = "CH01",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Cầu Giấy",
                    Address = "123 Cầu Giấy, Q. Cầu Giấy, Hà Nội",
                    Status = "ACTIVE"
                },
                new Branch
                {
                    Id = 2,
                    BranchCode = "CH02",
                    Name = "Cửa hàng Tiện lợi Chi nhánh Lê Văn Việt",
                    Address = "456 Lê Văn Việt, TP. Thủ Đức, TP. Hồ Chí Minh",
                    Status = "ACTIVE"
                }
            };
            context.Branches.AddRange(branches);
            context.SaveChanges();
        }

        // 3. Seed Users
        if (!context.Users.Any())
        {
            var defaultPasswordHash = PasswordHasher.Hash("Password@123");
            var defaultPinHash = PasswordHasher.Hash("1234");

            var users = new List<User>
            {
                new User
                {
                    Id = 1,
                    EmployeeCode = "OWN001",
                    FullName = "Nguyễn Văn Chủ",
                    Email = "owner@rwfm.vn",
                    Phone = "0901000001",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 1, // BUSINESS_OWNER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = null,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 2,
                    EmployeeCode = "OPS001",
                    FullName = "Trần Văn Vận Hành",
                    Email = "ops.admin@rwfm.vn",
                    Phone = "0901000002",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 2, // OPERATIONS_ADMIN
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = null,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 3,
                    EmployeeCode = "MGR001",
                    FullName = "Trần Thị Mai",
                    Email = "manager.store01@rwfm.vn",
                    Phone = "0901111111",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 3, // STORE_MANAGER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 1,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 4,
                    EmployeeCode = "SLD001",
                    FullName = "Phạm Gia Bảo",
                    Email = "leader.store01@rwfm.vn",
                    Phone = "0901111112",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 4, // SHIFT_LEADER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 1,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 5,
                    EmployeeCode = "CSH001",
                    FullName = "Đỗ Hoàng Ngân",
                    Email = "cashier.store01@rwfm.vn",
                    Phone = "0901111113",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 5, // CASHIER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 1,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 6,
                    EmployeeCode = "SAL001",
                    FullName = "Võ Minh Khang",
                    Email = "sales.store01@rwfm.vn",
                    Phone = "0901111114",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 6, // SALES_STAFF
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 1,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 7,
                    EmployeeCode = "SEC001",
                    FullName = "Đinh Hùng Dũng",
                    Email = "security.store01@rwfm.vn",
                    Phone = "0901111115",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 7, // SECURITY_GUARD
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 1,
                    Status = "ACTIVE"
                },
                new User
                {
                    Id = 8,
                    EmployeeCode = "MGR002",
                    FullName = "Lê Hoàng Phúc",
                    Email = "manager.store02@rwfm.vn",
                    Phone = "0902222222",
                    PasswordHash = defaultPasswordHash,
                    KioskPinHash = defaultPinHash,
                    RoleId = 3, // STORE_MANAGER
                    EmploymentType = "FULL_TIME",
                    HomeBranchId = 2,
                    Status = "ACTIVE"
                }
            };
            context.Users.AddRange(users);
            context.SaveChanges();
        }

        // 4. Seed ShiftTemplates
        if (!context.ShiftTemplates.Any())
        {
            var templates = new List<ShiftTemplate>
            {
                new ShiftTemplate
                {
                    Id = 1,
                    TemplateCode = "CA_SANG",
                    Name = "Ca Sáng (06:00 - 14:00)",
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(14, 0),
                    IsOvernight = false,
                    BreakDurationMinutes = 30,
                    IsActive = true
                },
                new ShiftTemplate
                {
                    Id = 2,
                    TemplateCode = "CA_CHIEU",
                    Name = "Ca Chiều (14:00 - 22:00)",
                    StartTime = new TimeOnly(14, 0),
                    EndTime = new TimeOnly(22, 0),
                    IsOvernight = false,
                    BreakDurationMinutes = 30,
                    IsActive = true
                },
                new ShiftTemplate
                {
                    Id = 3,
                    TemplateCode = "CA_DEM",
                    Name = "Ca Đêm (22:00 - 06:00)",
                    StartTime = new TimeOnly(22, 0),
                    EndTime = new TimeOnly(6, 0),
                    IsOvernight = true,
                    BreakDurationMinutes = 45,
                    IsActive = true
                }
            };
            context.ShiftTemplates.AddRange(templates);
            context.SaveChanges();
        }

        // 5. Seed Kiosks
        if (!context.KioskDevices.Any())
        {
            var kiosk = new KioskDevice
            {
                Id = 1,
                BranchId = 1,
                KioskCode = "CH01-POS01",
                Name = "Máy Kiosk Cầu Giấy 01",
                DeviceToken = "ksk_tok_demo_pos01",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
            context.KioskDevices.Add(kiosk);
            context.SaveChanges();
        }

        // 6. Seed WorkSchedules & ShiftAssignments for today
        if (!context.WorkSchedules.Any())
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var schedule = new WorkSchedule
            {
                Id = 1,
                BranchId = 1,
                ShiftTemplateId = 1, // CA_SANG
                WorkDate = today,
                RequiredCashier = 1,
                RequiredSales = 1,
                RequiredSecurity = 1,
                Status = "PUBLISHED",
                CreatedBy = 3, // StoreManager 1
                CreatedAt = DateTime.UtcNow
            };
            context.WorkSchedules.Add(schedule);
            context.SaveChanges();

            var assignments = new List<ShiftAssignment>
            {
                new ShiftAssignment
                {
                    Id = 1,
                    ScheduleId = 1,
                    UserId = 5, // Đỗ Hoàng Ngân (Cashier)
                    AssignedRoleId = 5, // CASHIER
                    AssignmentType = "ASSIGNED",
                    Status = "CONFIRMED"
                },
                new ShiftAssignment
                {
                    Id = 2,
                    ScheduleId = 1,
                    UserId = 6, // Võ Minh Khang (Sales)
                    AssignedRoleId = 6, // SALES_STAFF
                    AssignmentType = "ASSIGNED",
                    Status = "CONFIRMED"
                },
                new ShiftAssignment
                {
                    Id = 3,
                    ScheduleId = 1,
                    UserId = 7, // Đinh Hùng Dũng (Security)
                    AssignedRoleId = 7, // SECURITY_GUARD
                    AssignmentType = "ASSIGNED",
                    Status = "CONFIRMED"
                },
                new ShiftAssignment
                {
                    Id = 4,
                    ScheduleId = 1,
                    UserId = 4, // Phạm Gia Bảo (ShiftLeader)
                    AssignedRoleId = 4, // SHIFT_LEADER
                    AssignmentType = "ASSIGNED",
                    Status = "CONFIRMED"
                }
            };
            context.ShiftAssignments.AddRange(assignments);
            context.SaveChanges();
        }
    }
}
