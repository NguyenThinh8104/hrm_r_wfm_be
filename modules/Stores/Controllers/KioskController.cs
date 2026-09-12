using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// API Controller quản lý thiết bị Kiosk và quy trình kích hoạt trạm Kiosk tại quầy cửa hàng.
/// </summary>
[ApiController]
[Route("api/kiosk")]
public class KioskController : ControllerBase
{
    private readonly IKioskService _kioskService;

    public KioskController(IKioskService kioskService)
    {
        _kioskService = kioskService;
    }

    /// <summary>
    /// [StoreManager/OpsAdmin/Owner] Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (hiệu lực 15 phút).
    /// </summary>
    /// <param name="request">DTO chứa StoreId và tên Kiosk hiển thị</param>
    /// <returns>Mã kích hoạt OTP dạng POS-XXXX và thời gian hết hạn</returns>
    [HttpPost("create-code")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<KioskCodeResponseDto>>> CreateKioskCode([FromBody] CreateKioskCodeRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var managerUserId);

        var result = await _kioskService.CreateKioskCodeAsync(managerUserId, request);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Public/Kiosk App] Kích hoạt trạm Kiosk mới tại quầy bằng mã OTP 6 ký tự.
    /// </summary>
    /// <param name="request">DTO chứa mã kích hoạt OTP Code</param>
    /// <returns>DeviceToken bí mật cho trạm Kiosk lưu vào LocalStorage</returns>
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> ActivateKiosk([FromBody] ActivateKioskRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _kioskService.ActivateKioskAsync(request, clientIp);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Public/Kiosk App] Xác minh DeviceToken của Kiosk khi bật ứng dụng hoặc Ping định kỳ.
    /// </summary>
    /// <param name="request">DTO chứa DeviceToken cần kiểm tra</param>
    /// <returns>Xác nhận Token hợp lệ và trả về thông tin chi nhánh cửa hàng</returns>
    [HttpPost("verify-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> VerifyKioskToken([FromBody] VerifyKioskTokenRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _kioskService.VerifyKioskTokenAsync(request.DeviceToken, clientIp);
        if (!result.Success) return Unauthorized(result);

        return Ok(result);
    }

    /// <summary>
    /// [StoreManager/OpsAdmin/Owner] Lấy danh sách toàn bộ các trạm Kiosk đã kích hoạt của một cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID cửa hàng</param>
    /// <returns>Danh sách các máy Kiosk thuộc cửa hàng</returns>
    [HttpGet("store/{storeId}")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<KioskActivationResponseDto>>>> GetStoreKiosks(int storeId)
    {
        var result = await _kioskService.GetStoreKiosksAsync(storeId);
        return Ok(result);
    }
}
