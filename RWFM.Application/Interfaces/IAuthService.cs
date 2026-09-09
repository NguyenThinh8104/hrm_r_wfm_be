using RWFM.Application.Common;
using RWFM.Application.DTOs;

namespace RWFM.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request);
    Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId);
    Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId);
}
