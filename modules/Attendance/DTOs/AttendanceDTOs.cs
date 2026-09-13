namespace Modules.Attendance.DTOs;

/// <summary>
/// DTO yêu cầu điểm danh Check-in đầu ca tại trạm Kiosk.
/// </summary>
public class CheckInRequestDto
{
    /// <summary>
    /// Mã ID tài khoản nhân viên (users.id).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Mã PIN cá nhân 6 chữ số.
    /// </summary>
    public string Pin { get; set; } = string.Empty;
}

/// <summary>
/// DTO yêu cầu điểm danh Check-out kết thúc ca tại trạm Kiosk.
/// </summary>
public class CheckOutRequestDto
{
    /// <summary>
    /// Mã ID tài khoản nhân viên (users.id).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Mã PIN cá nhân 6 chữ số.
    /// </summary>
    public string Pin { get; set; } = string.Empty;
}

public class ValidatePinRequestDto
{
    public int StoreId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
}

public class ValidatePinResponseDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public int PrimaryStoreId { get; set; }
    public bool IsValid { get; set; }
    public bool HasShiftToday { get; set; }
    public int? AssignmentId { get; set; }
    public string? ShiftName { get; set; }
    public bool HasCheckedIn { get; set; }
    public bool HasCheckedOut { get; set; }
}

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
    public int? KioskId { get; set; }
}

public class KioskPinCheckOutDto
{
    public int EmployeeId { get; set; }
    public string PinCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int? KioskId { get; set; }
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
    public int? KioskId { get; set; }
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

/// <summary>
/// DTO chứa thông tin chi tiết bản ghi điểm danh đầu ca (Check-in) thành công.
/// </summary>
public class AttendanceLogDto
{
    /// <summary>
    /// Mã ID bản ghi điểm danh (attendance_log_id).
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Mã ID phân công lịch làm việc (shift_assignment_id).
    /// </summary>
    public ulong AssignmentId { get; set; }

    /// <summary>
    /// Mã ID nhân viên điểm danh (user_id).
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Mã nhân viên (EmployeeCode).
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Họ và tên nhân viên.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Mã ID chi nhánh cửa hàng thực hiện điểm danh.
    /// </summary>
    public ulong BranchId { get; set; }

    /// <summary>
    /// Mã ID thiết bị Kiosk thực hiện điểm danh.
    /// </summary>
    public ulong? KioskId { get; set; }

    /// <summary>
    /// Thời gian điểm danh vào ca (Server local timestamp).
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// Số tiền lẻ bàn giao đầu ca (Chỉ dành cho Thu ngân).
    /// </summary>
    public decimal? OpeningFloatCash { get; set; }

    /// <summary>
    /// Cờ đánh dấu nhân viên đi muộn so với giờ bắt đầu ca.
    /// </summary>
    public bool IsLate { get; set; }

    /// <summary>
    /// Trạng thái điểm danh (Present / Late).
    /// </summary>
    public string Status { get; set; } = "Present";
}

/// <summary>
/// DTO chứa thông tin chi tiết kết quả điểm danh kết thúc ca (Check-out).
/// </summary>
public class AttendanceCheckOutResultDto
{
    /// <summary>
    /// Mã ID bản ghi điểm danh.
    /// </summary>
    public ulong AttendanceLogId { get; set; }

    /// <summary>
    /// Mã ID phân công lịch làm việc.
    /// </summary>
    public ulong AssignmentId { get; set; }

    /// <summary>
    /// Mã ID nhân viên.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Mã nhân viên.
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Họ và tên nhân viên.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Tên ca làm việc (Ví dụ: Ca Sáng, Ca Tối).
    /// </summary>
    public string ShiftName { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm bắt đầu ca (Check-in time).
    /// </summary>
    public DateTime CheckInTime { get; set; }

    /// <summary>
    /// Thời điểm kết thúc ca (Check-out time).
    /// </summary>
    public DateTime CheckOutTime { get; set; }

    /// <summary>
    /// Tổng số phút làm việc thực tế trong ca.
    /// </summary>
    public double ActualWorkMinutes { get; set; }

    /// <summary>
    /// Thông điệp kết quả check-out thành công.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}


