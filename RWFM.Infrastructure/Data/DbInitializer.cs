using Microsoft.EntityFrameworkCore;
using RWFM.Domain.Entities;
using RWFM.Infrastructure.Services;

namespace RWFM.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(RWFMDbContext context)
    {
        // Kiểm tra xem đã có Users chưa
        if (await context.Users.AnyAsync())
        {
            return; // Đã seed dữ liệu
        }

        var defaultPasswordHash = PasswordHasher.Hash("Password@123");
        var defaultPinHash = PasswordHasher.Hash("1234");
        var now = DateTime.UtcNow;

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

        // 3. Thu ngân Store 1 (Employee)
        var cashierUser1 = new User
        {
            Username = "cashier.store01",
            PasswordHash = defaultPasswordHash,
            Role = "Employee",
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

        // 4. Bán hàng Store 1 (Employee)
        var salesUser1 = new User
        {
            Username = "sales.store01",
            PasswordHash = defaultPasswordHash,
            Role = "Employee",
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

        // 5. Bảo vệ Store 1 (Employee)
        var secUser1 = new User
        {
            Username = "security.store01",
            PasswordHash = defaultPasswordHash,
            Role = "Employee",
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

        await context.SaveChangesAsync();

        // 7. Tạo WorkSchedule và ShiftAssignments hôm nay cho Cửa hàng 1 (Nguyễn Trãi)
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dayOfWeek = (int)today.DayOfWeek;
        var diffToMonday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        var weekStart = today.AddDays(diffToMonday);
        var weekEnd = weekStart.AddDays(6);

        var schedule = new WorkSchedule
        {
            StoreId = 1,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            Status = "Published",
            PublishedAt = now,
            PublishedBy = mgrEmp1.EmployeeId,
            CreatedAt = now
        };
        context.WorkSchedules.Add(schedule);
        await context.SaveChangesAsync();

        // Ca sáng (ShiftId = 1: 08:00 - 14:00) cho Trưởng ca, Thu ngân, Bán hàng, Bảo vệ
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                ScheduleId = schedule.ScheduleId,
                StoreId = 1,
                EmployeeId = leaderEmp1.EmployeeId,
                ShiftId = 1, // Ca sáng
                WorkDate = today,
                Status = "Scheduled",
                CreatedAt = now
            },
            new()
            {
                ScheduleId = schedule.ScheduleId,
                StoreId = 1,
                EmployeeId = cashierEmp1.EmployeeId,
                ShiftId = 1, // Ca sáng
                WorkDate = today,
                Status = "Scheduled",
                CreatedAt = now
            },
            new()
            {
                ScheduleId = schedule.ScheduleId,
                StoreId = 1,
                EmployeeId = salesEmp1.EmployeeId,
                ShiftId = 1, // Ca sáng
                WorkDate = today,
                Status = "Scheduled",
                CreatedAt = now
            },
            new()
            {
                ScheduleId = schedule.ScheduleId,
                StoreId = 1,
                EmployeeId = secEmp1.EmployeeId,
                ShiftId = 1, // Ca sáng
                WorkDate = today,
                Status = "Scheduled",
                CreatedAt = now
            }
        };

        context.ShiftAssignments.AddRange(assignments);
        await context.SaveChangesAsync();
    }
}
