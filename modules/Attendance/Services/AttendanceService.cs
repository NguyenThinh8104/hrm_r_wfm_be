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
/// Dịch vụ xử lý logic điểm danh Kiosk (Check-in, Check-out), xác thực mã PIN và quản lý báo cáo gian lận.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;
    private readonly IKioskContext _kioskContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Khởi tạo AttendanceService với các Dependency Injections cần thiết.
    /// </summary>
    /// <param name="context">Database Context kết nối tới EF Core DB</param>
    /// <param name="kioskContext">Ngữ cảnh máy Kiosk (Chi nhánh và Id Kiosk)</param>
    /// <param name="passwordHasher">Dịch vụ mã hóa và kiểm tra mã PIN</param>
    /// <param name="timeProvider">Dịch vụ cung cấp thời gian thực của Server</param>
    public AttendanceService(
        AppDbContext context,
        IKioskContext kioskContext,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _context = context;
        _kioskContext = kioskContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
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

        if (string.IsNullOrEmpty(user.KioskPinHash) || !_passwordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

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

        if (string.IsNullOrEmpty(user.KioskPinHash) || !_passwordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

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

        var now = _timeProvider.GetLocalNow().DateTime;
        var status = "Present";

        var shiftStartDt = today.ToDateTime(assignment.Schedule.ShiftTemplate.StartTime);
        if (now > shiftStartDt.AddMinutes(15))
        {
            status = "Late";
        }

        var log = new AttendanceLog
        {
            AssignmentId = assignment.Id,
            BranchId = (ulong)request.StoreId,
            KioskId = request.KioskId.HasValue ? (ulong)request.KioskId.Value : null,
            CheckInTime = now,
            OpeningFloatCash = null,
            CreatedAt = now
        };

        _context.AttendanceLogs.Add(log);
        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckInSuccess, log.CheckInTime.ToString("HH:mm:ss"));

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

        if (string.IsNullOrEmpty(user.KioskPinHash) || !_passwordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPin);
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

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

        var now = _timeProvider.GetLocalNow().DateTime;
        log.CheckOutTime = now;
        if (request.KioskId.HasValue) log.KioskId = (ulong)request.KioskId.Value;
        assignment.Status = "COMPLETED";

        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckOutSuccess, log.CheckOutTime?.ToString("HH:mm:ss"));

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
            .Where(al => al.BranchId == (ulong)storeId && DateOnly.FromDateTime(al.CreatedAt) == date)
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

    /// <summary>
    /// Xử lý nghiệp vụ điểm danh đầu ca (Check-in) tại quầy Kiosk theo các bước quy chuẩn.
    /// </summary>
    /// <param name="userId">Mã ID nhân viên trong hệ thống (users.id)</param>
    /// <param name="pin">Mã PIN cá nhân 6 chữ số của nhân viên</param>
    /// <returns>ApiResponse chứa bản ghi điểm danh đầu ca (AttendanceLogDto) hoặc thông báo lỗi</returns>
    public async Task<ApiResponse<AttendanceLogDto>> CheckInAsync(long userId, string pin)
    {
        // Bước 1: Query User theo userId và Status == "ACTIVE", Include Role
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        // Xác thực mã PIN nhân viên qua IPasswordHasher
        if (string.IsNullOrEmpty(user.KioskPinHash) || !_passwordHasher.Verify(pin, user.KioskPinHash))
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.InvalidPin);
        }

        // Bước 2: Lấy ngày làm việc hôm nay từ TimeProvider
        var currentLocalOffset = _timeProvider.GetLocalNow();
        var currentLocalTime = currentLocalOffset.DateTime;
        var today = DateOnly.FromDateTime(currentLocalTime);

        // Query ShiftAssignments (Include WorkSchedule & ShiftTemplate) kiểm tra phân công ca làm việc
        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .FirstOrDefaultAsync(sa =>
                sa.UserId == user.Id &&
                sa.Status == "CONFIRMED" &&
                sa.Schedule.BranchId == _kioskContext.BranchId &&
                sa.Schedule.WorkDate == today &&
                sa.Schedule.Status == "PUBLISHED");

        if (assignment == null)
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.NoScheduleAtBranch);
        }

        // Bước 3: Kiểm tra xem ca làm việc này đã được điểm danh hay chưa
        var existingLog = await _context.AttendanceLogs
            .FirstOrDefaultAsync(al => al.AssignmentId == assignment.Id);

        if (existingLog != null)
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.AlreadyCheckedInShift);
        }

        // Bước 4: Kiểm tra thời gian bắt đầu ca và quy định điểm danh sớm tối đa 30 phút
        var shiftTemplate = assignment.Schedule.ShiftTemplate;
        var shiftStartDateTime = today.ToDateTime(shiftTemplate.StartTime);
        var earliestCheckInTime = shiftStartDateTime.AddMinutes(-30);

        if (currentLocalTime < earliestCheckInTime)
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.TooEarlyForCheckIn);
        }

        // Bước 5: Khởi tạo bản ghi AttendanceLog và lưu cơ sở dữ liệu
        bool isLate = currentLocalTime > shiftStartDateTime;
        string status = isLate ? "Late" : "Present";

        var log = new AttendanceLog
        {
            AssignmentId = assignment.Id,
            BranchId = _kioskContext.BranchId,
            KioskId = _kioskContext.KioskId,
            CheckInTime = currentLocalTime,
            OpeningFloatCash = null,
            CreatedAt = currentLocalTime
        };

        _context.AttendanceLogs.Add(log);
        await _context.SaveChangesAsync();

        var dto = new AttendanceLogDto
        {
            Id = log.Id,
            AssignmentId = assignment.Id,
            UserId = user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            BranchId = _kioskContext.BranchId,
            KioskId = _kioskContext.KioskId,
            CheckInTime = log.CheckInTime,
            OpeningFloatCash = null,
            IsLate = isLate,
            Status = status
        };

        var message = string.Format(AttendanceMessages.CheckInSuccess, currentLocalTime.ToString("HH:mm:ss"));
        return ApiResponse<AttendanceLogDto>.Ok(dto, message);
    }

    /// <summary>
    /// Xử lý nghiệp vụ điểm danh kết thúc ca (Check-out) tại quầy Kiosk theo 3 bước quy chuẩn.
    /// </summary>
    /// <param name="userId">Mã ID nhân viên trong hệ thống (users.id)</param>
    /// <param name="pin">Mã PIN cá nhân 6 chữ số của nhân viên</param>
    /// <returns>ApiResponse chứa kết quả điểm danh kết thúc ca (AttendanceCheckOutResultDto) bao gồm tổng số phút làm việc</returns>
    public async Task<ApiResponse<AttendanceCheckOutResultDto>> CheckOutAsync(long userId, string pin)
    {
        // Bước 1: Query User theo userId và Status == "ACTIVE", xác thực mã PIN qua IPasswordHasher
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<AttendanceCheckOutResultDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        if (string.IsNullOrEmpty(user.KioskPinHash) || !_passwordHasher.Verify(pin, user.KioskPinHash))
        {
            return ApiResponse<AttendanceCheckOutResultDto>.Fail(AttendanceMessages.InvalidPin);
        }

        // Bước 2: Tìm phiên điểm danh đang mở (WorkDate = today HOẶC (IsOvernight = true VÀ WorkDate = yesterday))
        var currentLocalOffset = _timeProvider.GetLocalNow();
        var currentLocalTime = currentLocalOffset.DateTime;
        var today = DateOnly.FromDateTime(currentLocalTime);
        var yesterday = today.AddDays(-1);

        var log = await _context.AttendanceLogs
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
            .Where(al => al.Assignment.UserId == user.Id &&
                         al.CheckOutTime == null &&
                         (al.Assignment.Schedule.WorkDate == today ||
                          (al.Assignment.Schedule.ShiftTemplate.IsOvernight && al.Assignment.Schedule.WorkDate == yesterday)))
            .OrderByDescending(al => al.CheckInTime)
            .FirstOrDefaultAsync();

        if (log == null)
        {
            return ApiResponse<AttendanceCheckOutResultDto>.Fail(AttendanceMessages.NoOpenAttendanceLogForCheckOut);
        }

        // Bước 3: Cập nhật CheckOutTime, tính số phút làm việc thực tế và cập nhật trạng thái phân công
        log.CheckOutTime = currentLocalTime;
        if (log.Assignment != null)
        {
            log.Assignment.Status = "COMPLETED";
        }

        double actualMinutes = Math.Max(0, (currentLocalTime - log.CheckInTime).TotalMinutes);

        await _context.SaveChangesAsync();

        var shiftName = log.Assignment?.Schedule?.ShiftTemplate?.Name ?? "Ca làm việc";

        var resultDto = new AttendanceCheckOutResultDto
        {
            AttendanceLogId = log.Id,
            AssignmentId = log.AssignmentId,
            UserId = user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            ShiftName = shiftName,
            CheckInTime = log.CheckInTime,
            CheckOutTime = currentLocalTime,
            ActualWorkMinutes = Math.Round(actualMinutes, 2),
            Message = string.Format(AttendanceMessages.CheckOutSuccess, currentLocalTime.ToString("HH:mm:ss"))
        };

        return ApiResponse<AttendanceCheckOutResultDto>.Ok(resultDto, resultDto.Message);
    }
}
