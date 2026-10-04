using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Common.Constants;

namespace Modules.Auth.Controllers;

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
        SetRefreshTokenCookie(result.Data?.RefreshToken);
        return Ok(result);
    }

    [HttpPost("kiosk-login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> KioskLogin([FromBody] KioskLoginRequestDto request)
    {
        var result = await _authService.KioskLoginAsync(request);
        if (!result.Success) return BadRequest(result);
        SetRefreshTokenCookie(result.Data?.RefreshToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken([FromBody] RefreshTokenRequestDto? bodyRequest)
    {
        var refreshToken = Request.Cookies["refreshToken"] ?? bodyRequest?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Không tìm thấy Refresh Token. Vui lòng đăng nhập lại."));
        }

        var result = await _authService.RefreshTokenAsync(refreshToken);
        if (!result.Success)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(result);
        }

        SetRefreshTokenCookie(result.Data?.RefreshToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            await _authService.RevokeTokenAsync(userId);
        }

        DeleteRefreshTokenCookie();
        return Ok(ApiResponse<bool>.Ok(true, "Đăng xuất thành công."));
    }

    private void SetRefreshTokenCookie(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Cực kỳ quan trọng: JavaScript không thể đọc được -> Chống XSS
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserSummaryDto>>> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserSummaryDto>.Fail(AuthMessages.USER_IDENTITY_NOT_FOUND));
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

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> VerifyOtp([FromBody] VerifyOtpRequestDto request)
    {
        var result = await _authService.VerifyOtpAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GoogleLogin([FromBody] GoogleLoginDTOs request)
    {
        var result = await _authService.GoogleLoginAsync(request);
        if (!result.Success) return BadRequest(result);
        SetRefreshTokenCookie(result.Data?.RefreshToken);
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Fail(AuthMessages.USER_IDENTITY_NOT_FOUND));
        }

        var result = await _authService.ChangePasswordAsync(userId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserSummaryDto>>> UpdateProfile([FromBody] UpdateProfileDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<UserSummaryDto>.Fail(AuthMessages.USER_IDENTITY_NOT_FOUND));
        }

        var result = await _authService.UpdateProfileAsync(userId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("notifications")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<NotificationItemDto>>>> GetNotifications()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<List<NotificationItemDto>>.Fail(AuthMessages.USER_IDENTITY_NOT_FOUND));
        }

        var result = await _authService.GetNotificationsAsync(userId);
        return Ok(result);
    }
}


