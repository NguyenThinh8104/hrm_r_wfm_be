using System;
using System.Collections.Generic;

namespace Modules.Attendance.DTOs;

// --- API 1: Lịch làm việc tuần (Calendar View) ---
public class MyWeeklyScheduleDto
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public List<MyDayScheduleDto> Days { get; set; } = new();
}

public class MyDayScheduleDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public List<MyShiftSlotDto> Shifts { get; set; } = new();
}

public class MyShiftSlotDto
{
    public ulong AssignmentId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchAddress { get; set; } = string.Empty;
    public ulong BranchId { get; set; }
    public bool IsDispatched { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string AttendanceStatus { get; set; } = "NOT_YET"; // NOT_YET | CHECKED_IN | COMPLETED | ABSENT | CANCELLED
    public string AssignmentStatus { get; set; } = "CONFIRMED"; // CONFIRMED | CANCELLED
}

// --- API 2: Lịch sử chấm công tháng (Attendance History) ---
public class MyAttendanceHistoryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalAssignedShifts { get; set; }
    public int TotalWorkedShifts { get; set; }
    public int TotalAbsentShifts { get; set; }
    public double TotalWorkHours { get; set; }
    public double AbsentPercentage { get; set; }
    public double AttendanceRate { get; set; }
    public List<MyAttendanceDayDto> Details { get; set; } = new();
}

public class MyAttendanceDayDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public double? ActualWorkMinutes { get; set; }
    public string Status { get; set; } = "ABSENT"; // PRESENT | LATE | ABSENT | INCOMPLETE | NOT_YET
    public bool IsLate { get; set; }
    public bool IsDispatched { get; set; }
}
