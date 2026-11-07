using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Security;
using Shared.Services;

namespace Modules.Attendance.Services;

/// <summary>
/// Triển khai dịch vụ chấm công chuyên dụng tại máy trạm Kiosk cửa hàng.
/// </summary>
public class KioskAttendanceService : IKioskAttendanceService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IRedisOtpService _redisOtpService;
    private readonly IS3StorageService _s3StorageService;

    public KioskAttendanceService(
        AppDbContext context,
        TimeProvider timeProvider,
        IRedisOtpService redisOtpService,
        IS3StorageService s3StorageService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _redisOtpService = redisOtpService;
        _s3StorageService = s3StorageService;
    }

    /// <inheritdoc />
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
            .FirstOrDefaultAsync(sa => sa.UserId == user.Id && sa.Schedule.BranchId == (ulong)request.StoreId && sa.Schedule.WorkDate == today && sa.Schedule.Status == "PUBLISHED" && (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED"));

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

    /// <inheritdoc />
    public async Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
            .Where(sa => sa.Schedule.BranchId == (ulong)storeId && sa.Schedule.WorkDate == date && sa.Schedule.Status == "PUBLISHED" && sa.Status != "DRAFT" && sa.Status != "CANCELLED")
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
                ShiftName = sa.Schedule.EffectiveShiftName,
                StartTime = sa.Schedule.EffectiveStartTime,
                EndTime = sa.Schedule.EffectiveEndTime,
                HasCheckedIn = att?.CheckInTime != null,
                HasCheckedOut = att?.CheckOutTime != null,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                IsDispatched = sa.User.HomeBranchId != (ulong)storeId
            };
        }).ToList();

        return ApiResponse<List<KioskEmployeeRosterDto>>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        // Lấy danh sách ID nhân viên đang có lệnh điều động hợp lệ đến cửa hàng này hôm nay
        var dispatchedDispatches = await _context.TemporaryDispatches
            .Include(d => d.DispatchEmployees)
            .Where(d => d.TargetBranchId == (ulong)storeId 
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL")
                     && d.StartDate <= today 
                     && today <= d.EndDate)
            .ToListAsync();

        var dispatchedUserIds = dispatchedDispatches
            .SelectMany(d => d.DispatchEmployees.Any() 
                ? d.DispatchEmployees.Where(de => de.Status == "APPROVED").Select(de => de.UserId)
                : (d.UserId > 0 ? new[] { d.UserId } : Array.Empty<ulong>()))
            .Distinct()
            .ToList();

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

    /// <inheritdoc />
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

        if (kiosk.Branch == null || !string.Equals(kiosk.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                $"Chi nhánh '{kiosk.Branch?.Name ?? "của Kiosk"}' đã bị khóa. Không thể thực hiện chấm công.",
                new List<string> { "BRANCH_LOCKED" });
        }

        // 2. Xác thực OTP & bóc tách userId từ Redis
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
                         sa.Schedule.Status == "PUBLISHED" &&
                         (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED") &&
                         (sa.Schedule.WorkDate == today ||
                          ((sa.Schedule.SnapshotIsOvernight ?? sa.Schedule.ShiftTemplate.IsOvernight) && sa.Schedule.WorkDate == yesterday)))
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.SnapshotStartTime ?? sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        if (activeAssignments.Count == 0)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                string.Format(AttendanceMessages.NoShiftToday, user.FullName, today.ToString("dd/MM/yyyy")));
        }

        // Lọc các ca chưa điểm danh Check-in hoàn tất
        var unattendedAssignments = activeAssignments
            .Where(sa => sa.AttendanceLog == null || sa.AttendanceLog.CheckInTime == default || (sa.AttendanceLog.Status == AttendanceLogStatus.PENDING && string.IsNullOrEmpty(sa.AttendanceLog.CheckInPhotoKey)))
            .ToList();

        if (unattendedAssignments.Count == 0)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.AlreadyCheckedInShift);
        }

        // 6. So khớp ca làm việc dựa theo khung giờ
        var shiftTimeWindows = unattendedAssignments.Select(sa =>
        {
            var workDate = sa.Schedule.WorkDate;
            var startTime = sa.Schedule.EffectiveStartTime;
            var endTime = sa.Schedule.EffectiveEndTime;
            var isOvernight = sa.Schedule.EffectiveIsOvernight;
            var startDt = workDate.ToDateTime(startTime);
            var endDt = (isOvernight || endTime < startTime)
                ? workDate.AddDays(1).ToDateTime(endTime)
                : workDate.ToDateTime(endTime);

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
            var upcomingShift = shiftTimeWindows
                .Where(w => now < w.EarliestCheckIn)
                .OrderBy(w => w.StartDt)
                .FirstOrDefault();

            if (upcomingShift != null)
            {
                return ApiResponse<AttendanceRecordDto>.Fail(string.Format(
                    AttendanceMessages.TooEarlyForShift,
                    upcomingShift.Assignment.Schedule.EffectiveShiftName,
                    upcomingShift.StartDt.ToString("HH:mm"),
                    upcomingShift.EarliestCheckIn.ToString("HH:mm")));
            }

            var endedShift = shiftTimeWindows
                .Where(w => now > w.LatestCheckIn)
                .OrderByDescending(w => w.EndDt)
                .FirstOrDefault();

            if (endedShift != null)
            {
                return ApiResponse<AttendanceRecordDto>.Fail(string.Format(
                    AttendanceMessages.ShiftAlreadyEnded,
                    endedShift.Assignment.Schedule.EffectiveShiftName,
                    endedShift.EndDt.ToString("HH:mm")));
            }

            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NoActiveShiftInTimeframe);
        }

        var assignment = matchedShift.Assignment;

        // 7. Tạo hoặc tái sử dụng AttendanceLog với Status = PENDING
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
            ShiftName = assignment.Schedule.EffectiveShiftName,
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

    /// <inheritdoc />
    public async Task<ApiResponse<AttendanceRecordDto>> CheckOutAsync(KioskCheckOutDto request)
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

        if (kiosk.Branch == null || !string.Equals(kiosk.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                $"Chi nhánh '{kiosk.Branch?.Name ?? "của Kiosk"}' đã bị khóa. Không thể thực hiện chấm công.",
                new List<string> { "BRANCH_LOCKED" });
        }

        var otpResult = await _redisOtpService.VerifyAndConsumeAttendanceOtpAsync(request.OtpCode);
        if (otpResult == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.InvalidAttendanceOtp);
        }

        var (userId, otpType) = otpResult.Value;

        if (!string.Equals(otpType, "CHECK_OUT", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<AttendanceRecordDto>.Fail(
                string.Format(AttendanceMessages.OtpTypeMismatch, "CHECK_OUT"));
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Status == "ACTIVE");
        if (user == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.EmployeeNotFound);
        }

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
                          ((al.Assignment.Schedule.SnapshotIsOvernight ?? al.Assignment.Schedule.ShiftTemplate.IsOvernight) && al.Assignment.Schedule.WorkDate == yesterday)))
            .OrderByDescending(al => al.CheckInTime)
            .FirstOrDefaultAsync();

        if (log == null)
        {
            return ApiResponse<AttendanceRecordDto>.Fail(AttendanceMessages.NoOpenAttendanceLogForCheckOut);
        }

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

    /// <inheritdoc />
    public async Task<ApiResponse<UploadAttendancePhotoResponseDto>> UploadAttendancePhotoAsync(UploadAttendancePhotoDto request)
    {
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

        var log = await _context.AttendanceLogs.FindAsync(request.AttendanceId);
        if (log == null)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceRecordNotFound);
        }

        if (log.KioskId != kiosk.Id)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogNotOwnedByKiosk);
        }

        var photoType = request.PhotoType?.Trim().ToUpper();
        if (photoType != "CHECK_IN" && photoType != "CHECK_OUT")
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.InvalidPhotoType);
        }

        if (photoType == "CHECK_IN" && log.Status != AttendanceLogStatus.PENDING)
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogAlreadyCompleted);
        }
        if (photoType == "CHECK_OUT" && !string.IsNullOrEmpty(log.CheckOutPhotoKey))
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.AttendanceLogAlreadyCompleted);
        }

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return ApiResponse<UploadAttendancePhotoResponseDto>.Fail(AttendanceMessages.UploadAttendancePhotoV3Failed);
        }

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
                log.Status = AttendanceLogStatus.PRESENT;
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
            log.Status = AttendanceLogStatus.COMPLETED;

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

    /// <inheritdoc />
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
            if (!string.IsNullOrEmpty(log.CheckOutPhotoKey))
            {
                return ApiResponse<bool>.Fail(AttendanceMessages.CancelAttendanceFailed);
            }

            log.CheckOutTime = null;
            log.CheckOutStatus = null;
            log.Status = AttendanceLogStatus.PRESENT;
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

        // Kiểm tra mã xác thực OTP 60s trên Redis
        return await _redisOtpService.VerifyOtpAsync(user.Id, trimmedCode, consume: consumeOtp);
    }
}
