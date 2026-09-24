using Domain.Entities;
using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

public interface ITierService
{
    Task<ApiResponse<List<TierDto>>> GetAllTiersAsync();
    Task<ApiResponse<TierDto>> GetTierByIdAsync(int id);
    Task<ApiResponse<TierDto>> CreateTierAsync(CreateTierDto dto);
    Task<ApiResponse<TierDto>> UpdateTierAsync(int id, UpdateTierDto dto);
    Task<ApiResponse<bool>> DeleteTierAsync(int id);
    Task<BranchTierEntity?> MatchTierByStaffCountAsync(int staffCount);
}
