using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Attendance.Services;

/// <summary>
/// Triển khai dịch vụ tự phục vụ điểm danh của nhân viên (Lịch làm việc tuần & Lịch sử chấm công tháng).
/// </summary>
public class AttendanceSelfService : IAttendanceSelfService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AttendanceSelfService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
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
                    ShiftName = a.Schedule.EffectiveShiftName,
                    TemplateCode = a.Schedule.ShiftTemplate?.TemplateCode ?? string.Empty,
                    StartTime = a.Schedule.EffectiveStartTime,
                    EndTime = a.Schedule.EffectiveEndTime,
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

    /// <inheritdoc />
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
                var shiftStartDt = a.Schedule != null ? date.ToDateTime(a.Schedule.EffectiveStartTime) : a.AttendanceLog.CheckInTime;
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
                ShiftName = a.Schedule?.EffectiveShiftName ?? string.Empty,
                ShiftStart = a.Schedule?.EffectiveStartTime ?? default,
                ShiftEnd = a.Schedule?.EffectiveEndTime ?? default,
                BranchName = a.Schedule?.Branch?.Name ?? string.Empty,
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
