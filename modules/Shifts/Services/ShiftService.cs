using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Shifts.Services;

public class ShiftService : IShiftService
{
    private readonly AppDbContext _context;

    public ShiftService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync()
    {
        var shifts = await _context.Shifts
            .Where(s => s.IsActive)
            .OrderBy(s => s.StartTime)
            .Select(s => new ShiftDto
            {
                ShiftId = s.ShiftId,
                ShiftCode = s.ShiftCode,
                ShiftName = s.ShiftName,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsOvernight = s.IsOvernight
            })
            .ToListAsync();

        return ApiResponse<List<ShiftDto>>.Ok(shifts);
    }

    public async Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Employee)
                .ThenInclude(e => e.Position)
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Where(sa => sa.StoreId == storeId && sa.WorkDate >= startDate && sa.WorkDate <= endDate)
            .OrderBy(sa => sa.WorkDate)
            .ThenBy(sa => sa.Shift.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = sa.AssignmentId,
                ScheduleId = sa.ScheduleId,
                EmployeeId = sa.EmployeeId,
                EmployeeName = sa.Employee.FullName,
                EmployeeCode = sa.Employee.EmployeeCode,
                PositionName = sa.Employee.Position.PositionName,
                ShiftId = sa.ShiftId,
                ShiftName = sa.Shift.ShiftName,
                StartTime = sa.Shift.StartTime,
                EndTime = sa.Shift.EndTime,
                WorkDate = sa.WorkDate,
                StoreId = sa.StoreId,
                StoreName = sa.Store.StoreName,
                Status = sa.Status,
                IsDispatched = sa.Employee.PrimaryStoreId != sa.StoreId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    public async Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request)
    {
        // 1. Chặn gán trùng: Nhân viên đã được gán ca trong ngày này tại bất kỳ cửa hàng nào chưa
        var conflict = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .FirstOrDefaultAsync(sa => sa.EmployeeId == request.EmployeeId && sa.WorkDate == request.WorkDate);

        if (conflict != null)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail(
                $"Nhân viên đã được xếp ca '{conflict.Shift.ShiftName}' tại '{conflict.Store.StoreName}' ngày {request.WorkDate:dd/MM/yyyy}. Hệ thống tự động chặn gán trùng!");
        }

        var shift = await _context.Shifts.FindAsync(request.ShiftId);
        if (shift == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy ca làm việc.");

        var employee = await _context.Employees
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.EmployeeId == request.EmployeeId);
        if (employee == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy nhân viên.");

        var store = await _context.Stores.FindAsync(request.StoreId);
        if (store == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy cửa hàng.");

        // Tìm hoặc tạo WorkSchedule tuần
        int scheduleId = request.ScheduleId ?? 0;
        if (scheduleId == 0)
        {
            var dayOfWeek = (int)request.WorkDate.DayOfWeek;
            var diffToMonday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
            var weekStart = request.WorkDate.AddDays(diffToMonday);
            var weekEnd = weekStart.AddDays(6);

            var schedule = await _context.WorkSchedules
                .FirstOrDefaultAsync(ws => ws.StoreId == request.StoreId && ws.WeekStartDate == weekStart);

            if (schedule == null)
            {
                schedule = new WorkSchedule
                {
                    StoreId = request.StoreId,
                    WeekStartDate = weekStart,
                    WeekEndDate = weekEnd,
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                };
                _context.WorkSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }
            scheduleId = schedule.ScheduleId;
        }

        var assignment = new ShiftAssignment
        {
            StoreId = request.StoreId,
            ScheduleId = scheduleId,
            EmployeeId = request.EmployeeId,
            ShiftId = request.ShiftId,
            WorkDate = request.WorkDate,
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftAssignmentDto>.Ok(new ShiftAssignmentDto
        {
            AssignmentId = assignment.AssignmentId,
            ScheduleId = assignment.ScheduleId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            PositionName = employee.Position.PositionName,
            ShiftId = shift.ShiftId,
            ShiftName = shift.ShiftName,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            WorkDate = assignment.WorkDate,
            StoreId = store.StoreId,
            StoreName = store.StoreName,
            Status = assignment.Status,
            IsDispatched = employee.PrimaryStoreId != store.StoreId
        }, "Gán ca làm việc thành công.");
    }

    public async Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId)
    {
        var schedule = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .FirstOrDefaultAsync(ws => ws.StoreId == storeId && ws.WeekStartDate == weekStartDate);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy lịch làm việc của tuần đã chọn.");
        }

        schedule.Status = "Published";
        schedule.PublishedAt = DateTime.UtcNow;
        schedule.PublishedBy = publishedByEmployeeId;

        foreach (var assignment in schedule.ShiftAssignments)
        {
            assignment.Status = "Scheduled";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, $"Đã công bố lịch làm việc tuần {weekStartDate:dd/MM/yyyy} thành công!");
    }

    public async Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Shift)
            .Include(sa => sa.Store)
            .Include(sa => sa.Employee)
                .ThenInclude(e => e.Position)
            .Where(sa => sa.EmployeeId == employeeId && sa.WorkDate >= startDate && sa.WorkDate <= endDate)
            .OrderBy(sa => sa.WorkDate)
            .ThenBy(sa => sa.Shift.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = sa.AssignmentId,
                ScheduleId = sa.ScheduleId,
                EmployeeId = sa.EmployeeId,
                EmployeeName = sa.Employee.FullName,
                EmployeeCode = sa.Employee.EmployeeCode,
                PositionName = sa.Employee.Position.PositionName,
                ShiftId = sa.ShiftId,
                ShiftName = sa.Shift.ShiftName,
                StartTime = sa.Shift.StartTime,
                EndTime = sa.Shift.EndTime,
                WorkDate = sa.WorkDate,
                StoreId = sa.StoreId,
                StoreName = sa.Store.StoreName,
                Status = sa.Status,
                IsDispatched = sa.Employee.PrimaryStoreId != sa.StoreId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    public async Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
    {
        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Employee)
            .FirstOrDefaultAsync(sa => sa.AssignmentId == request.AssignmentId && sa.EmployeeId == requesterEmployeeId);

        if (assignment == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của bạn để yêu cầu đổi.");
        }

        var target = await _context.Employees.FindAsync(request.TargetEmployeeId);
        if (target == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy nhân viên được yêu cầu đổi ca.");
        }

        var swap = new ShiftSwapRequest
        {
            AssignmentId = request.AssignmentId,
            RequesterEmployeeId = requesterEmployeeId,
            TargetEmployeeId = request.TargetEmployeeId,
            Reason = request.Reason,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftSwapRequestDto>.Ok(new ShiftSwapRequestDto
        {
            SwapRequestId = swap.SwapRequestId,
            AssignmentId = swap.AssignmentId,
            RequesterEmployeeId = requesterEmployeeId,
            RequesterName = assignment.Employee.FullName,
            TargetEmployeeId = target.EmployeeId,
            TargetName = target.FullName,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        }, "Đã gửi yêu cầu đổi ca, chờ Quản lý cửa hàng phê duyệt.");
    }

    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.SwapRequestId == request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca không tồn tại.");
        }

        if (swap.Status != "Pending")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        swap.Status = request.IsApproved ? "Approved" : "Rejected";
        swap.ReviewedBy = managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved && swap.Assignment != null)
        {
            swap.Assignment.EmployeeId = swap.TargetEmployeeId;
        }

        await _context.SaveChangesAsync();

        var message = request.IsApproved ? "Đã duyệt yêu cầu đổi ca." : "Đã từ chối yêu cầu đổi ca.";
        return ApiResponse<bool>.Ok(true, message);
    }

    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
        var requests = await _context.ShiftSwapRequests
            .Include(s => s.Assignment)
            .Include(s => s.RequesterEmployee)
            .Include(s => s.TargetEmployee)
            .Where(s => s.Assignment.StoreId == storeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = s.SwapRequestId,
                AssignmentId = s.AssignmentId,
                RequesterEmployeeId = s.RequesterEmployeeId,
                RequesterName = s.RequesterEmployee.FullName,
                TargetEmployeeId = s.TargetEmployeeId,
                TargetName = s.TargetEmployee.FullName,
                Reason = s.Reason,
                Status = s.Status,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }
}

