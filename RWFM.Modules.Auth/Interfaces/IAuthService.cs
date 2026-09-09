using RWFM.Modules.Auth.DTOs;
using RWFM.Shared.Common;

namespace RWFM.Modules.Auth.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request);
    Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId);
    Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId);
}
