using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// API Controller quản lý danh mục chi nhánh cửa hàng (Route tương thích: /api/Stores).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly IBranchService _branchService;

    public StoresController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAllStores([FromQuery] string? status = null, [FromQuery] string? search = null)
    {
        var result = await _branchService.GetAllBranchesAsync(status, search);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một cửa hàng theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetStoreById(ulong id)
    {
        var result = await _branchService.GetBranchByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới chi nhánh cửa hàng vào chuỗi siêu thị.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> CreateStore([FromBody] CreateBranchDto dto)
    {
        var result = await _branchService.CreateBranchAsync(dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin chi nhánh cửa hàng.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateStore(ulong id, [FromBody] UpdateBranchDto dto)
    {
        var result = await _branchService.UpdateBranchAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Khóa hoặc Mở khóa chi nhánh cửa hàng.
    /// </summary>
    [HttpPut("{id}/status")]
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateStoreStatus(ulong id, [FromBody] UpdateBranchStatusDto dto)
    {
        var result = await _branchService.UpdateBranchStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Xóa chi nhánh cửa hàng.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStore(ulong id)
    {
        var result = await _branchService.DeleteBranchAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
