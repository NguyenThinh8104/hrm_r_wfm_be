namespace Modules.Attendance.DTOs;

public class RequestAttendanceOtpDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = "CHECK_IN"; // "CHECK_IN" or "CHECK_OUT"
}

public class RequestAttendanceOtpResponseDto
{
    public string OtpCode { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; } = 60;
    public double DistanceMeters { get; set; }
    public string BranchName { get; set; } = string.Empty;
}

public class LiveRosterDto
{
    public ulong? AttendanceId { get; set; }
    public ulong AssignmentId { get; set; }
    public ulong UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInPhotoPresignedUrl { get; set; }
    public string? CheckOutPhotoPresignedUrl { get; set; }
    public bool IsFraudFlagged { get; set; }
    public string? FraudReason { get; set; }
    public string? ReportedByName { get; set; }
    public bool IsLate { get; set; }
    public string Status { get; set; } = "ABSENT";
}


public class ResolveFraudDto
{
    public ulong AttendanceId { get; set; }
    public bool IsApproved { get; set; }
}

public class KioskCheckInDto
{
    public string KioskDeviceToken { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}

public class KioskCheckOutDto
{
    public string KioskDeviceToken { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}

/// <summary>
/// DTO upload ảnh chấm công sau khi đã tạo attendance record (bắt buộc).
/// </summary>
public class UploadAttendancePhotoDto
{
    public string KioskDeviceToken { get; set; } = string.Empty;
    public ulong AttendanceId { get; set; }
    public string ImageBase64 { get; set; } = string.Empty;
    /// <summary>
    /// Loại ảnh: "CHECK_IN" hoặc "CHECK_OUT"
    /// </summary>
    public string PhotoType { get; set; } = "CHECK_IN";
}

/// <summary>
/// Response sau khi upload ảnh thành công (status chuyển COMPLETED).
/// </summary>
public class UploadAttendancePhotoResponseDto
{
    public ulong AttendanceId { get; set; }
    public string PhotoKey { get; set; } = string.Empty;
    public string? PresignedUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLate { get; set; }
    public string CheckInStatus { get; set; } = string.Empty;
    public string? CheckOutStatus { get; set; }
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    public double? ActualWorkMinutes { get; set; }
    public string DetailedMessage { get; set; } = string.Empty;
}

public class CancelKioskAttendanceDto
{
    public string KioskDeviceToken { get; set; } = string.Empty;
    public ulong AttendanceId { get; set; }
    public string ActionType { get; set; } = "CHECK_IN"; // "CHECK_IN" | "CHECK_OUT"
}

// Aliases cho các chỗ tham chiếu cũ nếu còn
public class KioskCheckInV3Dto : KioskCheckInDto {}
public class KioskCheckOutV3Dto : KioskCheckOutDto {}
public class UploadAttendancePhotoV3Dto : UploadAttendancePhotoDto {}
public class UploadAttendancePhotoV3ResponseDto : UploadAttendancePhotoResponseDto {}

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



public class UploadPhotoRequestDto
{
    public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
    public string? Folder { get; set; }
}

public class UploadPhotoResponseDto
{
    public string PhotoKey { get; set; } = string.Empty;
    public string? PresignedUrl { get; set; }
}

public class PresignedUrlResponseDto
{
    public string PhotoKey { get; set; } = string.Empty;
    public string? PresignedUrl { get; set; }
    public DateTime ExpiresAt { get; set; }
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
    /// <summary>
    /// Trạng thái workflow: PENDING (chờ ảnh) hoặc COMPLETED (hoàn tất)
    /// </summary>
    public string AttendanceLogStatus { get; set; } = string.Empty;
    public bool HasException { get; set; }
    public string? ExceptionReason { get; set; }
    public string? ReportedByName { get; set; }
    public string? ShiftName { get; set; }
    public bool IsLate { get; set; }
    public string CheckInStatus { get; set; } = string.Empty;
    public string? CheckOutStatus { get; set; }
    public int? LateMinutes { get; set; }
    public int? EarlyLeaveMinutes { get; set; }
    public double? ActualWorkMinutes { get; set; }
}




public class KioskEmployeeSearchDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
}


