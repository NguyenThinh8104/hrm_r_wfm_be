using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Security;

namespace Modules.Attendance.Services;

/// <summary>
/// Dịch vụ xử lý logic chấm công Kiosk, xác thực PIN và ghi nhận ngoại lệ gian lận.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;

    public AttendanceService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Bước 1 quy trình Kiosk: Xác thực mã PIN nhân viên và trả về thông tin ca làm việc hôm nay.
    /// </summary>
    public async Task<ApiResponse<ValidatePinResponseDto>> ValidatePinAsync(ValidatePinRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || string.IsNullOrWhiteSpace(request.PinCode))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.EmployeeCode.ToLower() == request.EmployeeCode.Trim().ToLower());

        if (user == null)
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.EmployeeInactive);
        }

        if (string.IsNullOrEmpty(user.KioskPinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
            .FirstOrDefaultAsync(sa => sa.UserId == user.Id && sa.Schedule.BranchId == (ulong)request.StoreId && sa.Schedule.WorkDate == today);

        var log = assignment?.AttendanceLog;

        return ApiResponse<ValidatePinResponseDto>.Ok(new ValidatePinResponseDto
        {
            EmployeeId = (int)user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            PositionName = user.Role?.RoleName ?? string.Empty,
            PositionCode = user.Role?.RoleCode ?? string.Empty,
            PrimaryStoreId = user.HomeBranchId.HasValue ? (int)user.HomeBranchId.Value : 0,
            IsValid = true,
            HasShiftToday = assignment != null,
            AssignmentId = assignment != null ? (int)assignment.Id : null,
            ShiftName = assignment?.Schedule?.ShiftTemplate?.Name,
            HasCheckedIn = log?.CheckInTime != null,
            HasCheckedOut = log?.CheckOutTime != null
        }, AttendanceMessages.PinValidationSuccess);
    }

    /// <summary>
    /// Lấy danh sách phân công lịch làm việc hôm nay tại trạm Kiosk cửa hàng.
    /// </summary>
    public async Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
            .Where(sa => sa.Schedule.BranchId == (ulong)storeId && sa.Schedule.WorkDate == date)
            .OrderBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var result = assignments.Select(sa =>
        {
            var att = sa.AttendanceLog;
            return new KioskEmployeeRosterDto
            {
                EmployeeId = (int)sa.UserId,
                EmployeeCode = sa.User.EmployeeCode,
                FullName = sa.User.FullName,
                PositionName = sa.User.Role.RoleName,
                AssignmentId = (int)sa.Id,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                HasCheckedIn = att?.CheckInTime != null,
                HasCheckedOut = att?.CheckOutTime != null,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                IsDispatched = sa.User.HomeBranchId != (ulong)storeId
            };
        }).ToList();

        return ApiResponse<List<KioskEmployeeRosterDto>>.Ok(result);
    }

    /// <summary>
    /// Bước 2 quy trình Kiosk: Thực hiện điểm danh đầu ca (Check-in) bằng mã PIN nhân viên.
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> KioskCheckInAsync(KioskPinCheckInDto request)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId && u.Status == "ACTIVE");

        if (user == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);

        if (string.IsNullOrEmpty(user.KioskPinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(sa => sa.AttendanceLog)
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.EmployeeId && sa.Schedule.BranchId == (ulong)request.StoreId && sa.Schedule.WorkDate == today);

        if (assignment == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(string.Format(AttendanceMessages.NoShiftToday, user.FullName, today.ToString("dd/MM/yyyy")));
        }

        if (assignment.AttendanceLog != null && assignment.AttendanceLog.CheckInTime != default)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.AlreadyCheckedIn);
        }

        var now = DateTime.UtcNow;
        var status = "Present";

        var shiftStartDt = today.ToDateTime(assignment.Schedule.ShiftTemplate.StartTime);
        if (DateTime.Now > shiftStartDt.AddMinutes(15))
        {
            status = "Late";
        }

        var log = new AttendanceLog
        {
            AssignmentId = assignment.Id,
            BranchId = (ulong)request.StoreId,
            KioskId = request.KioskId.HasValue ? (ulong)request.KioskId.Value : null,
            CheckInTime = now,
            OpeningFloatCash = request.OpeningFloatCash,
            CreatedAt = now
        };

        _context.AttendanceLogs.Add(log);
        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckInSuccess, log.CheckInTime.ToLocalTime().ToString("HH:mm:ss"));

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = (int)log.Id,
            AssignmentId = (int)assignment.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            StoreId = request.StoreId,
            KioskId = request.KioskId,
            StoreName = assignment.Schedule.Branch.Name,
            CheckInTime = log.CheckInTime,
            CheckInMethod = "Kiosk",
            Status = status
        }, successMessage);
    }

    /// <summary>
    /// Bước 2 quy trình Kiosk: Thực hiện điểm danh kết thúc ca (Check-out) bằng mã PIN nhân viên.
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request)
    {
        var user = await _context.Users.FindAsync((ulong)request.EmployeeId);
        if (user == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);

        if (string.IsNullOrEmpty(user.KioskPinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(sa => sa.AttendanceLog)
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.EmployeeId && sa.Schedule.BranchId == (ulong)request.StoreId && sa.Schedule.WorkDate == today);

        if (assignment == null) return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.ShiftNotFound);

        var log = assignment.AttendanceLog;
        if (log == null || log.CheckInTime == default)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NotCheckedIn);
        }

        log.CheckOutTime = DateTime.UtcNow;
        if (request.KioskId.HasValue) log.KioskId = (ulong)request.KioskId.Value;
        assignment.Status = "COMPLETED";

        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckOutSuccess, log.CheckOutTime?.ToLocalTime().ToString("HH:mm:ss"));

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = (int)log.Id,
            AssignmentId = (int)assignment.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            StoreId = request.StoreId,
            KioskId = request.KioskId,
            StoreName = assignment.Schedule.Branch.Name,
            CheckInTime = log.CheckInTime,
            CheckOutTime = log.CheckOutTime,
            CheckInMethod = "Kiosk",
            CheckOutMethod = "Kiosk",
            Status = "Completed"
        }, successMessage);
    }

    /// <summary>
    /// Trưởng ca / Quản lý báo cáo gian lận điểm danh hoặc vắng mặt của nhân viên.
    /// </summary>
    public async Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request)
    {
        var log = await _context.AttendanceLogs
            .Include(al => al.Assignment)
            .FirstOrDefaultAsync(al => al.Id == (ulong)request.AttendanceId);

        if (log == null) return ApiResponse<bool>.Fail(AttendanceMessages.AttendanceRecordNotFound);

        log.IsFraudFlagged = true;
        log.FraudFlaggedBy = (ulong)leaderEmployeeId;
        log.FraudReason = request.Reason;

        if (log.Assignment != null)
        {
            log.Assignment.Status = "CANCELLED";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, AttendanceMessages.FraudReportSuccess);
    }

    /// <summary>
    /// Lấy lịch sử danh sách các bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date)
    {
        var records = await _context.AttendanceLogs
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.User)
            .Include(al => al.Branch)
            .Include(al => al.FraudFlaggedByUser)
            .Where(al => al.BranchId == (ulong)storeId && DateOnly.FromDateTime(al.CreatedAt.ToLocalTime()) == date)
            .OrderByDescending(al => al.CreatedAt)
            .Select(al => new AttendanceRecordDto
            {
                AttendanceId = (int)al.Id,
                AssignmentId = (int)al.AssignmentId,
                EmployeeId = (int)al.Assignment.UserId,
                EmployeeName = al.Assignment.User.FullName,
                EmployeeCode = al.Assignment.User.EmployeeCode,
                StoreId = (int)al.BranchId,
                KioskId = al.KioskId.HasValue ? (int)al.KioskId.Value : null,
                StoreName = al.Branch.Name,
                CheckInTime = al.CheckInTime,
                CheckOutTime = al.CheckOutTime,
                CheckInMethod = "Kiosk",
                CheckOutMethod = al.CheckOutTime.HasValue ? "Kiosk" : null,
                Status = al.IsFraudFlagged ? "Fraud" : (al.CheckOutTime.HasValue ? "Completed" : "Present"),
                HasException = al.IsFraudFlagged,
                ExceptionReason = al.FraudReason,
                ReportedByName = al.FraudFlaggedByUser != null ? al.FraudFlaggedByUser.FullName : null
            })
            .ToListAsync();

        return ApiResponse<List<AttendanceRecordDto>>.Ok(records);
    }
}
