using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// Controller quản lý danh mục chi nhánh cửa hàng & trạm Kiosk thuộc chi nhánh (UC 1.2 - Operations Admin).
/// </summary>
[ApiController]
[Route("api/v1/branches")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống (hỗ trợ lọc status & search).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAllBranches(
        [FromQuery] string? status = null, 
        [FromQuery] string? search = null)
    {
        var result = await _branchService.GetAllBranchesAsync(status, search);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy thông tin chi tiết của một chi nhánh theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(ulong id)
    {
        var result = await _branchService.GetBranchByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một chi nhánh cửa hàng vào hệ thống.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> CreateBranch([FromBody] CreateBranchDto dto)
    {
        var result = await _branchService.CreateBranchAsync(dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin chi nhánh cửa hàng (Tên, địa chỉ, số điện thoại).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranch(ulong id, [FromBody] UpdateBranchDto dto)
    {
        var result = await _branchService.UpdateBranchAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Khóa hoặc Mở khóa chi nhánh cửa hàng (ACTIVE / INACTIVE).
    /// Nghiệp vụ: Khi khóa Branch (INACTIVE), toàn bộ Kiosk thuộc chi nhánh tự động chuyển sang BLOCKED.
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranchStatus(ulong id, [FromBody] UpdateBranchStatusDto dto)
    {
        var result = await _branchService.UpdateBranchStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các trạm Kiosk thuộc một chi nhánh.
    /// </summary>
    [HttpGet("{id}/kiosks")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<List<KioskDto>>>> GetBranchKiosks(ulong id)
    {
        var result = await _branchService.GetBranchKiosksAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một trạm Kiosk tại chi nhánh cửa hàng (Tự sinh kiosk_token và kiosk_code).
    /// </summary>
    [HttpPost("{id}/kiosks")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> CreateBranchKiosk(ulong id, [FromBody] CreateKioskDto dto)
    {
        var result = await _branchService.CreateBranchKioskAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }
}
