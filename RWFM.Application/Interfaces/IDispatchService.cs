using RWFM.Application.Common;
using RWFM.Application.DTOs;

namespace RWFM.Application.Interfaces;

public interface IDispatchService
{
    Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request);
    Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request);
    Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId);
}
