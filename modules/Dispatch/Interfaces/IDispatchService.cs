using Modules.Dispatch.DTOs;
using Shared.Common;

namespace Modules.Dispatch.Interfaces;

public interface IDispatchService
{
    Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request);
    Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request);
    Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId);
}

