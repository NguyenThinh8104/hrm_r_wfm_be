namespace Modules.Attendance.DTOs;

public class StoreManagerAttendanceRecordDto
{
    public ulong AssignmentId { get; set; }
    public ulong? AttendanceLogId { get; set; }
    public ulong EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public DateOnly ShiftDate { get; set; }
    public string ScheduledStartTime { get; set; } = string.Empty;
    public string ScheduledEndTime { get; set; } = string.Empty;
    
    // Giờ thực tế điểm danh
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    
    // Trạng thái: ON_TIME, LATE, EARLY, EARLY_LEAVE, ABSENT, PENDING, COMPLETED...
    public string CheckInStatus { get; set; } = "PENDING";
    public string CheckOutStatus { get; set; } = "PENDING";
    public string OverallStatus { get; set; } = "PENDING";
    
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public double? ActualWorkHours { get; set; }
    
    // Cờ báo có ảnh hay không (Chỉ tải link ảnh khi người quản trị chủ động yêu cầu)
    public bool HasCheckInPhoto { get; set; }
    public bool HasCheckOutPhoto { get; set; }
}

public class StoreManagerAttendanceSummaryDto
{
    public int TotalShifts { get; set; }
    public int OnTimeCount { get; set; }
    public int LateCount { get; set; }
    public int EarlyLeaveCount { get; set; }
    public int AbsentCount { get; set; }
    public double TotalWorkHours { get; set; }
}

public class StoreManagerMonthlyAttendanceResponseDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public ulong BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public StoreManagerAttendanceSummaryDto Summary { get; set; } = new();
    public List<StoreManagerAttendanceRecordDto> Records { get; set; } = new();
}

public class AttendancePhotoUrlResponseDto
{
    public ulong AttendanceLogId { get; set; }
    public string PhotoType { get; set; } = "CHECKIN"; // "CHECKIN" hoặc "CHECKOUT"
    public string PhotoUrl { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 3;
}
