using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Stores.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IKioskService _kioskService;

    public StoresController(AppDbContext context, IKioskService kioskService)
    {
        _context = context;
        _kioskService = kioskService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetAllStores()
    {
        var stores = await _context.Stores
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.StoreId,
                s.StoreCode,
                s.StoreName,
                s.Address,
                s.Phone
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(stores, "Lấy danh sách cửa hàng thành công."));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetStoreById(int id)
    {
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.StoreId == id);

        if (store == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));

        return Ok(ApiResponse<object>.Ok(new
        {
            store.StoreId,
            store.StoreCode,
            store.StoreName,
            store.Address,
            store.Phone
        }));
    }

    [HttpPost("create-kiosk-code")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<KioskCodeResponseDto>>> CreateKioskCode([FromBody] CreateKioskCodeRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var managerUserId);

        var result = await _kioskService.CreateKioskCodeAsync(managerUserId, request);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("activate-kiosk")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> ActivateKiosk([FromBody] ActivateKioskRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _kioskService.ActivateKioskAsync(request, clientIp);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("verify-kiosk-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<KioskActivationResponseDto>>> VerifyKioskToken([FromBody] VerifyKioskTokenRequestDto request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _kioskService.VerifyKioskTokenAsync(request.DeviceToken, clientIp);
        if (!result.Success) return Unauthorized(result);

        return Ok(result);
    }

    [HttpGet("{storeId}/kiosks")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<KioskActivationResponseDto>>>> GetStoreKiosks(int storeId)
    {
        var result = await _kioskService.GetStoreKiosksAsync(storeId);
        return Ok(result);
    }
}

