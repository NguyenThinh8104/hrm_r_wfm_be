using Modules.Auth.DTOs;
using Shared.Common;

namespace Modules.Auth.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request);
    Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId);
    Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId);
}

