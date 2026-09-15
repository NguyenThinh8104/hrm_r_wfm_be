using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Security;
using Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Shared.Common.Constants;
using Shared.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;

    private const int OTP_EXPIRY_MINUTES = 5;

public AuthService(AppDbContext context, JwtTokenService jwtTokenService, IEmailService emailService, IConfiguration config, IMemoryCache cache, ILogger<AuthService> logger)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.USERNAME_PASSWORD_INVALID);
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Username.Trim().ToLower() 
                                   || u.EmployeeCode.ToLower() == request.Username.Trim().ToLower());

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.INVALID_CREDENTIALS);
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.ACCOUNT_LOCKED);
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        var summary = MapUserSummary(user);

        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = summary
        }, AuthMessages.LOGIN_SUCCESS);
    }

    public async Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || string.IsNullOrWhiteSpace(request.PinCode))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.KIOSK_LOGIN_REQUIRED);
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.EmployeeCode.ToLower() == request.EmployeeCode.Trim().ToLower() && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.EMPLOYEE_NOT_FOUND);
        }

        if (string.IsNullOrEmpty(user.KioskPinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.PIN_INVALID);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var isDispatched = await _context.TemporaryDispatches
            .AnyAsync(d => d.UserId == user.Id && d.TargetBranchId == (ulong)request.StoreId 
                        && d.StartDate <= today && d.EndDate >= today && d.Status == "APPROVED");

        if (user.HomeBranchId != (ulong)request.StoreId && !isDispatched)
        {
            return ApiResponse<AuthResponseDto>.Fail(string.Format(AuthMessages.NOT_ASSIGNED_TO_BRANCH, user.FullName));
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        var summary = MapUserSummary(user);

        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = summary
        }, AuthMessages.KIOSK_LOGIN_SUCCESS);
    }

    public async Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId);

        if (user == null)
        {
            return ApiResponse<UserSummaryDto>.Fail(AuthMessages.USER_NOT_FOUND);
        }

        return ApiResponse<UserSummaryDto>.Ok(MapUserSummary(user));
    }



    private static UserSummaryDto MapUserSummary(User user)
    {
        return new UserSummaryDto
        {
            UserId = (int)user.Id,
            EmployeeId = (int)user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            Username = user.EmployeeCode,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role?.RoleCode ?? "STORE_MANAGER",
            RoleName = user.Role?.RoleName ?? "Quản lý",
            StoreId = user.HomeBranchId.HasValue ? (int)user.HomeBranchId.Value : null,
            StoreName = user.HomeBranch?.Name,
            PositionName = user.Role?.RoleName
        };
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1. Kiểm tra Email có tồn tại trong hệ thống hay không
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            return ApiResponse<bool>.Fail(AuthMessages.EMAIL_NOT_IN_SYSTEM);
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<bool>.Fail(AuthMessages.ACCOUNT_INACTIVE);
        }

        // 2. Sinh mã OTP 6 chữ số ngẫu nhiên
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var cacheKey = $"RESET_OTP_{email}";

        // 3. Thiết lập MemoryCache với Absolute Expiration và CacheItemPriority.NeverRemove để đảm bảo không bị hủy trước 5 phút
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES))
            .SetPriority(CacheItemPriority.NeverRemove);

        _cache.Set(cacheKey, otpCode, cacheEntryOptions);

        // 4. Gửi email OTP tới địa chỉ nhân viên
        var sendEmailSuccess = await _emailService.SendPassResetOtpEmailAsync(user.Email, user.FullName, otpCode, OTP_EXPIRY_MINUTES);
        if (!sendEmailSuccess)
        {
            // Nếu gửi mail thất bại, xóa cache để tránh kẹt mã
            _cache.Remove(cacheKey);
            return ApiResponse<bool>.Fail(AuthMessages.EMAIL_SEND_FAILED);
        }

        return ApiResponse<bool>.Ok(true, string.Format(AuthMessages.OTP_SENT_SUCCESS, user.Email, OTP_EXPIRY_MINUTES));
    }

    public async Task<ApiResponse<bool>> VerifyOtpAsync(VerifyOtpRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var cacheKey = $"RESET_OTP_{email}";

        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || string.IsNullOrEmpty(cachedOtp))
        {
            return ApiResponse<bool>.Fail(ResetPassword.OTP_EXPIRED);
        }

        if (cachedOtp != request.OtpCode.Trim())
        {
            return ApiResponse<bool>.Fail(ResetPassword.OTP_INVALID);
        }

        // Kiểm tra User trong database
        var userExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email && u.Status == "ACTIVE");
        if (!userExists)
        {
            return ApiResponse<bool>.Fail(ResetPassword.USER_NOT_FOUND);
        }

        return ApiResponse<bool>.Ok(true, ResetPassword.OTP_SUCCESS);
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var cacheKey = $"RESET_OTP_{email}";

        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || string.IsNullOrEmpty(cachedOtp))
        {
            return ApiResponse<bool>.Fail(ResetPassword.OTP_EXPIRED);
        }

        if (cachedOtp != request.OtpCode.Trim())
        {
            return ApiResponse<bool>.Fail(ResetPassword.OTP_INVALID);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            return ApiResponse<bool>.Fail(ResetPassword.EMAIL_NOT_FOUND);
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Xóa mã OTP khỏi Cache ngay lập tức để chống tái sử dụng
        _cache.Remove(cacheKey);

        _logger.LogInformation("Nhân viên {EmployeeCode} ({Email}) đã đặt lại mật khẩu thành công.", user.EmployeeCode, user.Email);

        return ApiResponse<bool>.Ok(true, ResetPassword.PASSWORD_RESET_SUCCESS);
    }

    public Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse<AuthResponseDto>> GoogleLoginAsync(GoogleLoginDTOs request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.ID_TOKEN_REQUIRED);
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var clientId = _config["Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrEmpty(clientId))
            {
                settings.Audience = new[] { clientId };
            }

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Xác thực Google ID Token thất bại: {Message}", ex.Message);
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.GOOGLE_TOKEN_INVALID);
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.GOOGLE_TOKEN_INVALID);
        }

        if (!payload.EmailVerified)
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.GOOGLE_EMAIL_UNVERIFIED);
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail(string.Format(AuthMessages.GOOGLE_ACCOUNT_NOT_FOUND, payload.Email));
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.ACCOUNT_LOCKED);
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        var summary = MapUserSummary(user);

        _logger.LogInformation("Người dùng {FullName} ({Email}) đăng nhập Google thành công với vai trò {Role}.", user.FullName, user.Email, summary.Role);

        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = summary
        }, AuthMessages.GOOGLE_LOGIN_SUCCESS);
    }
}
