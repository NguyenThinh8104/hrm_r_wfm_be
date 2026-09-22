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

        var normalizedInput = request.Username.Trim();
        var normalizedUsername = normalizedInput.ToLower();

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedUsername 
                                   || u.EmployeeCode.ToLower() == normalizedUsername
                                   || u.Phone == normalizedInput);

        if (user == null)
        {
            _logger.LogWarning("Đăng nhập thất bại: Không tìm thấy tài khoản với Username '{Username}'", request.Username);
            return ApiResponse<AuthResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        var rawPassword = request.Password ?? string.Empty;
        var trimmedPassword = rawPassword.Trim();

        bool isPasswordValid = PasswordHasher.Verify(rawPassword, user.PasswordHash)
                            || PasswordHasher.Verify(trimmedPassword, user.PasswordHash);

        if (!isPasswordValid)
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

        string? email = null;
        bool emailVerified = false;
        var tokenStr = request.IdToken.Trim();

        // 1. Thử xác thực nếu là JWT ID Token (3 phần phân tách bởi dấu '.')
        if (tokenStr.Count(c => c == '.') == 2)
        {
            try
            {
                var clientId = _config["Google:ClientId"];
                var settings = new GoogleJsonWebSignature.ValidationSettings();
                if (!string.IsNullOrEmpty(clientId))
                {
                    settings.Audience = new[] { clientId };
                }

                var payload = await GoogleJsonWebSignature.ValidateAsync(tokenStr, settings);
                if (payload != null && !string.IsNullOrWhiteSpace(payload.Email))
                {
                    email = payload.Email;
                    emailVerified = payload.EmailVerified;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Xác thực qua JWT ID Token không thành công, thử qua Google UserInfo endpoint: {Message}", ex.Message);
            }
        }

        // 2. Nếu không phải JWT hoặc giải mã JWT thất bại, gọi Google UserInfo API với access_token
        if (string.IsNullOrWhiteSpace(email))
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenStr);
                var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("email", out var emailProp))
                    {
                        email = emailProp.GetString();
                    }
                    if (doc.RootElement.TryGetProperty("email_verified", out var evProp))
                    {
                        emailVerified = evProp.GetBoolean();
                    }
                    else
                    {
                        emailVerified = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi Google UserInfo API với access_token");
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.GOOGLE_TOKEN_INVALID);
        }

        if (!emailVerified)
        {
            return ApiResponse<AuthResponseDto>.Fail(AuthMessages.GOOGLE_EMAIL_UNVERIFIED);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            return ApiResponse<AuthResponseDto>.Fail(string.Format(AuthMessages.GOOGLE_ACCOUNT_NOT_FOUND, email));
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

    public async Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiResponse<bool>.Fail("Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới.");
        }

        if (request.NewPassword.Length < 6)
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse<bool>.Fail("Mật khẩu xác nhận không trùng khớp.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == (ulong)userId);
        if (user == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin tài khoản người dùng.");
        }

        bool isCurrentValid = PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash)
                           || PasswordHasher.Verify(request.CurrentPassword.Trim(), user.PasswordHash);
        if (!isCurrentValid)
        {
            return ApiResponse<bool>.Fail("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword.Trim());
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Người dùng {UserId} đổi mật khẩu thành công.", userId);
        return ApiResponse<bool>.Ok(true, "Đổi mật khẩu thành công.");
    }

    public async Task<ApiResponse<UserSummaryDto>> UpdateProfileAsync(int userId, UpdateProfileDto request)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId);

        if (user == null)
        {
            return ApiResponse<UserSummaryDto>.Fail("Không tìm thấy thông tin tài khoản người dùng.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            user.Phone = request.Phone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailLower = request.Email.Trim().ToLowerInvariant();
            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower && u.Id != (ulong)userId);
            if (emailExists)
            {
                return ApiResponse<UserSummaryDto>.Fail("Email này đã được sử dụng bởi một tài khoản khác.");
            }
            user.Email = emailLower;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var summary = MapUserSummary(user);
        return ApiResponse<UserSummaryDto>.Ok(summary, "Cập nhật thông tin hồ sơ cá nhân thành công.");
    }

    public async Task<ApiResponse<List<NotificationItemDto>>> GetNotificationsAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)userId);

        if (user == null)
        {
            return ApiResponse<List<NotificationItemDto>>.Ok(new List<NotificationItemDto>());
        }

        var list = new List<NotificationItemDto>();
        var roleCode = user.Role?.RoleCode ?? "";
        var isManager = roleCode == "STORE_MANAGER" || roleCode == "OPERATIONS_ADMIN" || roleCode == "BUSINESS_OWNER";

        if (isManager && user.HomeBranchId.HasValue)
        {
            var pendingSwaps = await _context.ShiftSwapRequests
                .Include(s => s.RequesterUser)
                .Include(s => s.Schedule).ThenInclude(sc => sc.ShiftTemplate)
                .Where(s => s.Schedule != null && s.Schedule.BranchId == user.HomeBranchId.Value && s.Status == "PENDING")
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var ps in pendingSwaps)
            {
                list.Add(new NotificationItemDto
                {
                    Id = $"swap-{ps.Id}",
                    Title = ps.RequestType == "LEAVE" ? "Đơn xin nghỉ ca mới" : "Đơn xin đổi ca mới",
                    Message = $"{ps.RequesterUser?.FullName ?? "Nhân viên"} đã gửi đơn yêu cầu cho ca {ps.Schedule?.ShiftTemplate?.Name ?? "ca trực"} ({ps.Schedule?.WorkDate:dd/MM/yyyy}).",
                    Type = "SHIFT_SWAP",
                    CreatedAt = ps.CreatedAt,
                    IsRead = false,
                    Link = "/employee/shift-requests"
                });
            }
        }
        else
        {
            var mySwaps = await _context.ShiftSwapRequests
                .Include(s => s.Schedule).ThenInclude(sc => sc.ShiftTemplate)
                .Include(s => s.ReviewedByUser)
                .Where(s => (s.RequesterUserId == user.Id || s.TargetUserId == user.Id) && s.Status != "PENDING")
                .OrderByDescending(s => s.ReviewedAt ?? s.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var ms in mySwaps)
            {
                var isApproved = ms.Status == "APPROVED";
                list.Add(new NotificationItemDto
                {
                    Id = $"swap-{ms.Id}",
                    Title = isApproved ? "Đơn đổi ca đã được phê duyệt" : "Đơn đổi ca bị từ chối",
                    Message = $"Đơn xin điều chỉnh ca {ms.Schedule?.ShiftTemplate?.Name} ({ms.Schedule?.WorkDate:dd/MM/yyyy}) đã được {(isApproved ? "phê duyệt" : "từ chối")}.",
                    Type = "SHIFT_SWAP",
                    CreatedAt = ms.ReviewedAt ?? ms.CreatedAt,
                    IsRead = false,
                    Link = "/employee/shift-requests"
                });
            }
        }

        // Add a general welcome notification if list is empty
        if (list.Count == 0)
        {
            list.Add(new NotificationItemDto
            {
                Id = "sys-welcome",
                Title = "Chào mừng bạn đến với hệ thống RWFM",
                Message = "Tất cả thông báo thay đổi lịch phân ca, đổi ca và điều động sẽ hiển thị tại đây.",
                Type = "INFO",
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Link = "/employee/my-calendar"
            });
        }

        list = list.OrderByDescending(n => n.CreatedAt).ToList();
        return ApiResponse<List<NotificationItemDto>>.Ok(list);
    }
}

