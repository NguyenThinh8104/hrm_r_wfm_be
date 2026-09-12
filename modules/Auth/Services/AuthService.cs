using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Security;

namespace Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(AppDbContext context, JwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
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

    public async Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId)
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .Where(u => u.HomeBranchId == (ulong)storeId && u.Status == "ACTIVE")
            .ToListAsync();

        var result = users.Select(MapUserSummary).ToList();
        return ApiResponse<List<UserSummaryDto>>.Ok(result);
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
}
