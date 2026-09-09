using RWFM.Modules.Dispatch.DTOs;
using RWFM.Shared.Common;

namespace RWFM.Modules.Dispatch.Interfaces;

public interface IDispatchService
{
    Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request);
    Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request);
    Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId);
}
