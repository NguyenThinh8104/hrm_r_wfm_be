using Modules.Auth.DTOs;
using Shared.Common;

namespace Modules.Auth.Interfaces;

public interface IAuthService
{

    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request);
    Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId);
    Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId);
    Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task<ApiResponse<bool>> VerifyOtpAsync(VerifyOtpRequestDto request);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto request);
    Task<ApiResponse<AuthResponseDto>> GoogleLoginAsync(GoogleLoginDTOs request);
    Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordDto request);
    Task<ApiResponse<UserSummaryDto>> UpdateProfileAsync(int userId, UpdateProfileDto request);
    Task<ApiResponse<List<NotificationItemDto>>> GetNotificationsAsync(int userId);
}



