using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;

namespace Modules.Auth.Controllers;

/// <summary>
/// Quản trị Nhân sự & Phân quyền Vận hành (UC 1.4, UC 1.5)
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
    [Authorize(Roles = "OPERATIONS_ADMIN,BUSINESS_OWNER,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<StoreManagerDto>>>> GetStoreManagers()
    {
        var result = await _userService.GetStoreManagersAsync();
        return Ok(result);
    }

    /// <summary>
    /// UC 1.4: Cấp mới tài khoản quản trị cho Cửa hàng trưởng (Store Manager)
    /// </summary>
    [HttpPost("store-managers")]
    [Authorize(Roles = "OPERATIONS_ADMIN,BUSINESS_OWNER,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<StoreManagerDto>>> CreateStoreManager([FromBody] CreateStoreManagerDto dto)
    {
        var (actorId, _, _, ip) = GetCurrentUserInfo();
        var result = await _userService.CreateStoreManagerAsync(dto, actorId, ip);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// UC 1.4: Khóa hoặc kích hoạt lại tài khoản người dùng
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "OPERATIONS_ADMIN,BUSINESS_OWNER,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
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
    [Authorize(Roles = "OPERATIONS_ADMIN,BUSINESS_OWNER,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
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
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER,BUSINESS_OWNER,OperationsAdmin,StoreManager,BusinessOwner,Admin,ADMIN")]
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
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER,BUSINESS_OWNER,OperationsAdmin,StoreManager,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> GetEmployeeById(ulong id)
    {
        var (actorId, role, branchId, _) = GetCurrentUserInfo();
        var result = await _userService.GetEmployeeByIdAsync(id, actorId, role, branchId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.5: Thêm mới hồ sơ nhân sự (Chỉ dành riêng cho OPERATIONS_ADMIN, kiểm tra định biên chi nhánh Tier Quota)
    /// Hỗ trợ cả 2 route: /api/Users/employees và /api/v1/users/employees
    /// </summary>
    [HttpPost("employees")]
    [HttpPost("/api/v1/users/employees")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> CreateEmployee([FromBody] CreateEmployeeDto dto)
    {
        var (actorId, role, branchId, ip) = GetCurrentUserInfo();
        var result = await _userService.CreateEmployeeAsync(dto, actorId, role, branchId, ip);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// UC 1.5: Cập nhật hồ sơ & hợp đồng nhân sự
    /// </summary>
    [HttpPut("employees/{id}")]
    [Authorize(Roles = "OPERATIONS_ADMIN,STORE_MANAGER,BUSINESS_OWNER,OperationsAdmin,StoreManager,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> UpdateEmployee(ulong id, [FromBody] UpdateEmployeeDto dto)
    {
        var (actorId, role, branchId, ip) = GetCurrentUserInfo();
        var result = await _userService.UpdateEmployeeAsync(id, dto, actorId, role, branchId, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// UC 1.5: Tải file mẫu Excel chuẩn để phục vụ import nhân sự hàng loạt (kèm danh mục mã chi nhánh & vai trò).
    /// </summary>
    [HttpGet("employees/import-template")]
    [HttpGet("/api/v1/users/employees/import-template")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<IActionResult> DownloadEmployeeImportTemplate()
    {
        var (fileBytes, contentType, fileName) = await _userService.GenerateEmployeeImportTemplateAsync();
        return File(fileBytes, contentType, fileName);
    }

    /// <summary>
    /// UC 1.5: Thêm nhân sự hàng loạt bằng tệp tin Excel (.xlsx / .xls / .csv).
    /// Kiểm tra chặt chẽ định biên theo từng chi nhánh, kiểm tra trùng lặp danh tính và trả về báo cáo chi tiết.
    /// </summary>
    [HttpPost("employees/import")]
    [HttpPost("/api/v1/users/employees/import")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BulkImportResultDto>>> BulkImportEmployees([FromForm] BulkImportEmployeeRequestDto dto)
    {
        var (actorId, role, _, ip) = GetCurrentUserInfo();
        var result = await _userService.BulkImportEmployeesAsync(dto, actorId, role, ip);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    #endregion

    #region Metadata & Dropdowns

    /// <summary>
    /// Lấy danh mục 7 vai trò chuẩn hóa trong hệ thống
    /// </summary>
    [HttpGet("roles")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetRoles()
    {
        var result = await _userService.GetRolesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh mục chi nhánh phục vụ dropdown khai báo nhân sự
    /// </summary>
    [HttpGet("branches")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BranchSimpleDto>>>> GetBranches()
    {
        var result = await _userService.GetBranchesAsync();
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
