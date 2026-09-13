using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// Controller quản lý thiết bị Kiosk quầy (UC 1.2 - Operations Admin).
/// </summary>
[ApiController]
[Route("api/v1/kiosks")]
public class KiosksController : ControllerBase
{
    private readonly IBranchService _branchService;

    public KiosksController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các trạm Kiosk trong hệ thống toàn chuỗi.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<KioskDto>>>> GetAllKiosks()
    {
        var result = await _branchService.GetAllKiosksAsync();
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy thông tin chi tiết một trạm Kiosk theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> GetKioskById(ulong id)
    {
        var result = await _branchService.GetKioskByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin cấu hình trạm Kiosk (DeviceName, IpWhitelist, UserAgentPattern).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> UpdateKiosk(ulong id, [FromBody] UpdateKioskDto dto)
    {
        var result = await _branchService.UpdateKioskAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật trạng thái trạm Kiosk (ACTIVE / BLOCKED / INACTIVE).
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> UpdateKioskStatus(ulong id, [FromBody] UpdateKioskStatusDto dto)
    {
        var result = await _branchService.UpdateKioskStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
