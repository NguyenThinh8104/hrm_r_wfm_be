using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// Xác thực và ghép nối thiết bị Kiosk POS — OTP activation, token verify, unpair.
/// Route: api/kiosk-auth
/// Dùng bởi: Web Admin (tạo mã OTP) và ứng dụng Kiosk POS (kích hoạt / xác minh token).
/// </summary>
[ApiController]
[Route("api/kiosk-auth")]
[Route("api/kiosk")]
public class KioskAuthController : ControllerBase
{
    private readonly IKioskService _kioskService;

    public KioskAuthController(IKioskService kioskService)
    {
        _kioskService = kioskService;
    }

    /// <summary>
    /// [StoreManager/OpsAdmin/Owner] Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (hiệu lực 15 phút).
    /// Được gọi từ Web Admin khi Store Manager muốn ghép nối máy Kiosk mới.
    /// </summary>
    [HttpPost("create-code")]
    [Authorize(Roles = "STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskCodeResponseDto>>> CreateKioskCode([FromBody] CreateKioskCodeRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var managerUserId);

        var result = await _kioskService.CreateKioskCodeAsync(managerUserId, request);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Public / Kiosk App] Kích hoạt trạm Kiosk mới tại quầy bằng mã OTP 6 ký tự.
    /// Trả về DeviceToken bí mật cho trạm Kiosk lưu vào LocalStorage.
    /// </summary>
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
    /// [Public / Kiosk App] Xác minh DeviceToken của Kiosk khi bật ứng dụng hoặc Ping định kỳ.
    /// Trả về thông tin chi nhánh cửa hàng nếu token hợp lệ.
    /// </summary>
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
    /// [Public / Kiosk App] Hủy ghép nối trạm Kiosk bằng DeviceToken khi người dùng chọn Đăng xuất trên ứng dụng Kiosk.
    /// </summary>
    [HttpPost("unpair")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> UnpairKiosk([FromBody] VerifyKioskTokenRequestDto request)
    {
        var result = await _kioskService.UnpairKioskTokenAsync(request.DeviceToken);
        return Ok(result);
    }
}
