using Microsoft.EntityFrameworkCore;
using RWFM.Domain.Entities;
using RWFM.Modules.Auth.DTOs;
using RWFM.Modules.Auth.Interfaces;
using RWFM.Shared.Common;
using RWFM.Shared.Data;
using RWFM.Shared.Security;

namespace RWFM.Modules.Auth.Services;

public class AuthService : IAuthService
{
    private readonly RWFMDbContext _context;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(RWFMDbContext context, JwtTokenService jwtTokenService)
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
            .Include(u => u.Employee)
                .ThenInclude(e => e!.PrimaryStore)
            .Include(u => u.Employee)
                .ThenInclude(e => e!.Position)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (!user.IsActive)
        {
            return ApiResponse<AuthResponseDto>.Fail("Tài khoản người dùng đang bị khóa.");
        }

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, user.Employee);
        var summary = MapUserSummary(user, user.Employee);

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

        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.PrimaryStore)
            .Include(e => e.Position)
            .FirstOrDefaultAsync(e => e.EmployeeCode.ToLower() == request.EmployeeCode.Trim().ToLower() && e.IsActive);

        if (employee == null)
        {
            return ApiResponse<AuthResponseDto>.Fail("Không tìm thấy nhân viên với mã này.");
        }

        if (string.IsNullOrEmpty(employee.PinHash) || !PasswordHasher.Verify(request.PinCode.Trim(), employee.PinHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Mã PIN không chính xác.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var isDispatched = await _context.TemporaryDispatches
            .AnyAsync(d => d.EmployeeId == employee.EmployeeId && d.ToStoreId == request.StoreId 
                        && d.StartDate <= today && d.EndDate >= today && d.Status == "Approved");

        if (employee.PrimaryStoreId != request.StoreId && !isDispatched)
        {
            return ApiResponse<AuthResponseDto>.Fail($"Nhân viên {employee.FullName} không thuộc chi nhánh này và không có lệnh điều động hợp lệ.");
        }

        var user = employee.User ?? new User
        {
            UserId = employee.EmployeeId,
            Username = employee.EmployeeCode,
            Role = employee.Position?.PositionCode ?? "Employee",
            IsActive = true
        };

        var (token, expiresAt) = _jwtTokenService.GenerateToken(user, employee);
        var summary = MapUserSummary(user, employee);

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
            .Include(u => u.Employee)
                .ThenInclude(e => e!.PrimaryStore)
            .Include(u => u.Employee)
                .ThenInclude(e => e!.Position)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return ApiResponse<UserSummaryDto>.Fail("Không tìm thấy thông tin người dùng.");
        }

        return ApiResponse<UserSummaryDto>.Ok(MapUserSummary(user, user.Employee));
    }

    public async Task<ApiResponse<List<UserSummaryDto>>> GetStoreEmployeesAsync(int storeId)
    {
        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.PrimaryStore)
            .Include(e => e.Position)
            .Where(e => e.PrimaryStoreId == storeId && e.IsActive)
            .ToListAsync();

        var result = employees.Select(e => MapUserSummary(e.User ?? new User { UserId = e.UserId ?? 0, Username = e.EmployeeCode, Role = e.Position?.PositionCode ?? "Employee" }, e)).ToList();
        return ApiResponse<List<UserSummaryDto>>.Ok(result);
    }

    private static UserSummaryDto MapUserSummary(User user, Employee? emp)
    {
        return new UserSummaryDto
        {
            UserId = user.UserId,
            EmployeeId = emp?.EmployeeId,
            EmployeeCode = emp?.EmployeeCode ?? user.Username,
            FullName = emp?.FullName ?? user.Username,
            Username = user.Username,
            Email = emp?.Email,
            Phone = emp?.Phone,
            Role = user.Role,
            RoleName = GetRoleDisplayName(user.Role),
            StoreId = emp?.PrimaryStoreId,
            StoreName = emp?.PrimaryStore?.StoreName,
            PositionName = emp?.Position?.PositionName
        };
    }

    private static string GetRoleDisplayName(string role) => role switch
    {
        "StoreManager" => "Cửa hàng trưởng (Store Manager)",
        "ShiftLeader" => "Trưởng ca (Shift Leader)",
        "Employee" => "Nhân viên vận hành (Store Staff)",
        _ => role
    };
}
