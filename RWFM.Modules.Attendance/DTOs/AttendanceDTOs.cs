namespace RWFM.Modules.Attendance.DTOs;

public class KioskEmployeeRosterDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public int AssignmentId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool HasCheckedIn { get; set; }
    public bool HasCheckedOut { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public bool IsDispatched { get; set; }
}

public class KioskPinCheckInDto
{
    public int EmployeeId { get; set; }
    public string PinCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public decimal? OpeningFloatCash { get; set; }
}

public class KioskPinCheckOutDto
{
    public int EmployeeId { get; set; }
    public string PinCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
}

public class ReportAttendanceFraudDto
{
    public int AttendanceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = "Fraud";
}

public class AttendanceRecordDto
{
    public int AttendanceId { get; set; }
    public int AssignmentId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInMethod { get; set; }
    public string? CheckOutMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasException { get; set; }
    public string? ExceptionReason { get; set; }
    public string? ReportedByName { get; set; }
}
