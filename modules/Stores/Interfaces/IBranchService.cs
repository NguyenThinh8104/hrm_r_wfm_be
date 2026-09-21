using Domain.Enums;
using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý danh mục chi nhánh cửa hàng & tọa độ GPS Geofence (UC 1.2 - Operations Admin).
/// </summary>
public interface IBranchService
{
    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh (hỗ trợ lọc theo trạng thái hoạt động, từ khóa tìm kiếm và phân cấp chi nhánh Tier).
    /// </summary>
    /// <param name="status">Trạng thái chi nhánh (ACTIVE / INACTIVE).</param>
    /// <param name="search">Từ khóa tìm kiếm (mã chi nhánh, tên, địa chỉ).</param>
    /// <param name="tier">Phân cấp chi nhánh (1 = Tier 1, 2 = Tier 2, 3 = Tier 3).</param>
    Task<ApiResponse<List<BranchDto>>> GetAllBranchesAsync(string? status = null, string? search = null, BranchTier? tier = null);

    /// <summary>
    /// Thống kê tổng quan số lượng chi nhánh theo từng phân cấp (Tier 1, Tier 2, Tier 3).
    /// </summary>
    Task<ApiResponse<BranchTierSummaryDto>> GetBranchTierSummaryAsync();

    Task<ApiResponse<BranchDto>> GetBranchByIdAsync(ulong id);
    Task<ApiResponse<BranchDto>> CreateBranchAsync(CreateBranchDto dto);
    Task<ApiResponse<BranchDto>> UpdateBranchAsync(ulong id, UpdateBranchDto dto);
    Task<ApiResponse<BranchDto>> UpdateBranchStatusAsync(ulong id, UpdateBranchStatusDto dto);
    Task<ApiResponse<bool>> DeleteBranchAsync(ulong id);
    // ==========================================
    // 2. Kiosk Management
    // ==========================================
    Task<ApiResponse<List<KioskDto>>> GetBranchKiosksAsync(ulong branchId);
    Task<ApiResponse<KioskDto>> CreateBranchKioskAsync(ulong branchId, CreateKioskDto dto);
    Task<ApiResponse<KioskDto>> UpdateKioskAsync(ulong kioskId, UpdateKioskDto dto);
    Task<ApiResponse<KioskDto>> UpdateKioskStatusAsync(ulong kioskId, UpdateKioskStatusDto dto);
    Task<ApiResponse<List<KioskDto>>> GetAllKiosksAsync();
    Task<ApiResponse<KioskDto>> GetKioskByIdAsync(ulong kioskId);
}

public interface IStoreService : IBranchService { }
