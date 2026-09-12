namespace Domain.Enums;

public enum UserRole
{
    BusinessOwner = 1,     // Chủ doanh nghiệp chuỗi (Dashboard, Audit Log)
    OperationsAdmin = 2,   // Quản trị vận hành toàn chuỗi (Master Data, Khung ca, Tài khoản)
    StoreManager = 3,      // Cửa hàng trưởng (Lập lịch, Phê duyệt đổi ca, Thỏa thuận mượn người, Chốt ca)
    ShiftLeader = 4,       // Trưởng ca (Giám sát ca, Ký chốt biên bản giao ca, Báo vắng mặt)
    Cashier = 5,           // Thu ngân (Kiosk check-in/out, Bàn giao két tiền mặt)
    SalesStaff = 6,        // Nhân viên bán hàng (Kiosk check-in/out, Đăng ký/Đổi ca)
    SecurityGuard = 7      // Nhân viên bảo vệ (Kiosk check-in/out, Bàn giao an ninh, khóa kho)
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

