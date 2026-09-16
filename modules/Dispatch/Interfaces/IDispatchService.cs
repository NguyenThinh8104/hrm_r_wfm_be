using Modules.Dispatch.DTOs;
using Shared.Common;

namespace Modules.Dispatch.Interfaces;

public interface IDispatchService
{
    Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request);
    Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request);
    Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId);
    Task<ApiResponse<List<DispatchRecordDto>>> GetAllDispatchesAsync(string? status, int? storeId);
    Task<ApiResponse<DispatchNetworkMetricsDto>> GetNetworkMetricsAsync(DateOnly? fromDate, DateOnly? toDate);
    Task<ApiResponse<DispatchRecordDto>> UpdateDispatchRequestAsync(int managerId, int dispatchId, UpdateDispatchRequestDto request);
    Task<ApiResponse<bool>> DeleteDispatchRequestAsync(int managerId, int dispatchId);
}
