using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;

namespace Modules.Auth.Controllers;

/// <summary>
/// Quản trị Nhân sự, Phân quyền Vận hành & Cấp mã PIN Kiosk (UC 1.4, UC 1.5, UC 1.6)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    #region UC 1.4 - Quản lý Tài khoản & Phân quyền Vận hành (Store Manager)

    /// <summary>
    /// UC 1.4: Lấy danh sách tài khoản Cửa hàng trưởng toàn chuỗi
    /// </summary>
    [HttpGet("store-managers")]
    [Authorize(Roles = "OPERATIONS_ADMIN,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<StoreManagerDto>>>> GetStoreManagers()
    {
        var result = await _userService.GetStoreManagersAsync();
        return Ok(result);
    }

    /// <summary>
    /// UC 1.4: Cấp mới tài khoản quản trị cho Cửa hàng trưởng (Store Manager)
    /// </summary>
    [HttpPost("store-managers")]
    [Authorize(Roles = "OPERATIONS_ADMIN")]
    public async Task<ActionResult<ApiResponse<StoreManagerDto>>> CreateStoreManager([FromBody] CreateStoreManagerDto dto)
    {
        var (actorId, _, _, ip) = GetCurrentUserInfo();
        var result = await _userService.CreateStoreManagerAsync(dto, actorId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.4: Khóa hoặc kích hoạt lại tài khoản người dùng
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "OPERATIONS_ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleUserStatus(ulong id, [FromBody] UpdateStatusDto dto)
    {
        var (actorId, _, _, ip) = GetCurrentUserInfo();
        var result = await _userService.ToggleUserStatusAsync(id, dto, actorId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.4: Đặt lại mật khẩu đăng nhập tài khoản
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [Authorize(Roles = "OPERATIONS_ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword(ulong id, [FromBody] ResetPasswordDto dto)
    {
        var (actorId, _, _, ip) = GetCurrentUserInfo();
        var result = await _userService.ResetPasswordAsync(id, dto, actorId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region UC 1.5 - Quản lý Hồ sơ & Hợp đồng Nhân sự Toàn chuỗi

    /// <summary>
    /// UC 1.5: Lấy danh sách hồ sơ nhân sự (kèm bộ lọc chi nhánh, chức danh, full-time/part-time)
    /// </summary>
    [HttpGet("employees")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<EmployeeDetailDto>>>> GetEmployees([FromQuery] EmployeeFilterDto filter)
    {
        var (actorId, role, branchId, _) = GetCurrentUserInfo();
        var result = await _userService.GetEmployeesAsync(filter, actorId, role, branchId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.5: Xem chi tiết hồ sơ nhân sự
    /// </summary>
    [HttpGet("employees/{id}")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> GetEmployeeById(ulong id)
    {
        var (actorId, role, branchId, _) = GetCurrentUserInfo();
        var result = await _userService.GetEmployeeByIdAsync(id, actorId, role, branchId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.5: Thêm mới hồ sơ nhân sự (Full-time / Part-time, gán chức danh & chi nhánh gốc)
    /// </summary>
    [HttpPost("employees")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> CreateEmployee([FromBody] CreateEmployeeDto dto)
    {
        var (actorId, role, branchId, ip) = GetCurrentUserInfo();
        var result = await _userService.CreateEmployeeAsync(dto, actorId, role, branchId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.5: Cập nhật hồ sơ & hợp đồng nhân sự
    /// </summary>
    [HttpPut("employees/{id}")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> UpdateEmployee(ulong id, [FromBody] UpdateEmployeeDto dto)
    {
        var (actorId, role, branchId, ip) = GetCurrentUserInfo();
        var result = await _userService.UpdateEmployeeAsync(id, dto, actorId, role, branchId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region UC 1.6 - Cấp mã PIN Chấm công Kiosk

    /// <summary>
    /// UC 1.6: Cấp mới hoặc sinh lại mã PIN định danh chấm công trên trạm Kiosk
    /// </summary>
    [HttpPost("employees/{id}/reset-pin")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<ResetPinResponseDto>>> ResetKioskPin(ulong id, [FromBody] ResetPinRequestDto dto)
    {
        var (actorId, role, branchId, ip) = GetCurrentUserInfo();
        var result = await _userService.ResetKioskPinAsync(id, dto, actorId, role, branchId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region Metadata

    /// <summary>
    /// Lấy danh mục 7 vai trò chuẩn hóa trong hệ thống
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
    {
        var result = await _userService.GetRolesAsync();
        return Ok(result);
    }

    #endregion

    #region Helper

    private (ulong actorId, string role, ulong? branchId, string? ip) GetCurrentUserInfo()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        ulong.TryParse(idStr, out var actorId);

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var storeIdStr = User.FindFirst("StoreId")?.Value;
        ulong? branchId = ulong.TryParse(storeIdStr, out var bId) ? bId : null;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        return (actorId, role, branchId, ip);
    }

    #endregion
}
