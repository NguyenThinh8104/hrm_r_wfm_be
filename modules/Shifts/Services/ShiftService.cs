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
        var shifts = await _context.ShiftTemplates
            .Where(s => s.IsActive)
            .OrderBy(s => s.StartTime)
            .Select(s => new ShiftDto
            {
                ShiftId = (int)s.Id,
                ShiftCode = s.TemplateCode,
                ShiftName = s.Name,
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
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Where(sa => sa.Schedule.BranchId == (ulong)storeId && sa.Schedule.WorkDate >= startDate && sa.Schedule.WorkDate <= endDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                EmployeeId = (int)sa.UserId,
                EmployeeName = sa.User.FullName,
                EmployeeCode = sa.User.EmployeeCode,
                PositionName = sa.User.Role.RoleName,
                ShiftId = (int)sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                WorkDate = sa.Schedule.WorkDate,
                StoreId = (int)sa.Schedule.BranchId,
                StoreName = sa.Schedule.Branch.Name,
                Status = sa.Status,
                IsDispatched = sa.User.HomeBranchId != (ulong)storeId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    public async Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request)
    {
        var conflict = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.EmployeeId && sa.Schedule.WorkDate == request.WorkDate);

        if (conflict != null)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail(
                $"Nhân viên đã được xếp ca '{conflict.Schedule.ShiftTemplate.Name}' tại '{conflict.Schedule.Branch.Name}' ngày {request.WorkDate:dd/MM/yyyy}. Hệ thống tự động chặn gán trùng!");
        }

        var shiftTemplate = await _context.ShiftTemplates.FindAsync((uint)request.ShiftId);
        if (shiftTemplate == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy khung ca làm việc.");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId);
        if (user == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy nhân viên.");

        var branch = await _context.Branches.FindAsync((ulong)request.StoreId);
        if (branch == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy cửa hàng.");

        var schedule = await _context.WorkSchedules
            .FirstOrDefaultAsync(ws => ws.BranchId == (ulong)request.StoreId 
                                    && ws.ShiftTemplateId == (uint)request.ShiftId 
                                    && ws.WorkDate == request.WorkDate);

        if (schedule == null)
        {
            schedule = new WorkSchedule
            {
                BranchId = (ulong)request.StoreId,
                ShiftTemplateId = (uint)request.ShiftId,
                WorkDate = request.WorkDate,
                RequiredCashier = 1,
                RequiredSales = 1,
                RequiredSecurity = 1,
                Status = "PUBLISHED",
                CreatedBy = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
        }

        var assignment = new ShiftAssignment
        {
            ScheduleId = schedule.Id,
            UserId = (ulong)request.EmployeeId,
            AssignedRoleId = user.RoleId,
            AssignmentType = "ASSIGNED",
            Status = "CONFIRMED"
        };

        _context.ShiftAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftAssignmentDto>.Ok(new ShiftAssignmentDto
        {
            AssignmentId = (int)assignment.Id,
            ScheduleId = (int)schedule.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PositionName = user.Role.RoleName,
            ShiftId = (int)shiftTemplate.Id,
            ShiftName = shiftTemplate.Name,
            StartTime = shiftTemplate.StartTime,
            EndTime = shiftTemplate.EndTime,
            WorkDate = request.WorkDate,
            StoreId = (int)branch.Id,
            StoreName = branch.Name,
            Status = assignment.Status,
            IsDispatched = user.HomeBranchId != (ulong)request.StoreId
        }, "Gán ca làm việc thành công.");
    }

    public async Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId)
    {
        var weekEnd = weekStartDate.AddDays(6);
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == (ulong)storeId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEnd)
            .ToListAsync();

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            foreach (var assignment in schedule.ShiftAssignments)
            {
                assignment.Status = "CONFIRMED";
            }
        }

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, $"Đã công bố lịch làm việc tuần {weekStartDate:dd/MM/yyyy} thành công!");
    }

    public async Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Where(sa => sa.UserId == (ulong)employeeId && sa.Schedule.WorkDate >= startDate && sa.Schedule.WorkDate <= endDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                EmployeeId = (int)sa.UserId,
                EmployeeName = sa.User.FullName,
                EmployeeCode = sa.User.EmployeeCode,
                PositionName = sa.User.Role.RoleName,
                ShiftId = (int)sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                WorkDate = sa.Schedule.WorkDate,
                StoreId = (int)sa.Schedule.BranchId,
                StoreName = sa.Schedule.Branch.Name,
                Status = sa.Status,
                IsDispatched = sa.User.HomeBranchId != sa.Schedule.BranchId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    public async Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
    {
        var requestingAssignment = await _context.ShiftAssignments
            .Include(sa => sa.User)
            .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.AssignmentId && sa.UserId == (ulong)requesterEmployeeId);

        if (requestingAssignment == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của bạn để yêu cầu đổi.");
        }

        var targetUser = await _context.Users.FindAsync((ulong)request.TargetEmployeeId);
        if (targetUser == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy nhân viên được yêu cầu đổi ca.");
        }

        var targetAssignment = await _context.ShiftAssignments
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.TargetEmployeeId && sa.ScheduleId == requestingAssignment.ScheduleId);

        if (targetAssignment == null)
        {
            targetAssignment = requestingAssignment;
        }

        var swap = new ShiftSwapRequest
        {
            RequestingAssignmentId = requestingAssignment.Id,
            TargetAssignmentId = targetAssignment.Id,
            Reason = request.Reason,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftSwapRequestDto>.Ok(new ShiftSwapRequestDto
        {
            SwapRequestId = (int)swap.Id,
            AssignmentId = (int)swap.RequestingAssignmentId,
            RequesterEmployeeId = requesterEmployeeId,
            RequesterName = requestingAssignment.User.FullName,
            TargetEmployeeId = (int)targetUser.Id,
            TargetName = targetUser.FullName,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        }, "Đã gửi yêu cầu đổi ca, chờ Quản lý cửa hàng phê duyệt.");
    }

    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
            .Include(s => s.TargetAssignment)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca không tồn tại.");
        }

        if (swap.Status != "PENDING")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        swap.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        swap.ReviewedBy = (ulong)managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved && swap.RequestingAssignment != null && swap.TargetAssignment != null)
        {
            var tempUser = swap.RequestingAssignment.UserId;
            swap.RequestingAssignment.UserId = swap.TargetAssignment.UserId;
            swap.TargetAssignment.UserId = tempUser;
        }

        await _context.SaveChangesAsync();

        var message = request.IsApproved ? "Đã duyệt yêu cầu đổi ca." : "Đã từ chối yêu cầu đổi ca.";
        return ApiResponse<bool>.Ok(true, message);
    }

    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.User)
            .Where(s => s.RequestingAssignment.Schedule.BranchId == (ulong)storeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                AssignmentId = (int)s.RequestingAssignmentId,
                RequesterEmployeeId = (int)s.RequestingAssignment.UserId,
                RequesterName = s.RequestingAssignment.User.FullName,
                TargetEmployeeId = (int)s.TargetAssignment.UserId,
                TargetName = s.TargetAssignment.User.FullName,
                Reason = s.Reason,
                Status = s.Status,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }
}
