namespace Modules.Shifts.DTOs;

// ==========================================
// 1. Shift Template DTOs (UC 1.3)
// ==========================================

/// <summary>
/// DTO thông tin mẫu ca làm việc chuẩn đã được chuẩn hóa hệ thống (UC 1.3).
/// </summary>
public class ShiftTemplateDto
{
    public uint Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StartTime { get; set; } = "00:00:00";
    public string EndTime { get; set; } = "00:00:00";
    public uint BreakMinutes { get; set; }
    public bool IsOvernight { get; set; }
    public double WorkHours { get; set; }
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Aliases for backward compatibility
    public int ShiftId => (int)Id;
    public string ShiftCode => Code;
    public string TemplateCode => Code;
    public string ShiftName => Name;
    public uint BreakDurationMinutes => BreakMinutes;
    public bool IsActive => Status == "ACTIVE";
    public bool IsSystemDefault => new[] { "CA_SANG", "CA_CHIEU", "CA_DEM" }.Contains(Code.ToUpper());
}

public class ShiftDto : ShiftTemplateDto { }

/// <summary>
/// DTO tạo mẫu ca chuẩn mới (Operations Admin).
/// </summary>
public class CreateShiftTemplateDto
{
    private string _code = string.Empty;
    /// <summary>
    /// Mã mẫu ca viết hoa duy nhất (Ví dụ: CA_SANG, CA_CHIEU, CA_DEM).
    /// </summary>
    public string Code 
    { 
        get => !string.IsNullOrWhiteSpace(_code) ? _code : (!string.IsNullOrWhiteSpace(TemplateCode) ? TemplateCode : (ShiftCode ?? string.Empty));
        set => _code = value;
    }
    public string? TemplateCode { get; set; }
    public string? ShiftCode { get; set; }

    private string _name = string.Empty;
    /// <summary>
    /// Tên ca làm việc (Ví dụ: Ca Sáng, Ca Chiều, Ca Đêm).
    /// </summary>
    public string Name 
    { 
        get => !string.IsNullOrWhiteSpace(_name) ? _name : (ShiftName ?? string.Empty);
        set => _name = value;
    }
    public string? ShiftName { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Giờ bắt đầu ca (HH:mm:ss).
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Giờ kết thúc ca (HH:mm:ss).
    /// </summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Cờ đánh dấu ca làm việc xuyên đêm qua ngày hôm sau.
    /// </summary>
    public bool IsOvernight { get; set; } = false;

    private uint _breakMinutes;
    /// <summary>
    /// Thời gian nghỉ giữa ca (Số phút).
    /// </summary>
    public uint BreakMinutes 
    { 
        get => _breakMinutes > 0 ? _breakMinutes : (BreakDurationMinutes ?? 0);
        set => _breakMinutes = value;
    }
    public uint? BreakDurationMinutes { get; set; }

    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
}

/// <summary>
/// DTO cập nhật mẫu ca chuẩn (Operations Admin).
/// </summary>
public class UpdateShiftTemplateDto
{
    private string _name = string.Empty;
    public string Name 
    { 
        get => !string.IsNullOrWhiteSpace(_name) ? _name : (ShiftName ?? string.Empty);
        set => _name = value;
    }
    public string? ShiftName { get; set; }

    public string? Description { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
    
    private uint _breakMinutes;
    public uint BreakMinutes 
    { 
        get => _breakMinutes > 0 ? _breakMinutes : (BreakDurationMinutes ?? 0);
        set => _breakMinutes = value;
    }
    public uint? BreakDurationMinutes { get; set; }

    public string? Status { get; set; }
}

/// <summary>
/// DTO cập nhật trạng thái mẫu ca chuẩn (ACTIVE / INACTIVE).
/// </summary>
public class UpdateShiftTemplateStatusDto
{
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
    public string? Reason { get; set; }
}

/// <summary>
/// DTO yêu cầu khởi tạo khung mẫu lịch làm việc theo tháng cho cửa hàng.
/// </summary>
public class GenerateMonthlyScheduleDto
{
    /// <summary>
    /// Mã ID chi nhánh cửa hàng.
    /// </summary>
    public ulong BranchId { get; set; }

    /// <summary>
    /// Năm cần sinh lịch (Ví dụ: 2026).
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Tháng cần sinh lịch (1 - 12).
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Danh sách ID các mẫu ca chuẩn được áp dụng. Nếu null/rỗng sẽ dùng tất cả ca active.
    /// </summary>
    public List<uint>? TemplateIds { get; set; }

    /// <summary>
    /// Định mức nhu cầu số lượng Thu ngân mặc định cho từng ca.
    /// </summary>
    public byte DefaultRequiredCashier { get; set; } = 1;

    /// <summary>
    /// Định mức nhu cầu số lượng Nhân viên bán hàng mặc định cho từng ca.
    /// </summary>
    public byte DefaultRequiredSales { get; set; } = 2;

    /// <summary>
    /// Định mức nhu cầu số lượng Bảo vệ mặc định cho từng ca.
    /// </summary>
    public byte DefaultRequiredSecurity { get; set; } = 1;
}

/// <summary>
/// DTO điều chỉnh định mức nhu cầu nhân sự của ca trực (Store Manager).
/// </summary>
public class UpdateScheduleRequirementDto
{
    public ulong ScheduleId { get; set; }
    public byte RequiredCashier { get; set; }
    public byte RequiredSales { get; set; }
    public byte RequiredSecurity { get; set; }
}

/// <summary>
/// DTO chi tiết bản ghi phân công lịch ca làm việc.
/// </summary>
public class WorkScheduleDto
{
    public ulong ScheduleId { get; set; }
    public ulong BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public uint ShiftTemplateId { get; set; }
    public string ShiftTemplateName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly WorkDate { get; set; }
    public byte RequiredCashier { get; set; }
    public byte RequiredSales { get; set; }
    public byte RequiredSecurity { get; set; }
    public int AssignedCashierCount { get; set; }
    public int AssignedSalesCount { get; set; }
    public int AssignedSecurityCount { get; set; }
    public string Status { get; set; } = "DRAFT";
}

/// <summary>
/// DTO thông tin 1 mục gán ca nhân viên.
/// </summary>
public class SingleAssignmentItemDto
{
    public ulong UserId { get; set; }
    public uint ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
}

/// <summary>
/// DTO phân bổ hàng loạt nhân viên vào ca trực (Store Manager).
/// </summary>
public class BatchAssignShiftDto
{
    public ulong BranchId { get; set; }
    public List<SingleAssignmentItemDto> Assignments { get; set; } = new List<SingleAssignmentItemDto>();
}

/// <summary>
/// DTO ô lịch phân công nhân viên trong ma trận lịch tháng.
/// </summary>
public class EmployeeRosterCellDto
{
    public DateOnly Date { get; set; }
    public uint ShiftTemplateId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ulong AssignmentId { get; set; }
    public string Status { get; set; } = "CONFIRMED";
}

/// <summary>
/// DTO thông tin lịch làm việc cả tháng của 1 nhân viên.
/// </summary>
public class EmployeeMonthlyRosterDto
{
    public ulong UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public List<EmployeeRosterCellDto> AssignedShifts { get; set; } = new List<EmployeeRosterCellDto>();
}

/// <summary>
/// DTO ma trận bảng phân bổ ca tuần/tháng cho Store Manager Dashboard.
/// </summary>
public class MonthlyScheduleMatrixDto
{
    public ulong BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalDaysInMonth { get; set; }
    public List<WorkScheduleDto> Schedules { get; set; } = new List<WorkScheduleDto>();
    public List<EmployeeMonthlyRosterDto> EmployeeRosters { get; set; } = new List<EmployeeMonthlyRosterDto>();
}

public class ShiftAssignmentDto
{
    public int AssignmentId { get; set; }
    public int ScheduleId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly WorkDate { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDispatched { get; set; }
}

public class CreateShiftAssignmentDto
{
    public int StoreId { get; set; }
    public int? ScheduleId { get; set; }
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateOnly WorkDate { get; set; }
}

public class ShiftSwapRequestDto
{
    public int SwapRequestId { get; set; }
    public int AssignmentId { get; set; }
    public int RequesterEmployeeId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public int TargetEmployeeId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateSwapRequestDto
{
    public int AssignmentId { get; set; }
    public int TargetEmployeeId { get; set; }
    public string? Reason { get; set; }
}

public class ReviewSwapRequestDto
{
    public int SwapRequestId { get; set; }
    public bool IsApproved { get; set; }
    public string? Remarks { get; set; }
}

