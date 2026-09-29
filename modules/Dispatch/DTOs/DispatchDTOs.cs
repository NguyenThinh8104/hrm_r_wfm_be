namespace Modules.Dispatch.DTOs;

public class CreateDispatchRequestDto
{
    public List<int> EmployeeIds { get; set; } = new();
    public int? EmployeeId { get; set; } // Backward compatibility
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}

public class UpdateDispatchRequestDto
{
    public List<int> EmployeeIds { get; set; } = new();
    public int? EmployeeId { get; set; }
    public int FromStoreId { get; set; }
    public int ToStoreId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}

public class EmployeeReviewItem
{
    public int EmployeeId { get; set; }
    public bool IsApproved { get; set; }
    public string? Note { get; set; }
}

public class ReviewDispatchRequestDto
{
    public int DispatchId { get; set; }
    public List<EmployeeReviewItem>? EmployeeReviews { get; set; }
    public bool? IsApproved { get; set; } // Backward compatibility: duyệt/từ chối toàn bộ phiếu
    public int? AssignedEmployeeId { get; set; } // Backward compatibility
    public string? ApprovalNotes { get; set; }
}

public class DispatchEmployeeDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public string? Note { get; set; }
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
    public List<DispatchEmployeeDto> Employees { get; set; } = new();
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

public class DispatchEmployeeOptionDto
{
    public ulong Id { get; set; }
    public ulong EmployeeId => Id;
    public ulong UserId => Id;
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public ulong HomeBranchId { get; set; }
}
