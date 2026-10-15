using Modules.Shifts.DTOs;
using Shared.Common;

namespace Modules.Shifts.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý khung ca làm việc mẫu toàn hệ thống & chi nhánh (UC 1.3).
/// </summary>
public interface IShiftTemplateService
{
    Task<PagedResult<ShiftTemplateDto>> GetShiftTemplatesPagedAsync(string? scope, ulong? branchId, string? type, bool? isActive, string? q, int page, int pageSize);
    Task<ShiftTemplateStatsDto> GetShiftTemplateStatsAsync();
    Task<ShiftOperationResult<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateRequest request, ulong? actorId = null, string? ipAddress = null);
    Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateRequest request, bool confirm, ulong? actorId = null, string? ipAddress = null);
    Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateActiveAsync(uint id, bool isActive, bool confirm, ulong? actorId = null, string? ipAddress = null);
    Task<ShiftOperationResult<BranchEffectiveShiftsDto>> GetBranchEffectiveShiftsAsync(ulong branchId, ulong? currentUserId = null, string? currentUserRole = null);
    Task<ShiftOperationResult<List<ShiftTemplateDto>>> GetBranchCustomShiftsAsync(ulong branchId);
    Task<ShiftOperationResult<BranchEffectiveShiftsDto>> UpdateBranchShiftModeAsync(ulong branchId, string mode, bool confirm, ulong? actorId = null, string? ipAddress = null);
    Task<List<BranchSelectorItemDto>> GetBranchesForSelectorAsync(string? shiftMode, string? q);

    // Phương thức CRUD cơ bản & tương thích
    Task<ApiResponse<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto);
    Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto);
    Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateStatusAsync(uint id, UpdateShiftTemplateStatusDto dto);
    Task<ApiResponse<ShiftTemplateDto>> GetShiftTemplateByIdAsync(uint id);
    Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id);
    Task<ApiResponse<List<ShiftTemplateDto>>> GetAllShiftTemplatesAsync(string? status = null);
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(ulong? branchId = null, bool includeInactive = false);
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync();
}
