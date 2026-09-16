namespace Modules.Dispatch.DTOs;

public class CreateDispatchRequestDto
{
    public int EmployeeId { get; set; }
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}

public class UpdateDispatchRequestDto
{
    public int EmployeeId { get; set; }
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}

public class ReviewDispatchRequestDto
{
    public int DispatchId { get; set; }
    public bool IsApproved { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? ApprovalNotes { get; set; }
}

public class DispatchRecordDto
{
    public int DispatchId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public int FromStoreId { get; set; }
    public string FromStoreName { get; set; } = string.Empty;
    public int ToStoreId { get; set; }
    public string ToStoreName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DispatchNetworkMetricsDto
{
    public int TotalDispatches { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ActiveTodayCount { get; set; }
    public double TotalDispatchedHours { get; set; }
    public List<StorePairDispatchMatrixDto> StorePairMatrix { get; set; } = new();
    public List<DispatchRecordDto> RecentDispatches { get; set; } = new();
}

public class StorePairDispatchMatrixDto
{
    public int FromStoreId { get; set; }
    public string FromStoreName { get; set; } = string.Empty;
    public int ToStoreId { get; set; }
    public string ToStoreName { get; set; } = string.Empty;
    public int DispatchCount { get; set; }
    public double TotalHours { get; set; }
}
