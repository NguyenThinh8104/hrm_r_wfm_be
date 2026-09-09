using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWFM.Application.Common;
using RWFM.Application.DTOs;
using RWFM.Application.Interfaces;

namespace RWFM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("kiosk-login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> KioskLogin([FromBody] KioskLoginRequestDto request)
    {
        var result = await _authService.KioskLoginAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserSummaryDto>>> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserSummaryDto>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _authService.GetCurrentUserAsync(userId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpGet("store-employees/{storeId}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<UserSummaryDto>>>> GetStoreEmployees(int storeId)
    {
        var result = await _authService.GetStoreEmployeesAsync(storeId);
        return Ok(result);
    }
}
