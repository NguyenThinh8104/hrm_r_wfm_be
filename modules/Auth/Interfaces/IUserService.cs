using Modules.Auth.DTOs;
using Shared.Common;

namespace Modules.Auth.Interfaces;

public interface IUserService
{
    // UC 1.4: Quản lý Tài khoản & Phân quyền Vận hành (Store Managers)
    Task<ApiResponse<List<StoreManagerDto>>> GetStoreManagersAsync();
    Task<ApiResponse<StoreManagerDto>> CreateStoreManagerAsync(CreateStoreManagerDto dto, ulong actorId, string? ipAddress);
    Task<ApiResponse<bool>> ToggleUserStatusAsync(ulong userId, UpdateStatusDto dto, ulong actorId, string? ipAddress);
    Task<ApiResponse<bool>> ResetPasswordAsync(ulong userId, ResetPasswordDto dto, ulong actorId, string? ipAddress);

    // UC 1.5: Quản lý Hồ sơ & Hợp đồng Nhân sự Toàn chuỗi
    Task<ApiResponse<List<EmployeeDetailDto>>> GetEmployeesAsync(EmployeeFilterDto filter, ulong actorId, string actorRole, ulong? actorBranchId);
    Task<ApiResponse<EmployeeDetailDto>> GetEmployeeByIdAsync(ulong userId, ulong actorId, string actorRole, ulong? actorBranchId);
    Task<ApiResponse<EmployeeDetailDto>> CreateEmployeeAsync(CreateEmployeeDto dto, ulong actorId, string actorRole, ulong? actorBranchId, string? ipAddress);
    Task<ApiResponse<EmployeeDetailDto>> UpdateEmployeeAsync(ulong userId, UpdateEmployeeDto dto, ulong actorId, string actorRole, ulong? actorBranchId, string? ipAddress);

    // Danh mục Roles & Chi nhánh
    Task<ApiResponse<List<RoleDto>>> GetRolesAsync();
    Task<ApiResponse<List<BranchSimpleDto>>> GetBranchesAsync();
}
