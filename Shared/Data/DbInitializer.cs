using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Shared.Security;

namespace Shared.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        var defaultPasswordHash = PasswordHasher.Hash("Password@123");
        var defaultPinHash = PasswordHasher.Hash("1234");
        var now = DateTime.UtcNow;

        if (!await context.Stores.AnyAsync())
        {
            context.Stores.AddRange(
                new Store { StoreCode = "ST001", StoreName = "Chi nhánh Nguyễn Trãi", Address = "Q5, TP.HCM", IsActive = true, CreatedAt = now },
                new Store { StoreCode = "ST002", StoreName = "Chi nhánh Lê Văn Việt", Address = "Q9, TP.HCM", IsActive = true, CreatedAt = now }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.Positions.AnyAsync())
        {
            context.Positions.AddRange(
                new Position { PositionCode = "STORE_MANAGER", PositionName = "Cửa hàng trưởng", IsActive = true },
                new Position { PositionCode = "SHIFT_LEADER", PositionName = "Trưởng ca", IsActive = true },
                new Position { PositionCode = "CASHIER", PositionName = "Thu ngân", IsActive = true },
                new Position { PositionCode = "SALES_STAFF", PositionName = "Nhân viên bán hàng", IsActive = true },
                new Position { PositionCode = "SECURITY_GUARD", PositionName = "Bảo vệ", IsActive = true }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync(u => u.Username == "manager.store01"))
        {
            // 1. Cửa hàng trưởng Store 1 (StoreManager)
            var mgrUser1 = new User
            {
                Username = "manager.store01",
                PasswordHash = defaultPasswordHash,
                Role = "StoreManager",
                IsActive = true,
                CreatedAt = now
            };
            context.Users.Add(mgrUser1);
            await context.SaveChangesAsync();

            var mgrEmp1 = new Employee
            {
                UserId = mgrUser1.UserId,
                EmployeeCode = "MGR001",
                FullName = "Trần Thị Mai (QL Nguyễn Trãi)",
                Email = "manager.store01@rwfm.vn",
                Phone = "0909000001",
                HireDate = new DateOnly(2023, 1, 15),
                PositionId = 1, // STORE_MANAGER
                PrimaryStoreId = 1,
                PinHash = defaultPinHash,
                IsActive = true,
                CreatedAt = now
            };
            context.Employees.Add(mgrEmp1);
        }

        // 2. Trưởng ca Store 1 (ShiftLeader)
        var leaderUser1 = new User
        {
            Username = "leader.store01",
            PasswordHash = defaultPasswordHash,
            Role = "ShiftLeader",
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(leaderUser1);
        await context.SaveChangesAsync();

        var leaderEmp1 = new Employee
        {
            UserId = leaderUser1.UserId,
            EmployeeCode = "SLD001",
            FullName = "Phạm Gia Bảo (Trưởng ca)",
            Email = "leader.store01@rwfm.vn",
            Phone = "0909000002",
            HireDate = new DateOnly(2023, 3, 1),
            PositionId = 2, // SHIFT_LEADER
            PrimaryStoreId = 1,
            PinHash = defaultPinHash,
            IsActive = true,
            CreatedAt = now
        };
        context.Employees.Add(leaderEmp1);

        // 3. Thu ngân Store 1 (Cashier)
        var cashierUser1 = new User
        {
            Username = "cashier.store01",
            PasswordHash = defaultPasswordHash,
            Role = "Cashier",
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(cashierUser1);
        await context.SaveChangesAsync();

        var cashierEmp1 = new Employee
        {
            UserId = cashierUser1.UserId,
            EmployeeCode = "CSH001",
            FullName = "Đỗ Hoàng Ngân (Thu ngân)",
            Email = "cashier.store01@rwfm.vn",
            Phone = "0909000003",
            HireDate = new DateOnly(2023, 5, 10),
            PositionId = 3, // CASHIER
            PrimaryStoreId = 1,
            PinHash = defaultPinHash,
            IsActive = true,
            CreatedAt = now
        };
        context.Employees.Add(cashierEmp1);

        // 4. Bán hàng Store 1 (SalesStaff)
        var salesUser1 = new User
        {
            Username = "sales.store01",
            PasswordHash = defaultPasswordHash,
            Role = "SalesStaff",
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(salesUser1);
        await context.SaveChangesAsync();

        var salesEmp1 = new Employee
        {
            UserId = salesUser1.UserId,
            EmployeeCode = "SAL001",
            FullName = "Võ Minh Khang (Bán hàng)",
            Email = "sales.store01@rwfm.vn",
            Phone = "0909000004",
            HireDate = new DateOnly(2023, 6, 20),
            PositionId = 4, // SALES_STAFF
            PrimaryStoreId = 1,
            PinHash = defaultPinHash,
            IsActive = true,
            CreatedAt = now
        };
        context.Employees.Add(salesEmp1);

        // 5. Bảo vệ Store 1 (SecurityGuard)
        var secUser1 = new User
        {
            Username = "security.store01",
            PasswordHash = defaultPasswordHash,
            Role = "SecurityGuard",
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(secUser1);
        await context.SaveChangesAsync();

        var secEmp1 = new Employee
        {
            UserId = secUser1.UserId,
            EmployeeCode = "SEC001",
            FullName = "Đinh Hùng Dũng (Bảo vệ)",
            Email = "security.store01@rwfm.vn",
            Phone = "0909000005",
            HireDate = new DateOnly(2023, 2, 10),
            PositionId = 5, // SECURITY_GUARD
            PrimaryStoreId = 1,
            PinHash = defaultPinHash,
            IsActive = true,
            CreatedAt = now
        };
        context.Employees.Add(secEmp1);

        // 6. Quản lý Store 2 (StoreManager)
        var mgrUser2 = new User
        {
            Username = "manager.store02",
            PasswordHash = defaultPasswordHash,
            Role = "StoreManager",
            IsActive = true,
            CreatedAt = now
        };
        context.Users.Add(mgrUser2);
        await context.SaveChangesAsync();

        var mgrEmp2 = new Employee
        {
            UserId = mgrUser2.UserId,
            EmployeeCode = "MGR002",
            FullName = "Lê Hoàng Phúc (QL Lê Văn Việt)",
            Email = "manager.store02@rwfm.vn",
            Phone = "0909000006",
            HireDate = new DateOnly(2023, 4, 1),
            PositionId = 1,
            PrimaryStoreId = 2,
            PinHash = defaultPinHash,
            IsActive = true,
            CreatedAt = now
        };
        context.Employees.Add(mgrEmp2);

        if (!await context.Users.AnyAsync(u => u.Username == "business.owner"))
        {
            // 7. Business Owner (Chủ doanh nghiệp)
            var ownerUser = new User
            {
                Username = "business.owner",
                PasswordHash = defaultPasswordHash,
                Role = "BusinessOwner",
                IsActive = true,
                CreatedAt = now
            };
            context.Users.Add(ownerUser);
            await context.SaveChangesAsync();

            var ownerEmp = new Employee
            {
                UserId = ownerUser.UserId,
                EmployeeCode = "OWN001",
                FullName = "Nguyễn Văn Đạt (Chủ doanh nghiệp)",
                Email = "owner@rwfm.vn",
                Phone = "0909999999",
                HireDate = new DateOnly(2022, 1, 1),
                PositionId = 1,
                PrimaryStoreId = 1,
                PinHash = defaultPinHash,
                IsActive = true,
                CreatedAt = now
            };
            context.Employees.Add(ownerEmp);
        }

        if (!await context.Users.AnyAsync(u => u.Username == "ops.admin"))
        {
            // 8. Operations Admin (Quản trị vận hành)
            var opsUser = new User
            {
                Username = "ops.admin",
                PasswordHash = defaultPasswordHash,
                Role = "OperationsAdmin",
                IsActive = true,
                CreatedAt = now
            };
            context.Users.Add(opsUser);
            await context.SaveChangesAsync();

            var opsEmp = new Employee
            {
                UserId = opsUser.UserId,
                EmployeeCode = "OPS001",
                FullName = "Trần Văn Vận Hành (Ops Admin)",
                Email = "ops.admin@rwfm.vn",
                Phone = "0909888888",
                HireDate = new DateOnly(2022, 1, 1),
                PositionId = 1,
                PrimaryStoreId = 1,
                PinHash = defaultPinHash,
                IsActive = true,
                CreatedAt = now
            };
            context.Employees.Add(opsEmp);
        }

        await context.SaveChangesAsync();

        if (!await context.WorkSchedules.AnyAsync(ws => ws.StoreId == 1))
        {
            // 10. Tạo WorkSchedule và ShiftAssignments hôm nay cho Cửa hàng 1 (Nguyễn Trãi)
            var today = DateOnly.FromDateTime(DateTime.Now);
            var dayOfWeek = (int)today.DayOfWeek;
            var diffToMonday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
            var weekStart = today.AddDays(diffToMonday);
            var weekEnd = weekStart.AddDays(6);

            var mgrEmp1Id = await context.Employees.Where(e => e.EmployeeCode == "MGR001").Select(e => e.EmployeeId).FirstOrDefaultAsync();
            var leaderEmp1Id = await context.Employees.Where(e => e.EmployeeCode == "SLD001").Select(e => e.EmployeeId).FirstOrDefaultAsync();
            var cashierEmp1Id = await context.Employees.Where(e => e.EmployeeCode == "CSH001").Select(e => e.EmployeeId).FirstOrDefaultAsync();
            var salesEmp1Id = await context.Employees.Where(e => e.EmployeeCode == "SAL001").Select(e => e.EmployeeId).FirstOrDefaultAsync();
            var secEmp1Id = await context.Employees.Where(e => e.EmployeeCode == "SEC001").Select(e => e.EmployeeId).FirstOrDefaultAsync();

            if (mgrEmp1Id > 0 && leaderEmp1Id > 0)
            {
                var schedule = new WorkSchedule
                {
                    StoreId = 1,
                    WeekStartDate = weekStart,
                    WeekEndDate = weekEnd,
                    Status = "Published",
                    PublishedAt = now,
                    PublishedBy = mgrEmp1Id,
                    CreatedAt = now
                };
                context.WorkSchedules.Add(schedule);
                await context.SaveChangesAsync();

                // Ca sáng (ShiftId = 1: 08:00 - 14:00) cho Trưởng ca, Thu ngân, Bán hàng, Bảo vệ
                if (!await context.Shifts.AnyAsync())
                {
                    context.Shifts.AddRange(
                        new Shift { ShiftCode = "MORNING", ShiftName = "Ca sáng (08:00 - 14:00)", StartTime = new TimeOnly(8, 0, 0), EndTime = new TimeOnly(14, 0, 0), IsActive = true },
                        new Shift { ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (14:00 - 22:00)", StartTime = new TimeOnly(14, 0, 0), EndTime = new TimeOnly(22, 0, 0), IsActive = true }
                    );
                    await context.SaveChangesAsync();
                }

                var assignments = new List<ShiftAssignment>
                {
                    new()
                    {
                        ScheduleId = schedule.ScheduleId,
                        StoreId = 1,
                        EmployeeId = leaderEmp1Id,
                        ShiftId = 1, // Ca sáng
                        WorkDate = today,
                        Status = "Scheduled",
                        CreatedAt = now
                    },
                    new()
                    {
                        ScheduleId = schedule.ScheduleId,
                        StoreId = 1,
                        EmployeeId = cashierEmp1Id,
                        ShiftId = 1,
                        WorkDate = today,
                        Status = "Scheduled",
                        CreatedAt = now
                    },
                    new()
                    {
                        ScheduleId = schedule.ScheduleId,
                        StoreId = 1,
                        EmployeeId = salesEmp1Id,
                        ShiftId = 1,
                        WorkDate = today,
                        Status = "Scheduled",
                        CreatedAt = now
                    },
                    new()
                    {
                        ScheduleId = schedule.ScheduleId,
                        StoreId = 1,
                        EmployeeId = secEmp1Id,
                        ShiftId = 1,
                        WorkDate = today,
                        Status = "Scheduled",
                        CreatedAt = now
                    }
                };

                context.ShiftAssignments.AddRange(assignments);
                await context.SaveChangesAsync();
            }
        }

        if (!await context.KioskDevices.AnyAsync())
        {
            var defaultDevice = new KioskDevice
            {
                StoreId = 1,
                KioskCode = "CH01-POS01",
                Name = "Máy Quầy Thu Ngân 1 (Nguyễn Trãi)",
                DeviceToken = "ksk_tok_demo_st001_01",
                Status = "Active",
                IpAddress = "127.0.0.1",
                LastPingAt = now,
                CreatedAt = now
            };
            context.KioskDevices.Add(defaultDevice);
            await context.SaveChangesAsync();
        }

        if (!await context.KioskActivationCodes.AnyAsync())
        {
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Role == "StoreManager" || u.Role == "SystemAdmin");
            var adminUserId = adminUser?.UserId ?? 1;

            context.KioskActivationCodes.Add(new KioskActivationCode
            {
                StoreId = 1,
                KioskName = "Máy POS Quầy Số 2",
                Code = "POS-4421",
                GeneratedBy = adminUserId,
                ExpiresAt = now.AddDays(7), // Cho phép test thoải mái
                IsUsed = false,
                CreatedAt = now
            });
            await context.SaveChangesAsync();
        }
    }
}

