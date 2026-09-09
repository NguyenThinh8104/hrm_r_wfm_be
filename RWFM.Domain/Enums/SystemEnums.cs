namespace RWFM.Domain.Enums;

public enum UserRole
{
    ChainAdmin = 1,
    StoreManager = 2,
    ShiftLeader = 3,
    Cashier = 4,
    SalesStaff = 5,
    SecurityGuard = 6
}

public enum ShiftType
{
    Morning = 1,     // 06:00 - 14:00
    Afternoon = 2,   // 14:00 - 22:00
    Night = 3,       // 22:00 - 06:00
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
