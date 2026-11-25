using System.ComponentModel.DataAnnotations;

namespace Modules.Shifts.DTOs;

public class StoreSwapRequestsQueryDto
{
    [RegularExpression("(?i)^(ALL|PENDING|PENDING_PEER|APPROVED|REJECTED|EXPIRED|CANCELLED)$")]
    public string Status { get; set; } = "ALL";

    [StringLength(200)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

public class StoreSwapRequestStatisticsDto
{
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ExpiredCount { get; set; }
}

public class StoreSwapRequestsPageDto
{
    public List<ShiftSwapRequestDto> Items { get; set; } = [];
    public int TotalItems { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    // Branch-wide counts are independent of the table's status/search filters.
    public StoreSwapRequestStatisticsDto Statistics { get; set; } = new();
}
