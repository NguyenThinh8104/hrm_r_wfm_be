using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

public interface IKioskService
{
    Task<ApiResponse<KioskCodeResponseDto>> CreateKioskCodeAsync(int managerUserId, CreateKioskCodeRequestDto request);
    Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp);
    Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp);
    Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId);
}

