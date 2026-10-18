using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Services;

namespace Modules.Attendance.Services;

/// <summary>
/// Triển khai dịch vụ giám sát quân số thời gian thực và lịch sử điểm danh cửa hàng.
/// </summary>
public class AttendanceAuditService : IAttendanceAuditService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IS3StorageService _s3StorageService;

    public AttendanceAuditService(
        AppDbContext context,
        TimeProvider timeProvider,
        IS3StorageService s3StorageService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _s3StorageService = s3StorageService;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.AttendanceLog)
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
                status = att.Status.ToString();
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
                ShiftName = sa.Schedule.EffectiveShiftName,
                StartTime = sa.Schedule.EffectiveStartTime,
                EndTime = sa.Schedule.EffectiveEndTime,
                CheckInTime = att?.CheckInTime,
                CheckOutTime = att?.CheckOutTime,
                CheckInPhotoPresignedUrl = checkInPhotoUrl,
                CheckOutPhotoPresignedUrl = checkOutPhotoUrl,
                IsLate = isLate,
                Status = status,
                CheckInStatus = att?.CheckInStatus.ToString(),
                CheckOutStatus = att?.CheckOutStatus?.ToString()
            };
        }).ToList();

        return ApiResponse<List<LiveRosterDto>>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date)
    {
        var records = await _context.AttendanceLogs
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.User)
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
            .Include(al => al.Branch)
            .Where(al => al.BranchId == (ulong)storeId && DateOnly.FromDateTime(al.CreatedAt) == date)
            .OrderByDescending(al => al.CreatedAt)
            .ToListAsync();

        var result = records.Select(al =>
        {
            var shiftTemplate = al.Assignment?.Schedule?.ShiftTemplate;
            bool isLate = al.IsLate;

            string status = al.CheckOutTime.HasValue
                ? (isLate ? "Completed (Late)" : "Completed")
                : (isLate ? "Late" : "Present");

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
                HasException = isLate,
                ExceptionReason = isLate ? "Đi muộn" : null,
                ReportedByName = null
            };
        }).ToList();

        return ApiResponse<List<AttendanceRecordDto>>.Ok(result);
    }
}
