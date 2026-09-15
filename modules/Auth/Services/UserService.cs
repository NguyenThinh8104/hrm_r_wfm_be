using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Security;
using Shared.Services;

namespace Modules.Auth.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext context, 
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _logger = logger;
    }

    #region UC 1.4 - Quản lý Tài khoản & Phân quyền Vận hành (Store Managers)

    public async Task<ApiResponse<List<StoreManagerDto>>> GetStoreManagersAsync()
    {
        var storeManagers = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .Where(u => u.Role.RoleCode == "STORE_MANAGER")
            .OrderBy(u => u.HomeBranchId)
            .ThenBy(u => u.FullName)
            .Select(u => new StoreManagerDto
            {
                Id = u.Id,
                EmployeeCode = u.EmployeeCode,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                HomeBranchId = u.HomeBranchId,
                BranchCode = u.HomeBranch != null ? u.HomeBranch.BranchCode : null,
                BranchName = u.HomeBranch != null ? u.HomeBranch.Name : null,
                Status = u.Status,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<StoreManagerDto>>.Ok(storeManagers, "Lấy danh sách Cửa hàng trưởng thành công.");
    }

    public async Task<ApiResponse<StoreManagerDto>> CreateStoreManagerAsync(CreateStoreManagerDto dto, ulong actorId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            return ApiResponse<StoreManagerDto>.Fail("Vui lòng điền đầy đủ các thông tin bắt buộc.");
        }

        var normalizedCode = dto.EmployeeCode.Trim().ToUpper();
        var normalizedEmail = dto.Email.Trim().ToLower();
        var normalizedPhone = dto.Phone.Trim();

        // Kiểm tra trùng lặp
        if (await _context.Users.AnyAsync(u => u.EmployeeCode == normalizedCode))
            return ApiResponse<StoreManagerDto>.Fail($"Mã nhân viên '{normalizedCode}' đã tồn tại trong hệ thống.");
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            return ApiResponse<StoreManagerDto>.Fail($"Email '{normalizedEmail}' đã được sử dụng.");
        if (await _context.Users.AnyAsync(u => u.Phone == normalizedPhone))
            return ApiResponse<StoreManagerDto>.Fail($"Số điện thoại '{normalizedPhone}' đã được sử dụng.");

        var branch = await _context.Branches.FindAsync(dto.HomeBranchId);
        if (branch == null)
            return ApiResponse<StoreManagerDto>.Fail("Chi nhánh chỉ định không tồn tại.");

        var storeManagerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleCode == "STORE_MANAGER");
        if (storeManagerRole == null)
            return ApiResponse<StoreManagerDto>.Fail("Không tìm thấy vai trò STORE_MANAGER trong hệ thống.");

        var plainPassword = string.IsNullOrWhiteSpace(dto.Password) ? "Password@123" : dto.Password.Trim();
        var passwordHash = _passwordHasher.Hash(plainPassword);

        var newUser = new User
        {
            EmployeeCode = normalizedCode,
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            PasswordHash = passwordHash,
            RoleId = storeManagerRole.Id,
            EmploymentType = "FULL_TIME",
            HomeBranchId = dto.HomeBranchId,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // Ghi vết Audit Log
        await LogAuditAsync(actorId, "CREATE_STORE_MANAGER", "users", newUser.Id, null, new
        {
            newUser.EmployeeCode,
            newUser.FullName,
            newUser.Email,
            newUser.HomeBranchId,
            BranchName = branch.Name
        }, ipAddress);

        // Tự động gửi Welcome Email thông báo tài khoản & mật khẩu
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    newUser.Email,
                    newUser.FullName,
                    newUser.EmployeeCode,
                    storeManagerRole.RoleName,
                    branch.Name,
                    plainPassword
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi welcome email cho Cửa hàng trưởng {Email}", newUser.Email);
            }
        });

        return ApiResponse<StoreManagerDto>.Ok(new StoreManagerDto
        {
            Id = newUser.Id,
            EmployeeCode = newUser.EmployeeCode,
            FullName = newUser.FullName,
            Email = newUser.Email,
            Phone = newUser.Phone,
            HomeBranchId = newUser.HomeBranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.Name,
            Status = newUser.Status,
            CreatedAt = newUser.CreatedAt
        }, $"Cấp tài khoản Cửa hàng trưởng thành công! Mật khẩu khởi tạo ({plainPassword}) đã được tự động gửi tới email {newUser.Email}.");
    }

    public async Task<ApiResponse<bool>> ToggleUserStatusAsync(ulong userId, UpdateStatusDto dto, ulong actorId, string? ipAddress)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return ApiResponse<bool>.Fail("Không tìm thấy tài khoản người dùng.");

        if (user.Id == actorId)
            return ApiResponse<bool>.Fail("Không thể tự khóa tài khoản của chính mình.");

        var oldStatus = user.Status;
        var newStatus = string.IsNullOrWhiteSpace(dto.Status) ? (user.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE") : dto.Status.ToUpper();

        user.Status = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "UPDATE_USER_STATUS", "users", user.Id,
            new { Status = oldStatus },
            new { Status = newStatus }, ipAddress);

        return ApiResponse<bool>.Ok(true, $"Cập nhật trạng thái tài khoản '{user.FullName}' thành '{newStatus}' thành công.");
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ulong userId, ResetPasswordDto dto, ulong actorId, string? ipAddress)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return ApiResponse<bool>.Fail("Không tìm thấy người dùng.");

        var newPassword = string.IsNullOrWhiteSpace(dto.NewPassword) ? "Password@123" : dto.NewPassword.Trim();
        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "RESET_PASSWORD", "users", user.Id, null, new { Message = "Đã đặt lại mật khẩu" }, ipAddress);

        return ApiResponse<bool>.Ok(true, $"Đặt lại mật khẩu thành công. Mật khẩu mới là: {newPassword}");
    }

    #endregion

    #region UC 1.5 - Quản lý Hồ sơ & Hợp đồng Nhân sự Toàn chuỗi

    public async Task<ApiResponse<List<EmployeeDetailDto>>> GetEmployeesAsync(EmployeeFilterDto filter, ulong actorId, string actorRole, ulong? actorBranchId)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .AsQueryable();

        // Ràng buộc phân quyền: Nếu là STORE_MANAGER thì chỉ xem nhân viên chi nhánh mình
        var normalizedRole = actorRole.ToUpper();
        if (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
        {
            if (!actorBranchId.HasValue)
                return ApiResponse<List<EmployeeDetailDto>>.Fail("Tài khoản Quản lý chưa được gán chi nhánh.");
            query = query.Where(u => u.HomeBranchId == actorBranchId.Value);
        }
        else if (filter.BranchId.HasValue && filter.BranchId.Value > 0)
        {
            query = query.Where(u => u.HomeBranchId == filter.BranchId.Value);
        }

        if (filter.RoleId.HasValue && filter.RoleId.Value > 0)
        {
            query = query.Where(u => u.RoleId == filter.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
        {
            query = query.Where(u => u.EmploymentType == filter.EmploymentType.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(u => u.Status == filter.Status.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(search)
                                  || u.EmployeeCode.ToLower().Contains(search)
                                  || u.Email.ToLower().Contains(search)
                                  || u.Phone.Contains(search));
        }

        var employees = await query
            .OrderBy(u => u.HomeBranchId)
            .ThenBy(u => u.RoleId)
            .ThenBy(u => u.FullName)
            .Select(u => new EmployeeDetailDto
            {
                Id = u.Id,
                EmployeeCode = u.EmployeeCode,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                RoleCode = u.Role.RoleCode,
                RoleName = u.Role.RoleName,
                EmploymentType = u.EmploymentType,
                HomeBranchId = u.HomeBranchId,
                BranchCode = u.HomeBranch != null ? u.HomeBranch.BranchCode : null,
                BranchName = u.HomeBranch != null ? u.HomeBranch.Name : null,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return ApiResponse<List<EmployeeDetailDto>>.Ok(employees, "Lấy danh sách hồ sơ nhân sự thành công.");
    }

    public async Task<ApiResponse<EmployeeDetailDto>> GetEmployeeByIdAsync(ulong userId, ulong actorId, string actorRole, ulong? actorBranchId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return ApiResponse<EmployeeDetailDto>.Fail("Không tìm thấy hồ sơ nhân sự.");

        var normalizedRole = actorRole.ToUpper();
        if ((normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER") && user.HomeBranchId != actorBranchId)
            return ApiResponse<EmployeeDetailDto>.Fail("Bạn không có quyền truy cập hồ sơ nhân sự của chi nhánh khác.");

        return ApiResponse<EmployeeDetailDto>.Ok(new EmployeeDetailDto
        {
            Id = user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            RoleId = user.RoleId,
            RoleCode = user.Role.RoleCode,
            RoleName = user.Role.RoleName,
            EmploymentType = user.EmploymentType,
            HomeBranchId = user.HomeBranchId,
            BranchCode = user.HomeBranch?.BranchCode,
            BranchName = user.HomeBranch?.Name,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        });
    }

    public async Task<ApiResponse<EmployeeDetailDto>> CreateEmployeeAsync(CreateEmployeeDto dto, ulong actorId, string actorRole, ulong? actorBranchId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode) || string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone))
        {
            return ApiResponse<EmployeeDetailDto>.Fail("Vui lòng điền đầy đủ thông tin bắt buộc (Mã NV, Họ tên, Email, SĐT).");
        }

        var normalizedCode = dto.EmployeeCode.Trim().ToUpper();
        var normalizedEmail = dto.Email.Trim().ToLower();
        var normalizedPhone = dto.Phone.Trim();
        var normalizedActorRole = actorRole.ToUpper();

        // 1. Kiểm tra vai trò chỉ định
        var targetRole = await _context.Roles.FindAsync(dto.RoleId);
        if (targetRole == null)
            return ApiResponse<EmployeeDetailDto>.Fail("Vai trò chỉ định không hợp lệ trong hệ thống.");

        // Danh sách 5 vai trò nhân sự cửa hàng hợp lệ:
        var validStoreRoles = new HashSet<string> { "STORE_MANAGER", "SHIFT_LEADER", "CASHIER", "SALES_STAFF", "SECURITY_GUARD" };
        if (!validStoreRoles.Contains(targetRole.RoleCode))
        {
            return ApiResponse<EmployeeDetailDto>.Fail($"Hệ thống chỉ cho phép khai báo các vai trò nhân sự cửa hàng: STORE_MANAGER, SHIFT_LEADER, CASHIER, SALES_STAFF, SECURITY_GUARD.");
        }

        // Phân quyền tạo vai trò:
        // - Cửa hàng trưởng (STORE_MANAGER) chỉ được tạo 4 vai trò vận hành tại cửa hàng
        if (normalizedActorRole == "STORE_MANAGER" || normalizedActorRole == "STOREMANAGER")
        {
            if (targetRole.RoleCode == "OPERATIONS_ADMIN" || targetRole.RoleCode == "BUSINESS_OWNER" || targetRole.RoleCode == "STORE_MANAGER")
            {
                return ApiResponse<EmployeeDetailDto>.Fail("Cửa hàng trưởng chỉ được tạo tài khoản nhân viên vận hành tại cửa hàng (Trưởng ca, Thu ngân, Bán hàng, Bảo vệ).");
            }
        }
        // - OPERATIONS_ADMIN và BUSINESS_OWNER có toàn quyền tạo cả 5 vai trò (kể cả Cửa hàng trưởng)

        // 2. Gán và kiểm tra Chi nhánh làm việc (bắt buộc cho tất cả 5 vai trò)
        ulong branchIdToAssign = dto.HomeBranchId;
        if (normalizedActorRole == "STORE_MANAGER" || normalizedActorRole == "STOREMANAGER")
        {
            if (!actorBranchId.HasValue || actorBranchId.Value == 0)
                return ApiResponse<EmployeeDetailDto>.Fail("Tài khoản Quản lý chưa được gán chi nhánh công tác.");
            branchIdToAssign = actorBranchId.Value; // Buộc nhân viên mới vào chi nhánh của Store Manager
        }

        if (branchIdToAssign == 0)
        {
            return ApiResponse<EmployeeDetailDto>.Fail("Vui lòng chọn chi nhánh cửa hàng công tác cho nhân viên.");
        }

        var branch = await _context.Branches.FindAsync(branchIdToAssign);
        if (branch == null || branch.Status != "ACTIVE")
        {
            return ApiResponse<EmployeeDetailDto>.Fail("Chi nhánh chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        // 3. Kiểm tra trùng lặp danh tính (Mã NV, Email, SĐT)
        if (await _context.Users.AnyAsync(u => u.EmployeeCode == normalizedCode))
            return ApiResponse<EmployeeDetailDto>.Fail($"Mã nhân viên '{normalizedCode}' đã tồn tại trong hệ thống.");
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            return ApiResponse<EmployeeDetailDto>.Fail($"Email '{normalizedEmail}' đã được sử dụng.");
        if (await _context.Users.AnyAsync(u => u.Phone == normalizedPhone))
            return ApiResponse<EmployeeDetailDto>.Fail($"Số điện thoại '{normalizedPhone}' đã được sử dụng.");

        var plainPassword = string.IsNullOrWhiteSpace(dto.Password) ? "Password@123" : dto.Password.Trim();

        var newUser = new User
        {
            EmployeeCode = normalizedCode,
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            PasswordHash = _passwordHasher.Hash(plainPassword),
            RoleId = dto.RoleId,
            EmploymentType = string.Equals(dto.EmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME",
            HomeBranchId = branchIdToAssign,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // 4. Ghi vết kiểm toán (Audit Log)
        await LogAuditAsync(actorId, "CREATE_EMPLOYEE", "users", newUser.Id, null, new
        {
            newUser.EmployeeCode,
            newUser.FullName,
            Role = targetRole.RoleCode,
            Branch = branch.Name,
            newUser.EmploymentType
        }, ipAddress);

        // 5. Tự động gửi Welcome Email thông báo tài khoản & mật khẩu cho nhân sự
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    newUser.Email,
                    newUser.FullName,
                    newUser.EmployeeCode,
                    targetRole.RoleName,
                    branch.Name,
                    plainPassword
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi welcome email cho nhân viên mới {Email}", newUser.Email);
            }
        });

        return ApiResponse<EmployeeDetailDto>.Ok(new EmployeeDetailDto
        {
            Id = newUser.Id,
            EmployeeCode = newUser.EmployeeCode,
            FullName = newUser.FullName,
            Email = newUser.Email,
            Phone = newUser.Phone,
            RoleId = targetRole.Id,
            RoleCode = targetRole.RoleCode,
            RoleName = targetRole.RoleName,
            EmploymentType = newUser.EmploymentType,
            HomeBranchId = newUser.HomeBranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.Name,
            Status = newUser.Status,
            CreatedAt = newUser.CreatedAt,
            UpdatedAt = newUser.UpdatedAt
        }, $"Khai báo hồ sơ nhân sự thành công! Mật khẩu khởi tạo ({plainPassword}) đã được tự động gửi tới email {newUser.Email}.");
    }

    public async Task<ApiResponse<EmployeeDetailDto>> UpdateEmployeeAsync(ulong userId, UpdateEmployeeDto dto, ulong actorId, string actorRole, ulong? actorBranchId, string? ipAddress)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return ApiResponse<EmployeeDetailDto>.Fail("Không tìm thấy hồ sơ nhân sự.");

        var normalizedActorRole = actorRole.ToUpper();
        if ((normalizedActorRole == "STORE_MANAGER" || normalizedActorRole == "STOREMANAGER") && user.HomeBranchId != actorBranchId)
            return ApiResponse<EmployeeDetailDto>.Fail("Bạn không có quyền chỉnh sửa nhân sự của chi nhánh khác.");

        var targetRole = await _context.Roles.FindAsync(dto.RoleId);
        if (targetRole == null)
            return ApiResponse<EmployeeDetailDto>.Fail("Vai trò chỉ định không tồn tại.");

        if ((normalizedActorRole == "STORE_MANAGER" || normalizedActorRole == "STOREMANAGER") && (targetRole.RoleCode == "OPERATIONS_ADMIN" || targetRole.RoleCode == "BUSINESS_OWNER" || targetRole.RoleCode == "STORE_MANAGER"))
            return ApiResponse<EmployeeDetailDto>.Fail("Không có quyền nâng cấp vai trò này.");

        // Kiểm tra trùng SĐT nếu thay đổi
        var normalizedPhone = dto.Phone.Trim();
        if (normalizedPhone != user.Phone && await _context.Users.AnyAsync(u => u.Phone == normalizedPhone && u.Id != user.Id))
            return ApiResponse<EmployeeDetailDto>.Fail($"Số điện thoại '{normalizedPhone}' đã được sử dụng bởi người khác.");

        var oldValues = new
        {
            user.FullName,
            user.Phone,
            Role = user.Role.RoleCode,
            user.EmploymentType,
            user.HomeBranchId,
            user.Status
        };

        user.FullName = dto.FullName.Trim();
        user.Phone = normalizedPhone;
        user.RoleId = dto.RoleId;
        user.EmploymentType = string.Equals(dto.EmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME";

        if (normalizedActorRole != "STORE_MANAGER" && normalizedActorRole != "STOREMANAGER")
        {
            if (dto.HomeBranchId > 0)
            {
                user.HomeBranchId = dto.HomeBranchId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            user.Status = dto.Status.ToUpper();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "UPDATE_EMPLOYEE", "users", user.Id, oldValues, new
        {
            user.FullName,
            user.Phone,
            Role = targetRole.RoleCode,
            user.EmploymentType,
            user.HomeBranchId,
            user.Status
        }, ipAddress);

        return ApiResponse<EmployeeDetailDto>.Ok(new EmployeeDetailDto
        {
            Id = user.Id,
            EmployeeCode = user.EmployeeCode,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            RoleId = targetRole.Id,
            RoleCode = targetRole.RoleCode,
            RoleName = targetRole.RoleName,
            EmploymentType = user.EmploymentType,
            HomeBranchId = user.HomeBranchId,
            BranchCode = user.HomeBranch?.BranchCode,
            BranchName = user.HomeBranch?.Name,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        }, "Cập nhật thông tin hồ sơ nhân sự thành công.");
    }

    #endregion

    #region Metadata & Helper Methods

    public async Task<ApiResponse<List<RoleDto>>> GetRolesAsync()
    {
        var roles = await _context.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                RoleCode = r.RoleCode,
                RoleName = r.RoleName,
                Description = r.Description
            })
            .ToListAsync();

        return ApiResponse<List<RoleDto>>.Ok(roles);
    }

    public async Task<ApiResponse<List<BranchSimpleDto>>> GetBranchesAsync()
    {
        var branches = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.Id)
            .Select(b => new BranchSimpleDto
            {
                Id = b.Id,
                BranchCode = b.BranchCode,
                Code = b.BranchCode,
                Name = b.Name,
                Address = b.Address,
                Status = b.Status
            })
            .ToListAsync();

        return ApiResponse<List<BranchSimpleDto>>.Ok(branches);
    }

    private async Task LogAuditAsync(ulong actorId, string action, string targetTable, ulong targetId, object? oldValues, object? newValues, string? ipAddress)
    {
        try
        {
            var audit = new SystemAuditLog
            {
                ActorId = actorId,
                Action = action,
                TargetTable = targetTable,
                TargetId = targetId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.SystemAuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Tránh crash nếu audit logging gặp lỗi tạm thời
        }
    }

    #endregion
}
