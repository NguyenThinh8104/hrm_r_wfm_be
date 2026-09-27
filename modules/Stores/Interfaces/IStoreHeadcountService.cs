using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface nghiệp vụ tra cứu định biên nhân sự chi nhánh (Stores Module).
/// </summary>
public interface IStoreHeadcountService
{
    /// <summary>
    /// Lấy thông tin định biên chi nhánh (Quy mô Tier, Quota, Quân số hiện tại, Số vị trí trống).
    /// </summary>
    Task<ApiResponse<BranchHeadcountStatusDto>> GetBranchHeadcountStatusAsync(ulong branchId);
}
