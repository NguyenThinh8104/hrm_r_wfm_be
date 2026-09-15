using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Security;

using Shared.Services;

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
    private readonly IRedisOtpService _redisOtpService;
    private readonly IS3StorageService _s3StorageService;

    public AttendanceService(
        AppDbContext context,
        IKioskContext kioskContext,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider,
        IRedisOtpService redisOtpService,
        IS3StorageService s3StorageService)
    {
        _context = context;
        _kioskContext = kioskContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _redisOtpService = redisOtpService;
        _s3StorageService = s3StorageService;
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

        if (!await ValidatePinOrOtpAsync(user, request.PinCode, consumeOtp: false))
        {
            return ApiResponse<ValidatePinResponseDto>.Fail(AttendanceMessages.InvalidPinOrOtp);
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

        if (!await ValidatePinOrOtpAsync(user, request.PinCode, consumeOtp: true))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPinOrOtp);
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

        var existingLog = await _context.AttendanceLogs.FirstOrDefaultAsync(al => al.AssignmentId == assignment.Id);
        if (existingLog != null && existingLog.CheckInTime != default)
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

        var validKioskId = await GetValidKioskIdAsync(request.KioskId);

        AttendanceLog log;
        if (existingLog != null)
        {
            log = existingLog;
            log.BranchId = (ulong)request.StoreId;
            log.KioskId = validKioskId;
            log.CheckInTime = now;
            log.OpeningFloatCash = request.OpeningFloatCash;
            if (!string.IsNullOrWhiteSpace(request.PhotoKey)) log.CheckInPhotoKey = request.PhotoKey.Trim();
        }
        else
        {
            log = new AttendanceLog
            {
                AssignmentId = assignment.Id,
                BranchId = (ulong)request.StoreId,
                KioskId = validKioskId,
                CheckInTime = now,
                OpeningFloatCash = request.OpeningFloatCash,
                CheckInPhotoKey = !string.IsNullOrWhiteSpace(request.PhotoKey) ? request.PhotoKey.Trim() : null,
                CreatedAt = now
            };
            _context.AttendanceLogs.Add(log);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi lưu điểm danh Check-in: {Message}", ex.InnerException?.Message ?? ex.Message);
            return ApiResponse<AttendanceRecordDto>.Fail($"Không thể lưu bản ghi điểm danh: {ex.InnerException?.Message ?? ex.Message}");
        }

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

        if (!await ValidatePinOrOtpAsync(user, request.PinCode, consumeOtp: true))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidPinOrOtp);
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
        if (!string.IsNullOrWhiteSpace(request.PhotoKey)) log.CheckOutPhotoKey = request.PhotoKey.Trim();
        var validKioskId = await GetValidKioskIdAsync(request.KioskId);
        if (validKioskId.HasValue) log.KioskId = validKioskId.Value;
        assignment.Status = "COMPLETED";

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi lưu điểm danh Check-out: {Message}", ex.InnerException?.Message ?? ex.Message);
            return ApiResponse<AttendanceRecordDto>.Fail($"Không thể lưu bản ghi Check-out: {ex.InnerException?.Message ?? ex.Message}");
        }

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

        if (!await ValidatePinOrOtpAsync(user, pin, consumeOtp: true))
        {
            return ApiResponse<AttendanceLogDto>.Fail(AttendanceMessages.InvalidPinOrOtp);
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
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi lưu điểm danh CheckInAsync: {Message}", ex.InnerException?.Message ?? ex.Message);
            return ApiResponse<AttendanceLogDto>.Fail($"Không thể lưu bản ghi điểm danh: {ex.InnerException?.Message ?? ex.Message}");
        }

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

        if (!await ValidatePinOrOtpAsync(user, pin, consumeOtp: true))
        {
            return ApiResponse<AttendanceCheckOutResultDto>.Fail(AttendanceMessages.InvalidPinOrOtp);
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi khi lưu điểm danh CheckOutAsync: {Message}", ex.InnerException?.Message ?? ex.Message);
            return ApiResponse<AttendanceCheckOutResultDto>.Fail($"Không thể lưu bản ghi Check-out: {ex.InnerException?.Message ?? ex.Message}");
        }

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

    /// <summary>
    /// Tra cứu tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý tại trạm Kiosk.
    /// </summary>
    public async Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null)
    {
        var dbQuery = _context.Users
            .Include(u => u.Role)
            .Where(u => u.HomeBranchId == (ulong)storeId && u.Status == "ACTIVE");

        if (!string.IsNullOrWhiteSpace(query))
        {
            var cleanQuery = query.Trim().ToLower();
            dbQuery = dbQuery.Where(u => u.EmployeeCode.ToLower().Contains(cleanQuery) 
                                      || u.FullName.ToLower().Contains(cleanQuery));
        }

        var users = await dbQuery.Take(20).ToListAsync();
        var result = users.Select(u => new KioskEmployeeSearchDto
        {
            EmployeeId = (int)u.Id,
            EmployeeCode = u.EmployeeCode,
            FullName = u.FullName,
            PositionName = u.Role?.RoleName ?? string.Empty,
            RoleCode = u.Role?.RoleCode ?? string.Empty,
            StoreId = storeId
        }).ToList();

        return ApiResponse<List<KioskEmployeeSearchDto>>.Ok(result);
    }

    /// <summary>
    /// Lấy quân số theo dõi trực tiếp thời gian thực tại cửa hàng kèm Presigned Temporary URL ảnh S3.
    /// </summary>
    public async Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
                .ThenInclude(al => al!.FraudFlaggedByUser)
            .Where(sa => sa.Schedule.BranchId == storeId && sa.Schedule.WorkDate == targetDate && sa.Status != "CANCELLED")
            .OrderBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var result = assignments.Select(sa =>
        {
            var att = sa.AttendanceLog;
            string status = "ABSENT";
            if (att != null)
            {
                if (att.IsFraudFlagged) status = "FRAUD_FLAGGED";
                else if (att.CheckOutTime.HasValue) status = "COMPLETED";
                else if (att.CheckInTime != default) status = "PRESENT";
            }

            string? checkInPhotoUrl = _s3StorageService.GetPresignedUrl(att?.CheckInPhotoKey);
            string? checkOutPhotoUrl = _s3StorageService.GetPresignedUrl(att?.CheckOutPhotoKey);

            return new LiveRosterDto
            {
                AttendanceId = att?.Id,
                AssignmentId = sa.Id,
                UserId = sa.UserId,
                EmployeeCode = sa.User.EmployeeCode,
                FullName = sa.User.FullName,
                PositionName = sa.User.Role?.RoleName ?? string.Empty,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                CheckInPhotoPresignedUrl = checkInPhotoUrl,
                CheckOutPhotoPresignedUrl = checkOutPhotoUrl,
                IsFraudFlagged = att?.IsFraudFlagged ?? false,
                FraudReason = att?.FraudReason,
                ReportedByName = att?.FraudFlaggedByUser?.FullName,
                Status = status
            };
        }).ToList();

        return ApiResponse<List<LiveRosterDto>>.Ok(result);
    }

    /// <summary>
    /// Store Manager phân xử khiếu nại (Duyệt khôi phục giờ công hoặc Bác bỏ).
    /// </summary>
    public async Task<ApiResponse<bool>> ResolveFraudAsync(ResolveFraudDto request)
    {
        var log = await _context.AttendanceLogs
            .Include(al => al.Assignment)
            .FirstOrDefaultAsync(al => al.Id == request.AttendanceId);

        if (log == null) return ApiResponse<bool>.Fail("Không tìm thấy bản ghi điểm danh cần phân xử.");

        if (request.IsApproved)
        {
            log.IsFraudFlagged = false;
            log.FraudReason = null;
            if (log.Assignment != null)
            {
                log.Assignment.Status = log.CheckOutTime.HasValue ? "COMPLETED" : "CONFIRMED";
            }
        }

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, request.IsApproved ? "Đã duyệt khôi phục giờ công thành công." : "Đã giữ nguyên cờ vi phạm.");
    }

    /// <summary>
    /// Điểm danh Check-in V3 trên Kiosk: Xác thực Kiosk Token, mã OTP 60s và chụp/upload ảnh S3.
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> CheckInV3Async(KioskCheckInV3Dto request)
    {
        if (string.IsNullOrWhiteSpace(request.KioskDeviceToken))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidKioskDeviceToken);
        }

        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.DeviceToken == request.KioskDeviceToken.Trim() && k.Status == "ACTIVE");

        if (kiosk == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.KioskNotActive);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.Status == "ACTIVE");
        if (user == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        // Xác thực & Hủy mã OTP 60s từ Redis
        var isOtpValid = await _redisOtpService.VerifyAndConsumeOtpAsync(request.UserId, request.OtpCode, "CHECK_IN");
        if (!isOtpValid)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidCheckInOtp);
        }

        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);

        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
            .FirstOrDefaultAsync(sa => sa.UserId == user.Id && sa.Schedule.BranchId == kiosk.BranchId && sa.Schedule.WorkDate == today);

        if (assignment == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail($"Nhân viên {user.FullName} không có ca làm việc được phân công hôm nay tại cửa hàng {kiosk.Branch.Name}.");
        }

        if (assignment.AttendanceLog != null && assignment.AttendanceLog.CheckInTime != default)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.AlreadyCheckedInShift);
        }

        // Upload ảnh chân dung lên S3
        string? photoS3Key = null;
        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            photoS3Key = await _s3StorageService.UploadBase64ImageAsync(request.ImageBase64, "attendance/checkin");
        }

        var shiftStartDt = today.ToDateTime(assignment.Schedule.ShiftTemplate.StartTime);
        bool isLate = now > shiftStartDt.AddMinutes(15);
        string status = isLate ? "Late" : "Present";

        var log = new AttendanceLog
        {
            AssignmentId = assignment.Id,
            BranchId = kiosk.BranchId,
            KioskId = kiosk.Id,
            CheckInTime = now,
            CheckInPhotoKey = photoS3Key,
            CreatedAt = now
        };

        _context.AttendanceLogs.Add(log);
        await _context.SaveChangesAsync();

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = (int)log.Id,
            AssignmentId = (int)assignment.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            StoreId = (int)kiosk.BranchId,
            KioskId = (int)kiosk.Id,
            StoreName = kiosk.Branch.Name,
            CheckInTime = log.CheckInTime,
            CheckInMethod = "Kiosk_OTP_S3",
            Status = status,
            HasException = false
        }, $"Check-in thành công lúc {now:HH:mm:ss}!");
    }

    /// <summary>
    /// Điểm danh Check-out V3 trên Kiosk: Xác thực Kiosk Token, mã OTP 60s, chụp/upload ảnh S3 và tính giờ công.
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> CheckOutV3Async(KioskCheckOutV3Dto request)
    {
        if (string.IsNullOrWhiteSpace(request.KioskDeviceToken))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidKioskDeviceToken);
        }

        var kiosk = await _context.KioskDevices
            .Include(k => k.Branch)
            .FirstOrDefaultAsync(k => k.DeviceToken == request.KioskDeviceToken.Trim() && k.Status == "ACTIVE");

        if (kiosk == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.KioskNotActive);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.Status == "ACTIVE");
        if (user == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        var isOtpValid = await _redisOtpService.VerifyAndConsumeOtpAsync(request.UserId, request.OtpCode, "CHECK_OUT");
        if (!isOtpValid)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidCheckOutOtp);
        }

        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
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
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NoOpenAttendanceLogForCheckOut);
        }

        string? photoS3Key = null;
        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            photoS3Key = await _s3StorageService.UploadBase64ImageAsync(request.ImageBase64, "attendance/checkout");
        }

        log.CheckOutTime = now;
        log.CheckOutPhotoKey = photoS3Key;
        log.KioskId = kiosk.Id;

        if (log.Assignment != null)
        {
            log.Assignment.Status = "COMPLETED";
        }

        await _context.SaveChangesAsync();

        double workMinutes = Math.Round(log.ActualWorkMinutes ?? 0, 1);
        int hours = (int)(workMinutes / 60);
        int mins = (int)(workMinutes % 60);

        return ApiResponse<AttendanceRecordDto>.Ok(new AttendanceRecordDto
        {
            AttendanceId = (int)log.Id,
            AssignmentId = (int)log.AssignmentId,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            StoreId = (int)kiosk.BranchId,
            KioskId = (int)kiosk.Id,
            StoreName = kiosk.Branch.Name,
            CheckInTime = log.CheckInTime,
            CheckOutTime = log.CheckOutTime,
            CheckInMethod = "Kiosk_OTP_S3",
            CheckOutMethod = "Kiosk_OTP_S3",
            Status = "Completed"
        }, $"Tạm biệt! Bạn đã hoàn thành ca làm việc lúc {now:HH:mm:ss}. Tổng thời gian: {hours} giờ {mins} phút.");
    }

    private async Task<bool> ValidatePinOrOtpAsync(User user, string inputCode, bool consumeOtp = false)
    {
        if (user == null || string.IsNullOrWhiteSpace(inputCode)) return false;
        var trimmedCode = inputCode.Trim();

        // 1. Kiểm tra mã xác thực OTP 60s trên Redis trước
        var isOtpValid = await _redisOtpService.VerifyOtpAsync(user.Id, trimmedCode, consume: consumeOtp);
        if (isOtpValid) return true;

        // 2. Dự phòng: Kiểm tra Mã PIN cá nhân nếu có
        if (!string.IsNullOrEmpty(user.KioskPinHash) && _passwordHasher.Verify(trimmedCode, user.KioskPinHash))
        {
            return true;
        }

        return false;
    }

    private async Task<ulong?> GetValidKioskIdAsync(int? kioskId)
    {
        if (!kioskId.HasValue || kioskId.Value <= 0) return null;
        ulong id = (ulong)kioskId.Value;
        bool exists = await _context.KioskDevices.AnyAsync(k => k.Id == id);
        return exists ? id : null;
    }

    public async Task<ApiResponse<MyWeeklyScheduleDto>> GetMyWeeklyScheduleAsync(ulong userId, DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().LocalDateTime);

        var assignments = await _context.ShiftAssignments
            .AsNoTracking()
            .Include(a => a.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(a => a.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(a => a.AttendanceLog)
            .Where(a => a.UserId == userId && a.Schedule.WorkDate >= weekStart && a.Schedule.WorkDate <= weekEnd)
            .OrderBy(a => a.Schedule.WorkDate)
            .ThenBy(a => a.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var dispatches = await _context.TemporaryDispatches
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.Status == "APPROVED" && d.StartDate <= weekEnd && d.EndDate >= weekStart)
            .ToListAsync();

        var weeklyDto = new MyWeeklyScheduleDto
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Days = new List<MyDayScheduleDto>()
        };

        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var dayAssignments = assignments.Where(a => a.Schedule.WorkDate == date).ToList();

            var dayDto = new MyDayScheduleDto
            {
                Date = date,
                DayOfWeek = GetVietnameseDayOfWeek(date.DayOfWeek),
                Shifts = new List<MyShiftSlotDto>()
            };

            foreach (var a in dayAssignments)
            {
                bool isDispatched = dispatches.Any(d => d.StartDate <= date && d.EndDate >= date);

                string attendanceStatus = "NOT_YET";
                if (a.AttendanceLog != null)
                {
                    attendanceStatus = a.AttendanceLog.CheckOutTime.HasValue ? "COMPLETED" : "CHECKED_IN";
                }
                else if (date < today)
                {
                    attendanceStatus = "ABSENT";
                }

                dayDto.Shifts.Add(new MyShiftSlotDto
                {
                    AssignmentId = a.Id,
                    ShiftName = a.Schedule.ShiftTemplate?.Name ?? string.Empty,
                    TemplateCode = a.Schedule.ShiftTemplate?.TemplateCode ?? string.Empty,
                    StartTime = a.Schedule.ShiftTemplate?.StartTime ?? default,
                    EndTime = a.Schedule.ShiftTemplate?.EndTime ?? default,
                    BranchId = a.Schedule.BranchId,
                    BranchName = a.Schedule.Branch?.Name ?? string.Empty,
                    BranchAddress = a.Schedule.Branch?.Address ?? string.Empty,
                    IsDispatched = isDispatched,
                    CheckInTime = a.AttendanceLog?.CheckInTime,
                    CheckOutTime = a.AttendanceLog?.CheckOutTime,
                    AttendanceStatus = attendanceStatus
                });
            }

            weeklyDto.Days.Add(dayDto);
        }

        return ApiResponse<MyWeeklyScheduleDto>.Ok(weeklyDto);
    }

    public async Task<ApiResponse<MyAttendanceHistoryDto>> GetMyAttendanceHistoryAsync(ulong userId, int month, int year)
    {
        var monthStart = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var monthEnd = new DateOnly(year, month, daysInMonth);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().LocalDateTime);

        var assignments = await _context.ShiftAssignments
            .AsNoTracking()
            .Include(a => a.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(a => a.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(a => a.AttendanceLog)
            .Where(a => a.UserId == userId && a.Schedule.WorkDate >= monthStart && a.Schedule.WorkDate <= monthEnd)
            .OrderBy(a => a.Schedule.WorkDate)
            .ThenBy(a => a.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var dispatches = await _context.TemporaryDispatches
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.Status == "APPROVED" && d.StartDate <= monthEnd && d.EndDate >= monthStart)
            .ToListAsync();

        var historyDetails = new List<MyAttendanceDayDto>();
        double totalWorkHours = 0;

        foreach (var a in assignments)
        {
            var date = a.Schedule.WorkDate;
            bool isDispatched = dispatches.Any(d => d.StartDate <= date && d.EndDate >= date);

            string status = "NOT_YET";
            double? actualMinutes = null;

            if (a.AttendanceLog != null)
            {
                if (a.AttendanceLog.CheckOutTime.HasValue)
                {
                    status = "PRESENT";
                    actualMinutes = (a.AttendanceLog.CheckOutTime.Value - a.AttendanceLog.CheckInTime).TotalMinutes;
                    if (actualMinutes > 0)
                    {
                        totalWorkHours += actualMinutes.Value / 60.0;
                    }
                }
                else
                {
                    status = "INCOMPLETE";
                }
            }
            else if (date < today)
            {
                status = "ABSENT";
            }

            historyDetails.Add(new MyAttendanceDayDto
            {
                Date = date,
                DayOfWeek = GetVietnameseDayOfWeek(date.DayOfWeek),
                ShiftName = a.Schedule.ShiftTemplate?.Name ?? string.Empty,
                ShiftStart = a.Schedule.ShiftTemplate?.StartTime ?? default,
                ShiftEnd = a.Schedule.ShiftTemplate?.EndTime ?? default,
                BranchName = a.Schedule.Branch?.Name ?? string.Empty,
                CheckInTime = a.AttendanceLog?.CheckInTime,
                CheckOutTime = a.AttendanceLog?.CheckOutTime,
                ActualWorkMinutes = actualMinutes.HasValue ? Math.Round(actualMinutes.Value, 1) : null,
                Status = status,
                IsDispatched = isDispatched
            });
        }

        int totalAssigned = assignments.Count;
        int totalWorked = assignments.Count(a => a.AttendanceLog != null);
        int totalAbsent = assignments.Count(a => a.AttendanceLog == null && a.Schedule.WorkDate < today);

        var result = new MyAttendanceHistoryDto
        {
            Month = month,
            Year = year,
            TotalAssignedShifts = totalAssigned,
            TotalWorkedShifts = totalWorked,
            TotalAbsentShifts = totalAbsent,
            TotalWorkHours = Math.Round(totalWorkHours, 1),
            AbsentPercentage = totalAssigned > 0 ? Math.Round((double)totalAbsent / totalAssigned * 100, 1) : 0,
            AttendanceRate = totalAssigned > 0 ? Math.Round((double)totalWorked / totalAssigned * 100, 1) : 0,
            Details = historyDetails
        };

        return ApiResponse<MyAttendanceHistoryDto>.Ok(result);
    }

    private static string GetVietnameseDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => string.Empty
        };
    }
}



