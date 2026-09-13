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

namespace Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;

    private const int OTP_EXPIRY_MINUTES = 5;

public AuthService(AppDbContext context, JwtTokenService jwtTokenService, IEmailService emailService, IMemoryCache cache, ILogger<AuthService> logger)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResponse<AuthResponseDto>.Fail("Vui lòng nhập tên đăng nhập và mật khẩu.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Username.Trim().ToLower() 
                                   || u.EmployeeCode.ToLower() == request.Username.Trim().ToLower());

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<AuthResponseDto>.Fail("Tài khoản người dùng đang bị khóa.");
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        var summary = MapUserSummary(user);

        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = summary
        }, "Đăng nhập thành công.");
    }

    public async Task<ApiResponse<AuthResponseDto>> KioskLoginAsync(KioskLoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || string.IsNullOrWhiteSpace(request.PinCode))
        {
            return ApiResponse<AuthResponseDto>.Fail("Vui lòng nhập mã nhân viên và mã PIN.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.EmployeeCode.ToLower() == request.EmployeeCode.Trim().ToLower() && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Không tìm thấy nhân viên với mã này.");
        }

        if (string.IsNullOrEmpty(user.KioskPinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), user.KioskPinHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Mã PIN không chính xác.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var isDispatched = await _context.TemporaryDispatches
            .AnyAsync(d => d.UserId == user.Id && d.TargetBranchId == (ulong)request.StoreId 
                        && d.StartDate <= today && d.EndDate >= today && d.Status == "APPROVED");

        if (user.HomeBranchId != (ulong)request.StoreId && !isDispatched)
        {
            return ApiResponse<AuthResponseDto>.Fail($"Nhân viên {user.FullName} không thuộc chi nhánh này và không có lệnh điều động hợp lệ.");
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user);
        var summary = MapUserSummary(user);

        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = summary
        }, "Xác thực Kiosk thành công.");
    }

    public async Task<ApiResponse<UserSummaryDto>> GetCurrentUserAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId);

        if (user == null)
        {
            return ApiResponse<UserSummaryDto>.Fail("Không tìm thấy thông tin người dùng.");
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
            return ApiResponse<bool>.Fail("Email này không tồn tại trong hệ thống nhân viên.");
        }

        if (user.Status != "ACTIVE")
        {
            return ApiResponse<bool>.Fail("Tài khoản người dùng hiện đang bị khóa hoặc ngừng hoạt động.");
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
            return ApiResponse<bool>.Fail("Hệ thống tạm thời không thể gửi email. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau ít phút.");
        }

        return ApiResponse<bool>.Ok(true, $"Mã xác thực OTP đã được gửi đến hòm thư {user.Email}. Mã có hiệu lực trong 5 phút.");
    }

    public async Task<ApiResponse<bool>> VerifyOtpAsync(VerifyOtpRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var cacheKey = $"RESET_OTP_{email}";

        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || string.IsNullOrEmpty(cachedOtp))
        {
            return ApiResponse<bool>.Fail("Mã OTP đã hết hạn (quá 5 phút) hoặc chưa được gửi yêu cầu. Vui lòng nhấn gửi lại mã mới.");
        }

        if (cachedOtp != request.OtpCode.Trim())
        {
            return ApiResponse<bool>.Fail("Mã OTP không chính xác. Vui lòng kiểm tra lại hòm thư email.");
        }

        // Kiểm tra User trong database
        var userExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email && u.Status == "ACTIVE");
        if (!userExists)
        {
            return ApiResponse<bool>.Fail("Tài khoản người dùng không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        return ApiResponse<bool>.Ok(true, "Xác thực mã OTP thành công. Bạn có thể đặt lại mật khẩu mới.");
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var cacheKey = $"RESET_OTP_{email}";

        if (!_cache.TryGetValue(cacheKey, out string? cachedOtp) || string.IsNullOrEmpty(cachedOtp))
        {
            return ApiResponse<bool>.Fail("Mã OTP đã hết hạn (quá 5 phút) hoặc không tồn tại. Vui lòng thao tác lại.");
        }

        if (cachedOtp != request.OtpCode.Trim())
        {
            return ApiResponse<bool>.Fail("Mã OTP không chính xác. Vui lòng kiểm tra lại.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin tài khoản nhân viên.");
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Xóa mã OTP khỏi Cache ngay lập tức để chống tái sử dụng
        _cache.Remove(cacheKey);

        _logger.LogInformation("Nhân viên {EmployeeCode} ({Email}) đã đặt lại mật khẩu thành công.", user.EmployeeCode, user.Email);

        return ApiResponse<bool>.Ok(true, "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.");
    }

    public Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId)
    {
        throw new NotImplementedException();
    }
}
