using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// API Controller quản lý thiết bị Kiosk và cấu hình mạng/trình duyệt trạm Kiosk tại quầy (UC 1.2).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KioskController : ControllerBase
{
    private readonly IKioskService _kioskService;

    public KioskController(IKioskService kioskService)
    {
        _kioskService = kioskService;
    }

    /// <summary>
    /// [Operations Admin / Store Manager] Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (hiệu lực 15 phút).
    /// </summary>
    /// <param name="request">DTO chứa StoreId và tên Kiosk hiển thị</param>
    /// <returns>Mã kích hoạt OTP dạng POS-XXXX và thời gian hết hạn</returns>
    [HttpPost("create-code")]
    [Authorize(Roles = "StoreManager,STORE_MANAGER,OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
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
    /// Tự động ghi nhận địa chỉ IP và trình duyệt Kiosk gửi lên.
    /// </summary>
    /// <param name="request">DTO chứa mã kích hoạt OTP Code</param>
    /// <returns>DeviceToken bí mật cho trạm Kiosk lưu vào LocalStorage</returns>
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> ActivateKiosk([FromBody] ActivateKioskRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _kioskService.ActivateKioskAsync(request, clientIp, userAgent);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Public/Kiosk App] Xác minh DeviceToken của Kiosk khi bật ứng dụng hoặc Ping định kỳ.
    /// Tự động kiểm tra trạng thái trạm, trạng thái chi nhánh, địa chỉ IP và trình duyệt hợp lệ.
    /// </summary>
    /// <param name="request">DTO chứa DeviceToken cần kiểm tra</param>
    /// <returns>Xác nhận Token hợp lệ và trả về thông tin chi nhánh cửa hàng</returns>
    [HttpPost("verify-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> VerifyKioskToken([FromBody] VerifyKioskTokenRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _kioskService.VerifyKioskTokenAsync(request.DeviceToken, clientIp, userAgent);
        if (!result.Success) return Unauthorized(result);

        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các trạm Kiosk trong hệ thống toàn chuỗi.
    /// </summary>
    /// <returns>Danh sách chi tiết các trạm Kiosk</returns>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<KioskDetailDto>>>> GetAllKiosks()
    {
        var result = await _kioskService.GetAllKiosksAsync();
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin / Store Manager] Lấy thông tin chi tiết một trạm Kiosk theo ID.
    /// </summary>
    /// <param name="id">Mã ID trạm Kiosk</param>
    /// <returns>Thông tin chi tiết Kiosk</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = "StoreManager,STORE_MANAGER,OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<KioskDetailDto>>> GetKioskById(int id)
    {
        var result = await _kioskService.GetKioskByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin / Store Manager] Lấy danh sách toàn bộ các trạm Kiosk đã kích hoạt của một cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID cửa hàng</param>
    /// <returns>Danh sách các máy Kiosk thuộc cửa hàng</returns>
    [HttpGet("store/{storeId}")]
    [Authorize(Roles = "StoreManager,STORE_MANAGER,OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<KioskActivationResponseDto>>>> GetStoreKiosks(int storeId)
    {
        var result = await _kioskService.GetStoreKiosksAsync(storeId);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin / Store Manager] Thiết lập thông tin mạng (AllowedIp) và trình duyệt (AllowedBrowser) cho trạm Kiosk tại quầy.
    /// </summary>
    /// <param name="id">Mã ID trạm Kiosk cần cấu hình</param>
    /// <param name="dto">DTO cấu hình mạng và trình duyệt</param>
    /// <returns>Thông tin trạm Kiosk sau khi cập nhật cấu hình</returns>
    [HttpPut("{id}/config")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDetailDto>>> UpdateKioskConfig(int id, [FromBody] UpdateKioskConfigDto dto)
    {
        var result = await _kioskService.UpdateKioskConfigAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin / Store Manager] Khóa khẩn cấp hoặc Mở khóa trạm Kiosk quầy.
    /// Khi Kiosk bị khóa (LOCKED), ứng dụng Kiosk tại quầy sẽ bị từ chối truy cập và điểm danh ngay lập tức.
    /// </summary>
    /// <param name="id">Mã ID trạm Kiosk</param>
    /// <param name="dto">DTO chứa trạng thái mới ("ACTIVE" hoặc "LOCKED") và lý do</param>
    /// <returns>Thông tin trạm Kiosk sau khi đổi trạng thái</returns>
    [HttpPut("{id}/status")]
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,StoreManager,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<KioskDetailDto>>> UpdateKioskStatus(int id, [FromBody] UpdateKioskStatusDto dto)
    {
        var result = await _kioskService.UpdateKioskStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
