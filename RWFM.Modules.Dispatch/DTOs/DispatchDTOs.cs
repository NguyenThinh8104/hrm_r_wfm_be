namespace RWFM.Modules.Dispatch.DTOs;

public class CreateDispatchRequestDto
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
    public string? ApprovalNotes { get; set; }
}

public class DispatchRecordDto
{
    public int DispatchId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
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
