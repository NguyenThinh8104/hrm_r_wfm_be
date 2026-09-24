namespace Domain.Enums;

public enum UserRole
{
    BUSINESS_OWNER = 1,     // Chủ doanh nghiệp chuỗi (Dashboard, Audit Log)
    OPERATIONS_ADMIN = 2,   // Quản trị vận hành toàn chuỗi (Master Data, Khung ca, Tài khoản)
    STORE_MANAGER = 3,      // Cửa hàng trưởng (Lập lịch, Phê duyệt đổi ca, Thỏa thuận mượn người, Chốt ca)
    SHIFT_LEADER = 4,       // Trưởng ca (Giám sát ca, Ký chốt biên bản giao ca, Báo vắng mặt)
    CASHIER = 5,           // Thu ngân (Kiosk check-in/out, Bàn giao két tiền mặt)
    SALES_STAFF = 6,        // Nhân viên bán hàng (Kiosk check-in/out, Đăng ký/Đổi ca)
    SECURITY_GUARD = 7      // Nhân viên bảo vệ (Kiosk check-in/out, Bàn giao an ninh, khóa kho)
}


public enum ShiftType
{
    Morning = 1,     // Ca sáng: 06:00 - 14:00 (hoặc 08:00 - 14:00)
    Afternoon = 2,   // Ca chiều: 14:00 - 22:00
    Night = 3,       // Ca đêm/Qua đêm: 22:00 - 06:00
    PartTime = 4     // Khung giờ linh hoạt
}

public enum ShiftAssignmentStatus
{
    Scheduled = 1,
    CheckedIn = 2,
    InProgress = 3,
    Completed = 4,
    Absent = 5,
    Swapped = 6
}

public enum AttendanceType
{
    CheckIn = 1,
    CheckOut = 2
}

public enum AttendanceStatus
{
    Valid = 1,
    Late = 2,
    EarlyDeparture = 3,
    FlaggedFraud = 4,
    CancelledByLeader = 5
}

public enum DispatchStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Active = 4,
    Completed = 5,
    Revoked = 6
}

public enum HandoverStatus
{
    Draft = 1,
    Submitted = 2,
    ApprovedByLeader = 3,
    DiscrepancyReported = 4
}

public enum SwapRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// Trạng thái hợp nhất của bản ghi chấm công (kết hợp cả workflow và chuyên cần).
/// Lưu dưới dạng TINYINT UNSIGNED (1 byte) trong database.
/// </summary>
public enum AttendanceLogStatus : byte
{
    PENDING = 1,          // Đã tạo record (OTP xác thực thành công), chờ upload ảnh
    PRESENT = 2,          // Upload ảnh thành công, vào ca đúng giờ (trong 5p ân hạn)
    LATE = 3,             // Upload ảnh thành công, vào ca muộn (> 5p ân hạn)
    COMPLETED = 4,        // Đã hoàn tất check-out ra ca (vào ca đúng giờ)
    COMPLETED_LATE = 5    // Đã hoàn tất check-out ra ca (vào ca đi muộn)
}

/// <summary>
/// Trạng thái chuyên cần chi tiết lúc Check-in.
/// Lưu dưới dạng TINYINT UNSIGNED (1 byte).
/// </summary>
public enum CheckInStatus : byte
{
    PENDING = 1,          // Vừa xác thực OTP, chờ chụp ảnh xác thực
    ON_TIME = 2,          // Vào ca đúng giờ (trong 5p ân hạn)
    LATE = 3,             // Đi muộn (> 5p ân hạn)
    EARLY = 4             // Đến sớm (trước giờ bắt đầu ca)
}

/// <summary>
/// Trạng thái chuyên cần chi tiết lúc Check-out.
/// Lưu dưới dạng TINYINT UNSIGNED (1 byte).
/// </summary>
public enum CheckOutStatus : byte
{
    PENDING = 1,          // Vừa xác thực OTP ra ca, chờ chụp ảnh xác thực
    ON_TIME = 2,          // Ra ca đúng giờ
    EARLY_LEAVE = 3,      // Về sớm (> 5p trước giờ kết thúc ca)
    LATE_LEAVE = 4        // Ra ca muộn / Tăng ca (> 15p sau giờ kết thúc ca)
}


