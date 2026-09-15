using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface dịch vụ nghiệp vụ quản lý danh mục chi nhánh & cấu hình Kiosk (UC 1.2 - Operations Admin).
/// </summary>
public interface IBranchService
{
    // ==========================================
    // 1. Branch CRUD
    // ==========================================
    Task<ApiResponse<List<BranchDto>>> GetAllBranchesAsync(string? status = null, string? search = null);
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
