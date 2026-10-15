using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;

namespace Modules.Dispatch.Services;

/// <summary>
/// Facade Service chuyển tiếp các yêu cầu điều động nhân sự đến các service chuyên biệt:
/// - <see cref="IDispatchWorkflowService"/>: Quy trình tạo, sửa, duyệt, thu hồi, hủy đơn điều động.
/// - <see cref="IDispatchQuotaService"/>: Tính toán hạn mức điều động an toàn (40%) và lọc nhân sự chi viện.
/// - <see cref="IDispatchMetricsService"/>: Thống kê ma trận cặp chi nhánh và tổng giờ chi viện.
/// </summary>
public class DispatchService : IDispatchService
{
    private readonly IDispatchWorkflowService _workflowService;
    private readonly IDispatchQuotaService _quotaService;
    private readonly IDispatchMetricsService _metricsService;

    public DispatchService(
        IDispatchWorkflowService workflowService,
        IDispatchQuotaService quotaService,
        IDispatchMetricsService metricsService)
    {
        _workflowService = workflowService;
        _quotaService = quotaService;
        _metricsService = metricsService;
    }

    /// <inheritdoc />
    public Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request)
        => _workflowService.CreateDispatchRequestAsync(requesterEmployeeId, request);

    /// <inheritdoc />
    public Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request)
        => _workflowService.ReviewDispatchRequestAsync(approverEmployeeId, request);

    /// <inheritdoc />
    public Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId)
        => _workflowService.GetDispatchesByStoreAsync(storeId);

    /// <inheritdoc />
    public Task<ApiResponse<List<DispatchRecordDto>>> GetAllDispatchesAsync(string? status, int? storeId)
        => _workflowService.GetAllDispatchesAsync(status, storeId);

    /// <inheritdoc />
    public Task<ApiResponse<DispatchNetworkMetricsDto>> GetNetworkMetricsAsync(DateOnly? fromDate, DateOnly? toDate)
        => _metricsService.GetNetworkMetricsAsync(fromDate, toDate);

    /// <inheritdoc />
    public Task<ApiResponse<DispatchRecordDto>> UpdateDispatchRequestAsync(int managerId, int dispatchId, UpdateDispatchRequestDto request)
        => _workflowService.UpdateDispatchRequestAsync(managerId, dispatchId, request);

    /// <inheritdoc />
    public Task<ApiResponse<DispatchRecordDto>> RecallDispatchRequestAsync(int managerId, int dispatchId, string? reason = null)
        => _workflowService.RecallDispatchRequestAsync(managerId, dispatchId, reason);

    /// <inheritdoc />
    public Task<ApiResponse<bool>> DeleteDispatchRequestAsync(int managerId, int dispatchId)
        => _workflowService.DeleteDispatchRequestAsync(managerId, dispatchId);

    /// <inheritdoc />
    public Task<ApiResponse<List<DispatchEmployeeOptionDto>>> GetBranchEmployeesForDispatchAsync(ulong branchId)
        => _quotaService.GetBranchEmployeesForDispatchAsync(branchId);

    /// <inheritdoc />
    public Task<ApiResponse<BranchDispatchQuotaStatusDto>> GetBranchDispatchQuotaAsync(ulong branchId, DateOnly? startDate, DateOnly? endDate, ulong? excludeDispatchId = null)
        => _quotaService.GetBranchDispatchQuotaAsync(branchId, startDate, endDate, excludeDispatchId);
}
