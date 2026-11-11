using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Security;

namespace Modules.Auth.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    private readonly WebSession _webSession;

    public AuthController(IAuthService authService, WebSession webSession)
    {
        _authService = authService;
        _webSession = webSession;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<UserSummaryDto>>> Login([FromBody] LoginRequestDto request)
    {
        return await StartWebSession(await _authService.LoginAsync(request));
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

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<bool>>> Logout()
    {
        await _webSession.EndAsync(HttpContext);
        if (User.Identity?.AuthenticationType != WebSession.Scheme &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await _authService.RevokeTokenAsync(userId);
        }

        DeleteRefreshTokenCookie();
        return Ok(ApiResponse<bool>.Ok(true, "Đăng xuất thành công."));
    }

    private async Task<ActionResult<ApiResponse<UserSummaryDto>>> StartWebSession(ApiResponse<WebLoginResult> result)
    {
        if (!result.Success || result.Data == null)
            return BadRequest(ApiResponse<UserSummaryDto>.Fail(result.Message, result.Errors));

        await _webSession.StartAsync(HttpContext, result.Data.Identity);
        DeleteRefreshTokenCookie();
        return Ok(ApiResponse<UserSummaryDto>.Ok(result.Data.User, result.Message));
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
    public async Task<ActionResult<ApiResponse<UserSummaryDto>>> GoogleLogin([FromBody] GoogleLoginDTOs request)
    {
        return await StartWebSession(await _authService.GoogleLoginAsync(request));
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


