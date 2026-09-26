using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly IRedisOtpService _redisOtpService;
    private readonly IS3StorageService _s3StorageService;

    public AttendanceService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider,
        IRedisOtpService redisOtpService,
        IS3StorageService s3StorageService)
    {
        _context = context;
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
            .FirstOrDefaultAsync(sa => sa.UserId == user.Id && sa.Schedule.BranchId == (ulong)request.StoreId && sa.Schedule.WorkDate == today && sa.Status != "CANCELLED");

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
            .Where(sa => sa.Schedule.BranchId == (ulong)storeId && sa.Schedule.WorkDate == date && sa.Status != "CANCELLED")
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
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
            .Include(al => al.Branch)
            .Include(al => al.FraudFlaggedByUser)
            .Where(al => al.BranchId == (ulong)storeId && DateOnly.FromDateTime(al.CreatedAt) == date)
            .OrderByDescending(al => al.CreatedAt)
            .ToListAsync();

        var result = records.Select(al =>
        {
            var shiftTemplate = al.Assignment?.Schedule?.ShiftTemplate;
            bool isLate = al.IsLate;

            string status = al.IsFraudFlagged
                ? "Fraud"
                : (al.CheckOutTime.HasValue
                    ? (isLate ? "Completed (Late)" : "Completed")
                    : (isLate ? "Late" : "Present"));

            return new AttendanceRecordDto
            {
                AttendanceId = (int)al.Id,
                AssignmentId = (int)al.AssignmentId,
                EmployeeId = (int)(al.Assignment?.UserId ?? 0),
                EmployeeName = al.Assignment?.User?.FullName ?? string.Empty,
                EmployeeCode = al.Assignment?.User?.EmployeeCode ?? string.Empty,
                StoreId = (int)al.BranchId,
                KioskId = al.KioskId.HasValue ? (int)al.KioskId.Value : null,
                StoreName = al.Branch?.Name ?? string.Empty,
                ShiftName = shiftTemplate?.Name,
                CheckInTime = al.CheckInTime,
                CheckOutTime = al.CheckOutTime,
                CheckInMethod = "Kiosk",
                CheckOutMethod = al.CheckOutTime.HasValue ? "Kiosk" : null,
                Status = status,
                AttendanceLogStatus = al.Status.ToString(),
                CheckInStatus = al.CheckInStatus.ToString(),
                CheckOutStatus = al.CheckOutStatus?.ToString(),
                LateMinutes = al.LateMinutes,
                EarlyLeaveMinutes = al.EarlyLeaveMinutes,
                ActualWorkMinutes = al.ActualWorkMinutes.HasValue ? Math.Round(al.ActualWorkMinutes.Value, 1) : null,
                IsLate = isLate,
                HasException = al.IsFraudFlagged || isLate,
                ExceptionReason = al.IsFraudFlagged ? al.FraudReason : (isLate ? "Đi muộn" : null),
                ReportedByName = al.FraudFlaggedByUser != null ? al.FraudFlaggedByUser.FullName : null
            };
        }).ToList();

        return ApiResponse<List<AttendanceRecordDto>>.Ok(result);
    }



    /// <summary>
    /// Tra cứu tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý tại trạm Kiosk.
    /// Bao gồm nhân viên cơ sở gốc (HomeBranchId) và nhân viên đang được điều động hợp lệ đến cửa hàng hôm nay.
    /// </summary>
    public async Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        // Lấy danh sách ID nhân viên đang có lệnh điều động hợp lệ đến cửa hàng này hôm nay
        var dispatchedUserIds = await _context.TemporaryDispatches
            .Where(d => d.TargetBranchId == (ulong)storeId 
                     && d.Status == "APPROVED" 
                     && d.StartDate <= today 
                     && today <= d.EndDate)
            .Select(d => d.UserId)
            .ToListAsync();

        var dbQuery = _context.Users
            .Include(u => u.Role)
            .Where(u => (u.HomeBranchId == (ulong)storeId || dispatchedUserIds.Contains(u.Id)) && u.Status == "ACTIVE");

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
            bool isLate = att != null && att.IsLate;

            string status = "ABSENT";
            if (att != null)
            {
                if (att.IsFraudFlagged) status = "FRAUD_FLAGGED";
                else status = att.Status.ToString();
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
                IsLate = isLate,
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
    /// Điểm danh Check-in tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, userId bóc tách từ Redis.
    /// Tạo record với Status = PENDING (chờ upload ảnh bắt buộc).
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> CheckInAsync(KioskCheckInDto request)
    {
        // 1. Validate KioskDeviceToken
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

        // 2. Xác thực OTP & bóc tách userId từ Redis (không tin tưởng payload)
        var otpResult = await _redisOtpService.VerifyAndConsumeAttendanceOtpAsync(request.OtpCode);
        if (otpResult == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidAttendanceOtp);
        }

        var (userId, otpType) = otpResult.Value;

        // 3. Kiểm tra otpType phải là CHECK_IN
        if (!string.Equals(otpType, "CHECK_IN", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                string.Format(AttendanceMessages.OtpTypeMismatch, "CHECK_IN"));
        }

        // 4. Tìm User bằng userId từ Redis
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Status == "ACTIVE");
        if (user == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        // 5. Tìm danh sách ShiftAssignment tại chi nhánh của Kiosk (bao gồm hôm nay và ca đêm bắt đầu từ hôm qua)
        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var yesterday = today.AddDays(-1);

        var activeAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
            .Where(sa => sa.UserId == user.Id &&
                         sa.Schedule.BranchId == kiosk.BranchId &&
                         sa.Status != "CANCELLED" &&
                         (sa.Schedule.WorkDate == today ||
                          (sa.Schedule.ShiftTemplate.IsOvernight && sa.Schedule.WorkDate == yesterday)))
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        if (activeAssignments.Count == 0)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                string.Format(AttendanceMessages.NoShiftToday, user.FullName, today.ToString("dd/MM/yyyy")));
        }

        // Lọc các ca chưa điểm danh Check-in hoàn tất (hoặc chưa có log hoặc log vẫn đang PENDING chưa upload ảnh)
        var unattendedAssignments = activeAssignments
            .Where(sa => sa.AttendanceLog == null || sa.AttendanceLog.CheckInTime == default || (sa.AttendanceLog.Status == AttendanceLogStatus.PENDING && string.IsNullOrEmpty(sa.AttendanceLog.CheckInPhotoKey)))
            .ToList();

        if (unattendedAssignments.Count == 0)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.AlreadyCheckedInShift);
        }

        // 6. So khớp ca làm việc dựa theo khung giờ
        // EarliestCheckIn: ShiftStart - 30 phút
        // LatestCheckIn: ShiftEnd (kết thúc ca)
        // GracePeriod: ShiftStart + 5 phút (sau mốc này đánh dấu LATE)
        var shiftTimeWindows = unattendedAssignments.Select(sa =>
        {
            var workDate = sa.Schedule.WorkDate;
            var template = sa.Schedule.ShiftTemplate;
            var startDt = workDate.ToDateTime(template.StartTime);
            var endDt = (template.IsOvernight || template.EndTime < template.StartTime)
                ? workDate.AddDays(1).ToDateTime(template.EndTime)
                : workDate.ToDateTime(template.EndTime);

            var earliestCheckIn = startDt.AddMinutes(-30);
            var latestCheckIn = endDt;

            return new
            {
                Assignment = sa,
                StartDt = startDt,
                EndDt = endDt,
                EarliestCheckIn = earliestCheckIn,
                LatestCheckIn = latestCheckIn,
                IsWithinWindow = now >= earliestCheckIn && now <= latestCheckIn
            };
        }).ToList();

        var matchedShift = shiftTimeWindows
            .Where(w => w.IsWithinWindow)
            .OrderBy(w => Math.Abs((w.StartDt - now).TotalMinutes))
            .FirstOrDefault();

        if (matchedShift == null)
        {
            // Kiểm tra xem đến quá sớm trước ca gần nhất hay không
            var upcomingShift = shiftTimeWindows
                .Where(w => now < w.EarliestCheckIn)
                .OrderBy(w => w.StartDt)
                .FirstOrDefault();

            if (upcomingShift != null)
            {
                return ApiResponse<AttendanceRecordDto>.Fail(string.Format(
                    AttendanceMessages.TooEarlyForShift,
                    upcomingShift.Assignment.Schedule.ShiftTemplate.Name,
                    upcomingShift.StartDt.ToString("HH:mm"),
                    upcomingShift.EarliestCheckIn.ToString("HH:mm")));
            }

            // Kiểm tra xem tất cả ca đều đã kết thúc hay không
            var endedShift = shiftTimeWindows
                .Where(w => now > w.LatestCheckIn)
                .OrderByDescending(w => w.EndDt)
                .FirstOrDefault();

            if (endedShift != null)
            {
                return ApiResponse<AttendanceRecordDto>.Fail(string.Format(
                    AttendanceMessages.ShiftAlreadyEnded,
                    endedShift.Assignment.Schedule.ShiftTemplate.Name,
                    endedShift.EndDt.ToString("HH:mm")));
            }

            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NoActiveShiftInTimeframe);
        }

        var assignment = matchedShift.Assignment;

        // 7. Tạo hoặc tái sử dụng AttendanceLog với Status = PENDING (chờ upload ảnh)
        AttendanceLog log;
        if (assignment.AttendanceLog != null && assignment.AttendanceLog.Status == AttendanceLogStatus.PENDING && string.IsNullOrEmpty(assignment.AttendanceLog.CheckInPhotoKey))
        {
            log = assignment.AttendanceLog;
            log.CheckInTime = now;
            log.BranchId = kiosk.BranchId;
            log.KioskId = kiosk.Id;
            log.CheckInStatus = CheckInStatus.PENDING;
            log.CreatedAt = now;
        }
        else
        {
            log = new AttendanceLog
            {
                AssignmentId = assignment.Id,
                BranchId = kiosk.BranchId,
                KioskId = kiosk.Id,
                CheckInTime = now,
                Status = AttendanceLogStatus.PENDING,
                CheckInStatus = CheckInStatus.PENDING,
                CreatedAt = now
            };
            _context.AttendanceLogs.Add(log);
        }
        await _context.SaveChangesAsync();

        var successMessage = string.Format(AttendanceMessages.CheckInSuccess, now.ToString("HH:mm:ss"));

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
            ShiftName = assignment.Schedule.ShiftTemplate.Name,
            CheckInTime = log.CheckInTime,
            CheckInMethod = "Kiosk_OTP",
            Status = "Pending",
            AttendanceLogStatus = log.Status.ToString(),
            CheckInStatus = log.CheckInStatus.ToString(),
            IsLate = false,
            HasException = false,
            ExceptionReason = null
        }, successMessage);
    }

    /// <summary>
    /// Điểm danh Check-out tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, userId bóc tách từ Redis.
    /// Cập nhật CheckOutTime với Status = PENDING (chờ upload ảnh bắt buộc).
    /// </summary>
    public async Task<ApiResponse<AttendanceRecordDto>> CheckOutAsync(KioskCheckOutDto request)
    {
        // 1. Validate KioskDeviceToken
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

        // 2. Xác thực OTP & bóc tách userId từ Redis
        var otpResult = await _redisOtpService.VerifyAndConsumeAttendanceOtpAsync(request.OtpCode);
        if (otpResult == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidAttendanceOtp);
        }

        var (userId, otpType) = otpResult.Value;

        // 3. Kiểm tra otpType phải là CHECK_OUT
        if (!string.Equals(otpType, "CHECK_OUT", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                string.Format(AttendanceMessages.OtpTypeMismatch, "CHECK_OUT"));
        }

        // 4. Tìm User bằng userId từ Redis
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Status == "ACTIVE");
        if (user == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

        // 5. Tìm open attendance log (chưa checkout)
        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var yesterday = today.AddDays(-1);

        var log = await _context.AttendanceLogs
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
            .Where(al => al.Assignment.UserId == user.Id &&
                         al.CheckOutPhotoKey == null &&
                         (al.Assignment.Schedule.WorkDate == today ||
                          (al.Assignment.Schedule.ShiftTemplate.IsOvernight && al.Assignment.Schedule.WorkDate == yesterday)))
            .OrderByDescending(al => al.CheckInTime)
            .FirstOrDefaultAsync();

        if (log == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NoOpenAttendanceLogForCheckOut);
        }

        // 6. Cập nhật CheckOutTime và CheckOutStatus = PENDING (chờ chụp ảnh xác thực)
        log.CheckOutTime = now;
        log.KioskId = kiosk.Id;
        log.CheckOutStatus = CheckOutStatus.PENDING;

        await _context.SaveChangesAsync();

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
            ShiftName = log.Assignment?.Schedule?.ShiftTemplate?.Name,
            CheckInTime = log.CheckInTime,
            CheckOutTime = log.CheckOutTime,
            CheckInMethod = "Kiosk_OTP",
            CheckOutMethod = "Kiosk_OTP",
            Status = "Pending",
            AttendanceLogStatus = "PENDING",
            CheckInStatus = log.CheckInStatus.ToString(),
            CheckOutStatus = "PENDING",
            IsLate = log.IsLate
        }, string.Format(AttendanceMessages.CheckOutSuccess, now.ToString("HH:mm:ss")));
    }

    /// <summary>
    /// Upload ảnh chấm công (bắt buộc) lên S3 và cập nhật attendance record → COMPLETED.
    /// </summary>
    public async Task<ApiResponse<UploadAttendancePhotoResponseDto>> UploadAttendancePhotoAsync(UploadAttendancePhotoDto request)
    {
        // 1. Validate KioskDeviceToken
        if (string.IsNullOrWhiteSpace(request.KioskDeviceToken))
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.InvalidKioskDeviceToken);
        }

        var kiosk = await _context.KioskDevices
            .FirstOrDefaultAsync(k => k.DeviceToken == request.KioskDeviceToken.Trim() && k.Status == "ACTIVE");

        if (kiosk == null)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.KioskNotActive);
        }

        // 2. Tìm AttendanceLog theo attendanceId
        var log = await _context.AttendanceLogs.FindAsync(request.AttendanceId);
        if (log == null)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceRecordNotFound);
        }

        // 3. Validate: record phải thuộc Kiosk này
        if (log.KioskId != kiosk.Id)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogNotOwnedByKiosk);
        }

        // 4. Validate PhotoType
        var photoType = request.PhotoType?.Trim().ToUpper();
        if (photoType != "CHECK_IN" && photoType != "CHECK_OUT")
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.InvalidPhotoType);
        }

        // 5. Kiểm tra hợp lệ theo trạng thái hiện tại
        if (photoType == "CHECK_IN" && log.Status != AttendanceLogStatus.PENDING)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogAlreadyCompleted);
        }
        if (photoType == "CHECK_OUT" && !string.IsNullOrEmpty(log.CheckOutPhotoKey))
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogAlreadyCompleted);
        }

        // 6. Validate ImageBase64 không rỗng
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.UploadAttendancePhotoV3Failed);
        }

        // 7. Upload ảnh lên S3
        var folderName = photoType == "CHECK_IN" ? "attendance/checkin" : "attendance/checkout";
        string photoS3Key;
        try
        {
            photoS3Key = await _s3StorageService.UploadBase64ImageAsync(request.ImageBase64, folderName);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Lỗi upload ảnh chấm công lên S3: {Message}", ex.Message);
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.UploadAttendancePhotoV3Failed);
        }

        // 8. Cập nhật attendance record với photoKey và tính toán trạng thái chuyên cần
        bool isLate = false;
        string detailedMessage = AttendanceMessages.UploadAttendancePhotoV3Success;

        var assignment = await _context.ShiftAssignments
            .Include(a => a.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .FirstOrDefaultAsync(a => a.Id == log.AssignmentId);

        var workDate = assignment?.Schedule?.WorkDate ?? DateOnly.FromDateTime(log.CheckInTime);
        var template = assignment?.Schedule?.ShiftTemplate;

        if (photoType == "CHECK_IN")
        {
            log.CheckInPhotoKey = photoS3Key;

            var shiftStartDt = template != null
                ? workDate.ToDateTime(template.StartTime)
                : log.CheckInTime;

            if (log.CheckInTime < shiftStartDt)
            {
                int earlyMins = (int)Math.Max(0, Math.Round((shiftStartDt - log.CheckInTime).TotalMinutes));
                log.CheckInStatus = CheckInStatus.EARLY;
                log.LateMinutes = 0;
                log.Status = AttendanceLogStatus.PRESENT;
                detailedMessage = string.Format(AttendanceMessages.CheckInEarlySuccess, earlyMins, log.CheckInTime.ToString("HH:mm:ss"));
            }
            else if (log.CheckInTime <= shiftStartDt.AddMinutes(5))
            {
                log.CheckInStatus = CheckInStatus.ON_TIME;
                log.LateMinutes = 0;
                log.Status = AttendanceLogStatus.PRESENT;
                detailedMessage = string.Format(AttendanceMessages.CheckInOnTimeSuccess, log.CheckInTime.ToString("HH:mm:ss"));
            }
            else
            {
                isLate = true;
                int lateMins = (int)Math.Max(1, Math.Round((log.CheckInTime - shiftStartDt).TotalMinutes));
                log.CheckInStatus = CheckInStatus.LATE;
                log.LateMinutes = lateMins;
                log.Status = AttendanceLogStatus.LATE;
                detailedMessage = string.Format(AttendanceMessages.CheckInLateDetailedSuccess, log.CheckInTime.ToString("HH:mm:ss"), lateMins, shiftStartDt.ToString("HH:mm"));
            }
        }
        else
        {
            log.CheckOutPhotoKey = photoS3Key;

            DateTime shiftEndDt;
            if (template != null)
            {
                shiftEndDt = (template.IsOvernight || template.EndTime < template.StartTime)
                    ? workDate.AddDays(1).ToDateTime(template.EndTime)
                    : workDate.ToDateTime(template.EndTime);
            }
            else
            {
                shiftEndDt = log.CheckOutTime ?? log.CheckInTime.AddHours(8);
            }

            var checkOutTime = log.CheckOutTime ?? _timeProvider.GetLocalNow().DateTime;
            log.CheckOutTime = checkOutTime;

            if (checkOutTime < shiftEndDt.AddMinutes(-5))
            {
                int earlyLeaveMins = (int)Math.Max(1, Math.Round((shiftEndDt - checkOutTime).TotalMinutes));
                log.CheckOutStatus = CheckOutStatus.EARLY_LEAVE;
                log.EarlyLeaveMinutes = earlyLeaveMins;
                detailedMessage = string.Format(AttendanceMessages.CheckOutEarlySuccess, checkOutTime.ToString("HH:mm:ss"), earlyLeaveMins, shiftEndDt.ToString("HH:mm"));
            }
            else if (checkOutTime <= shiftEndDt.AddMinutes(15))
            {
                log.CheckOutStatus = CheckOutStatus.ON_TIME;
                log.EarlyLeaveMinutes = 0;
                detailedMessage = string.Format(AttendanceMessages.CheckOutOnTimeSuccess, checkOutTime.ToString("HH:mm:ss"));
            }
            else
            {
                int lateLeaveMins = (int)Math.Max(1, Math.Round((checkOutTime - shiftEndDt).TotalMinutes));
                log.CheckOutStatus = CheckOutStatus.LATE_LEAVE;
                detailedMessage = string.Format(AttendanceMessages.CheckOutLateSuccess, checkOutTime.ToString("HH:mm:ss"), lateLeaveMins);
            }

            isLate = log.IsLate;
            log.Status = (log.CheckInStatus == CheckInStatus.LATE || log.Status == AttendanceLogStatus.LATE || log.Status == AttendanceLogStatus.COMPLETED_LATE)
                ? AttendanceLogStatus.COMPLETED_LATE
                : AttendanceLogStatus.COMPLETED;

            if (assignment != null)
            {
                assignment.Status = "COMPLETED";
            }
        }

        await _context.SaveChangesAsync();

        var presignedUrl = _s3StorageService.GetPresignedUrl(photoS3Key);

        return ApiResponse<UploadAttendancePhotoResponseDto>.Ok(new UploadAttendancePhotoResponseDto
        {
            AttendanceId = log.Id,
            PhotoKey = photoS3Key,
            PresignedUrl = presignedUrl,
            Status = log.Status.ToString(),
            IsLate = isLate,
            CheckInStatus = log.CheckInStatus.ToString(),
            CheckOutStatus = log.CheckOutStatus?.ToString(),
            LateMinutes = log.LateMinutes,
            EarlyLeaveMinutes = log.EarlyLeaveMinutes,
            ActualWorkMinutes = log.ActualWorkMinutes.HasValue ? Math.Round(log.ActualWorkMinutes.Value, 1) : null,
            DetailedMessage = detailedMessage
        }, detailedMessage);
    }

    /// <summary>
    /// Hủy lượt điểm danh tại trạm Kiosk khi nhân viên dừng ở bước chụp ảnh hoặc muốn nhường người khác.
    /// </summary>
    public async Task<ApiResponse<bool>> CancelAttendanceAsync(CancelKioskAttendanceDto request)
    {
        if (string.IsNullOrWhiteSpace(request.KioskDeviceToken))
        {
            return ApiResponse<bool>.Fail(AttendanceMessages.InvalidKioskDeviceToken);
        }

        var kiosk = await _context.KioskDevices
            .FirstOrDefaultAsync(k => k.DeviceToken == request.KioskDeviceToken.Trim() && k.Status == "ACTIVE");

        if (kiosk == null)
        {
            return ApiResponse<bool>.Fail(AttendanceMessages.KioskNotActive);
        }

        var log = await _context.AttendanceLogs
            .Include(l => l.Assignment)
            .FirstOrDefaultAsync(l => l.Id == request.AttendanceId && l.KioskId == kiosk.Id);

        if (log == null)
        {
            return ApiResponse<bool>.Fail(AttendanceMessages.AttendanceRecordNotFound);
        }

        var actionType = request.ActionType?.Trim().ToUpper() ?? "CHECK_IN";

        if (actionType == "CHECK_IN")
        {
            // Chỉ cho phép hủy nếu chưa upload ảnh Check-in thành công
            if (log.Status != AttendanceLogStatus.PENDING || !string.IsNullOrEmpty(log.CheckInPhotoKey))
            {
                return ApiResponse<bool>.Fail(AttendanceMessages.CancelAttendanceFailed);
            }

            _context.AttendanceLogs.Remove(log);
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, AttendanceMessages.CancelAttendanceSuccess);
        }
        else
        {
            // Check-out: Nếu chưa upload ảnh Check-out (CheckOutPhotoKey == null)
            if (!string.IsNullOrEmpty(log.CheckOutPhotoKey))
            {
                return ApiResponse<bool>.Fail(AttendanceMessages.CancelAttendanceFailed);
            }

            log.CheckOutTime = null;
            log.CheckOutStatus = null;
            log.Status = (log.CheckInStatus == CheckInStatus.LATE) ? AttendanceLogStatus.LATE : AttendanceLogStatus.PRESENT;
            if (log.Assignment != null)
            {
                log.Assignment.Status = "CONFIRMED";
            }

            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, AttendanceMessages.CancelAttendanceSuccess);
        }
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
                if (string.Equals(a.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    attendanceStatus = "CANCELLED";
                }
                else if (a.AttendanceLog != null)
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
                    AttendanceStatus = attendanceStatus,
                    AssignmentStatus = a.Status
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

            bool isLate = false;
            if (string.Equals(a.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                status = "CANCELLED";
            }
            else if (a.AttendanceLog != null)
            {
                var template = a.Schedule.ShiftTemplate;
                var shiftStartDt = template != null ? date.ToDateTime(template.StartTime) : a.AttendanceLog.CheckInTime;
                isLate = a.AttendanceLog.IsLate || (a.AttendanceLog.CheckInTime > shiftStartDt.AddMinutes(15));

                if (a.AttendanceLog.CheckOutTime.HasValue)
                {
                    status = isLate ? "LATE" : "PRESENT";
                    actualMinutes = (a.AttendanceLog.CheckOutTime.Value - a.AttendanceLog.CheckInTime).TotalMinutes;
                    if (actualMinutes > 0)
                    {
                        totalWorkHours += actualMinutes.Value / 60.0;
                    }
                }
                else
                {
                    status = isLate ? "LATE" : "INCOMPLETE";
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
                IsLate = isLate,
                IsDispatched = isDispatched
            });
        }

        int totalAssigned = assignments.Count(a => a.Status != "CANCELLED");
        int totalWorked = assignments.Count(a => a.AttendanceLog != null && a.Status != "CANCELLED");
        int totalAbsent = assignments.Count(a => a.AttendanceLog == null && a.Schedule.WorkDate < today && a.Status != "CANCELLED");

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



