using Modules.Auth.DTOs;
using Shared.Common;

namespace Modules.Auth.Interfaces;

public interface IUserAccountService
{
    /// <summary>
    /// Soft-delete: đánh dấu Status = "DELETED", xóa ca tương lai, bảo lưu lịch sử chấm công / bàn giao.
    /// </summary>
    Task<ApiResponse<bool>> DeleteUserAsync(ulong userId, ulong actorId, string actorRole, string? ipAddress);

    Task<ApiResponse<bool>> ToggleUserStatusAsync(ulong userId, UpdateStatusDto dto, ulong actorId, string? ipAddress);

    Task<ApiResponse<ResetPasswordResultDto>> ResetPasswordAsync(ulong userId, ResetPasswordDto dto, ulong actorId, string? ipAddress);
}
