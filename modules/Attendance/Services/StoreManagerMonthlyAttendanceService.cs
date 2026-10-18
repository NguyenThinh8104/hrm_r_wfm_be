using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Services;

namespace Modules.Attendance.Services;

public class StoreManagerMonthlyAttendanceService : IStoreManagerMonthlyAttendanceService
{
    private readonly AppDbContext _context;
    private readonly IS3StorageService _storageService;

    public StoreManagerMonthlyAttendanceService(AppDbContext context, IS3StorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<ApiResponse<StoreManagerMonthlyAttendanceResponseDto>> GetMonthlyAttendanceAsync(
        ulong branchId, int month, int year, ulong? employeeId = null)
    {
        if (month < 1 || month > 12)
            return ApiResponse<StoreManagerMonthlyAttendanceResponseDto>.Fail("Tháng không hợp lệ (1 - 12).");
        if (year < 2020 || year > 2100)
            return ApiResponse<StoreManagerMonthlyAttendanceResponseDto>.Fail("Năm không hợp lệ.");

        var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch == null)
            return ApiResponse<StoreManagerMonthlyAttendanceResponseDto>.Fail("Không tìm thấy thông tin chi nhánh.");

        var startWorkDate = new DateOnly(year, month, 1);
        var endWorkDate = startWorkDate.AddMonths(1).AddDays(-1);
        var nowUtc = DateTime.UtcNow;

        // Query lịch phân ca (ShiftAssignment) LEFT JOIN với AttendanceLog
        var query = _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.User)
            .Include(sa => sa.AttendanceLog)
            .Where(sa => sa.Schedule.BranchId == branchId
                      && sa.Schedule.WorkDate >= startWorkDate
                      && sa.Schedule.WorkDate <= endWorkDate);

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(sa => sa.UserId == employeeId.Value);
        }

        var assignments = await query.AsNoTracking().ToListAsync();
        var records = new List<StoreManagerAttendanceRecordDto>();

        foreach (var sa in assignments)
        {
            var schedule = sa.Schedule;
            var workDate = schedule.WorkDate;
            var shiftTemplate = schedule.ShiftTemplate;

            var shiftName = schedule.EffectiveShiftName;
            var startTime = schedule.EffectiveStartTime;
            var endTime = schedule.EffectiveEndTime;

            // Tính thời gian kết thúc ca dưới dạng DateTime UTC/Local để đánh giá trạng thái vắng mặt
            var shiftEndDateTime = workDate.ToDateTime(endTime);

            var log = sa.AttendanceLog;
            var record = new StoreManagerAttendanceRecordDto
            {
                AssignmentId = sa.Id,
                AttendanceLogId = log?.Id,
                EmployeeId = sa.UserId,
                EmployeeCode = sa.User?.EmployeeCode ?? $"EMP{sa.UserId}",
                FullName = sa.User?.FullName ?? "N/A",
                ShiftName = shiftName,
                ShiftDate = workDate,
                ScheduledStartTime = startTime.ToString("HH:mm"),
                ScheduledEndTime = endTime.ToString("HH:mm"),
                CheckInTime = log?.CheckInTime,
                CheckOutTime = log?.CheckOutTime,
                LateMinutes = log?.LateMinutes ?? 0,
                EarlyLeaveMinutes = log?.EarlyLeaveMinutes ?? 0,
                ActualWorkHours = log?.ActualWorkMinutes.HasValue == true
                    ? Math.Round(log.ActualWorkMinutes.Value / 60.0, 2)
                    : null,
                HasCheckInPhoto = !string.IsNullOrWhiteSpace(log?.CheckInPhotoKey),
                HasCheckOutPhoto = !string.IsNullOrWhiteSpace(log?.CheckOutPhotoKey)
            };

            // ══════════════════════════════════════════════════════════════════
            // QUY TẮC SUY DIỄN TRẠNG THÁI (BUSINESS LOGIC)
            // ══════════════════════════════════════════════════════════════════
            if (log == null)
            {
                // Nếu ca làm việc đã trôi qua trong quá khứ mà không có Log -> Vắng mặt/Nghỉ (ABSENT)
                if (DateTime.Now > shiftEndDateTime)
                {
                    record.CheckInStatus = "ABSENT";
                    record.CheckOutStatus = "ABSENT";
                    record.OverallStatus = "ABSENT";
                }
                else
                {
                    record.CheckInStatus = "PENDING";
                    record.CheckOutStatus = "PENDING";
                    record.OverallStatus = "PENDING";
                }
            }
            else
            {
                record.CheckInStatus = log.CheckInStatus.ToString();
                record.CheckOutStatus = log.CheckOutStatus?.ToString() ?? "NOT_CHECKED_OUT";

                if (log.CheckInStatus == CheckInStatus.LATE || log.CheckOutStatus == CheckOutStatus.EARLY_LEAVE)
                {
                    record.OverallStatus = "VIOLATION";
                }
                else if (log.CheckInStatus == CheckInStatus.ON_TIME && log.CheckOutStatus == CheckOutStatus.ON_TIME)
                {
                    record.OverallStatus = "ON_TIME";
                }
                else if (log.CheckInStatus == CheckInStatus.EARLY)
                {
                    record.OverallStatus = "EARLY";
                }
                else
                {
                    record.OverallStatus = log.Status.ToString();
                }
            }

            records.Add(record);
        }

        // Sắp xếp theo ngày giảm dần, giờ bắt đầu tăng dần
        var sortedRecords = records
            .OrderByDescending(r => r.ShiftDate)
            .ThenBy(r => r.ScheduledStartTime)
            .ToList();

        var summary = new StoreManagerAttendanceSummaryDto
        {
            TotalShifts = sortedRecords.Count,
            OnTimeCount = sortedRecords.Count(r => r.CheckInStatus == "ON_TIME"),
            LateCount = sortedRecords.Count(r => r.CheckInStatus == "LATE"),
            EarlyLeaveCount = sortedRecords.Count(r => r.CheckOutStatus == "EARLY_LEAVE"),
            AbsentCount = sortedRecords.Count(r => r.OverallStatus == "ABSENT"),
            TotalWorkHours = Math.Round(sortedRecords.Sum(r => r.ActualWorkHours ?? 0), 2)
        };

        var response = new StoreManagerMonthlyAttendanceResponseDto
        {
            Month = month,
            Year = year,
            BranchId = branchId,
            BranchName = branch.Name,
            Summary = summary,
            Records = sortedRecords
        };

        return ApiResponse<StoreManagerMonthlyAttendanceResponseDto>.Ok(response, "Lấy dữ liệu điểm danh tháng thành công.");
    }

    public async Task<ApiResponse<AttendancePhotoUrlResponseDto>> GetAttendancePhotoUrlAsync(
        ulong branchId, ulong attendanceLogId, string photoType)
    {
        var log = await _context.AttendanceLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == attendanceLogId && l.BranchId == branchId);

        if (log == null)
        {
            return ApiResponse<AttendancePhotoUrlResponseDto>.Fail("Không tìm thấy bản ghi điểm danh thuộc quyền quản lý của chi nhánh.");
        }

        bool isCheckout = photoType.Equals("checkout", StringComparison.OrdinalIgnoreCase);
        string? photoKey = isCheckout ? log.CheckOutPhotoKey : log.CheckInPhotoKey;

        if (string.IsNullOrWhiteSpace(photoKey))
        {
            return ApiResponse<AttendancePhotoUrlResponseDto>.Fail($"Bản ghi điểm danh này không có ảnh {(isCheckout ? "check-out" : "check-in")}.");
        }

        // Sinh link Presigned URL xem ảnh có hiệu lực ngắn (3 phút)
        var presignedUrl = _storageService.GetPresignedViewUrl(photoKey, "image/jpeg", 3);

        if (string.IsNullOrWhiteSpace(presignedUrl))
        {
            return ApiResponse<AttendancePhotoUrlResponseDto>.Fail("Không thể khởi tạo đường dẫn xem ảnh bảo mật.");
        }

        var result = new AttendancePhotoUrlResponseDto
        {
            AttendanceLogId = attendanceLogId,
            PhotoType = isCheckout ? "CHECKOUT" : "CHECKIN",
            PhotoUrl = presignedUrl,
            ExpiresInMinutes = 3
        };

        return ApiResponse<AttendancePhotoUrlResponseDto>.Ok(result, "Sinh liên kết xem ảnh đối soát thành công.");
    }
}
