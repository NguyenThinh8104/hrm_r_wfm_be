using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// Quản lý thiết bị Kiosk quầy — CRUD dành cho Admin và Store Manager (UC 1.2).
/// Route chuẩn hóa: api/kiosks
/// </summary>
[ApiController]
[Route("api/kiosks")]
[Authorize]
public class KiosksController : ControllerBase
{
    private readonly IKioskService _kioskService;

    public KiosksController(IKioskService kioskService)
    {
        _kioskService = kioskService;
    }

    /// <summary>
    /// [OpsAdmin/Owner] Lấy danh sách toàn bộ các trạm Kiosk trong hệ thống toàn chuỗi.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<KioskDto>>>> GetAllKiosks()
    {
        var result = await _kioskService.GetAllKiosksAsync();
        return Ok(result);
    }

    /// <summary>
    /// [OpsAdmin/Owner/StoreManager/ShiftLeader] Lấy thông tin chi tiết một trạm Kiosk theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> GetKioskById(ulong id)
    {
        var result = await _kioskService.GetKioskByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [StoreManager/OpsAdmin/Owner] Lấy danh sách các trạm Kiosk đã kích hoạt của một cửa hàng.
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize(Roles = "STORE_MANAGER,SHIFT_LEADER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,ShiftLeader,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<KioskActivationResponseDto>>>> GetStoreKiosks(int storeId)
    {
        var result = await _kioskService.GetStoreKiosksAsync(storeId);
        return Ok(result);
    }

    /// <summary>
    /// [OpsAdmin/Owner/StoreManager/ShiftLeader] Cập nhật thông tin cấu hình trạm Kiosk (DeviceName, IpWhitelist, UserAgentPattern).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> UpdateKiosk(ulong id, [FromBody] UpdateKioskDto dto)
    {
        var result = await _kioskService.UpdateKioskAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [OpsAdmin/Owner/StoreManager/ShiftLeader] Cập nhật trạng thái trạm Kiosk (ACTIVE / BLOCKED / INACTIVE).
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> UpdateKioskStatus(ulong id, [FromBody] UpdateKioskStatusDto dto)
    {
        var result = await _kioskService.UpdateKioskStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [StoreManager/OpsAdmin/Owner/ShiftLeader] Hủy ghép nối / Dừng hoạt động một trạm Kiosk theo KioskId.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "STORE_MANAGER,SHIFT_LEADER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,ShiftLeader,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateKiosk(int id)
    {
        var result = await _kioskService.DeactivateKioskAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
