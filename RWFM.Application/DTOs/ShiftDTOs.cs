namespace RWFM.Application.DTOs;

public class ShiftDto
{
    public int ShiftId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; }
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
