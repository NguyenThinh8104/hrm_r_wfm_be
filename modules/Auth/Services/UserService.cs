using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;
using Shared.Security;
using Shared.Services;

namespace Modules.Auth.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IBranchHeadcountService _headcountService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext context, 
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IBranchHeadcountService headcountService,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _headcountService = headcountService;
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
        bool emailSent = false;
        try
        {
            emailSent = await _emailService.SendWelcomeEmailAsync(
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

        var successMessage = emailSent
            ? $"Tạo Cửa hàng trưởng thành công. Email chào mừng kèm thông tin tài khoản và link đăng nhập đã được gửi tới {newUser.Email}."
            : $"Tạo Cửa hàng trưởng thành công. Tuy nhiên gửi email thông báo thất bại, vui lòng kiểm tra hộp thư hoặc cấu hình SMTP.";

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
        }, successMessage);
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

        // Ràng buộc phân quyền: Nếu là STORE_MANAGER thì chỉ xem nhân viên chi nhánh mình và loại trừ Cửa hàng trưởng
        var normalizedRole = actorRole.ToUpper();
        if (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
        {
            if (!actorBranchId.HasValue)
                return ApiResponse<List<EmployeeDetailDto>>.Fail("Tài khoản Quản lý chưa được gán chi nhánh.");
            query = query.Where(u => u.HomeBranchId == actorBranchId.Value);

            // Tự động loại trừ Cửa hàng trưởng khỏi danh sách nhân viên vận hành
            query = query.Where(u => u.Role.RoleCode != "STORE_MANAGER");
        }
        else if (filter.BranchId.HasValue && filter.BranchId.Value > 0)
        {
            query = query.Where(u => u.HomeBranchId == filter.BranchId.Value);
        }

        if (filter.ExcludeStoreManager == true)
        {
            query = query.Where(u => u.Role.RoleCode != "STORE_MANAGER");
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

        // 1. Phân quyền RBAC nghiêm ngặt: Chỉ duy nhất OPERATIONS_ADMIN (hoặc ADMIN hệ thống) mới có quyền tạo nhân sự
        if (normalizedActorRole != "OPERATIONS_ADMIN" && normalizedActorRole != "OPERATIONSADMIN" && normalizedActorRole != "ADMIN")
        {
            return ApiResponse<EmployeeDetailDto>.Fail(
                "Chỉ duy nhất Quản trị viên vận hành (OPERATIONS_ADMIN) mới có quyền tạo mới hồ sơ nhân sự. " +
                "Cửa hàng trưởng không được tạo nhân viên trực tiếp; vui lòng gửi file Excel đề xuất mở rộng định biên nếu chi nhánh có nhu cầu bổ sung nhân sự.");
        }

        // 2. Kiểm tra vai trò chỉ định
        var targetRole = await _context.Roles.FindAsync(dto.RoleId);
        if (targetRole == null)
            return ApiResponse<EmployeeDetailDto>.Fail("Vai trò chỉ định không hợp lệ trong hệ thống.");

        // Danh sách 5 vai trò nhân sự cửa hàng hợp lệ:
        var validStoreRoles = new HashSet<string> { "STORE_MANAGER", "SHIFT_LEADER", "CASHIER", "SALES_STAFF", "SECURITY_GUARD" };
        if (!validStoreRoles.Contains(targetRole.RoleCode))
        {
            return ApiResponse<EmployeeDetailDto>.Fail($"Hệ thống chỉ cho phép khai báo các vai trò nhân sự cửa hàng: STORE_MANAGER, SHIFT_LEADER, CASHIER, SALES_STAFF, SECURITY_GUARD.");
        }

        // 3. Gán và kiểm tra Chi nhánh làm việc (bắt buộc cho tất cả 5 vai trò)
        ulong branchIdToAssign = dto.HomeBranchId;
        if (branchIdToAssign == 0)
        {
            return ApiResponse<EmployeeDetailDto>.Fail("Vui lòng chọn chi nhánh cửa hàng công tác cho nhân viên.");
        }

        var branch = await _context.Branches.FindAsync(branchIdToAssign);
        if (branch == null || branch.Status != "ACTIVE")
        {
            return ApiResponse<EmployeeDetailDto>.Fail("Chi nhánh chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        // 4. Thẩm định định biên nhân sự theo BranchTier & trừ lùi chỉ tiêu từ đơn mở rộng nếu vượt Quota
        var quotaResult = await _headcountService.ValidateAndConsumeQuotaAsync(
            branchIdToAssign,
            dto.ImportRequestId,
            dto.ExpansionReason,
            actorId);

        if (!quotaResult.IsSuccess)
        {
            return ApiResponse<EmployeeDetailDto>.Fail(quotaResult.ErrorMessage ?? "Không đạt điều kiện định biên nhân sự của chi nhánh.");
        }

        // 5. Kiểm tra trùng lặp danh tính (Mã NV, Email, SĐT)
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

        // 6. Ghi vết kiểm toán (Audit Log)
        await LogAuditAsync(actorId, "CREATE_EMPLOYEE", "users", newUser.Id, null, new
        {
            newUser.EmployeeCode,
            newUser.FullName,
            Role = targetRole.RoleCode,
            Branch = branch.Name,
            newUser.EmploymentType,
            IsHeadcountOverride = quotaResult.IsOverride,
            ImportRequestId = quotaResult.ImportRequestId,
            ExpansionReason = dto.ExpansionReason,
            BranchQuota = quotaResult.Quota,
            CurrentHeadcount = quotaResult.CurrentCount
        }, ipAddress);

        // 5. Tự động gửi Welcome Email thông báo tài khoản & mật khẩu cho nhân sự
        bool emailSent = false;
        try
        {
            emailSent = await _emailService.SendWelcomeEmailAsync(
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

        var successMessage = emailSent
            ? $"Khai báo hồ sơ nhân sự thành công. Email chào mừng kèm thông tin tài khoản và liên kết đăng nhập đã được gửi đến {newUser.Email}."
            : $"Khai báo hồ sơ nhân sự thành công. Tuy nhiên gửi email thông báo thất bại, vui lòng kiểm tra hộp thư hoặc cấu hình SMTP.";

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
        }, successMessage);
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

    #endregion

    #region UC 1.5 - Bổ Sung: Import Nhân Sự Hàng Loạt Bằng File Excel

    public async Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateEmployeeImportTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Danh_Sach_Nhan_Su");

        var headers = new[]
        {
            "STT",
            "Mã Nhân Viên (*)",
            "Họ Và Tên (*)",
            "Email (*)",
            "Số Điện Thoại (*)",
            "Mã Vai Trò (*)",
            "Hình Thức (FULL_TIME / PART_TIME)",
            "Mã Chi Nhánh (*)",
            "Mật Khẩu Khởi Tạo"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Dữ liệu mẫu minh họa
        worksheet.Cell(2, 1).Value = 1;
        worksheet.Cell(2, 2).Value = "NV101";
        worksheet.Cell(2, 3).Value = "Nguyễn Văn An";
        worksheet.Cell(2, 4).Value = "an.nguyen@example.com";
        worksheet.Cell(2, 5).Value = "0901234567";
        worksheet.Cell(2, 6).Value = "CASHIER";
        worksheet.Cell(2, 7).Value = "FULL_TIME";
        worksheet.Cell(2, 8).Value = "CH01";
        worksheet.Cell(2, 9).Value = "Password@123";

        worksheet.Cell(3, 1).Value = 2;
        worksheet.Cell(3, 2).Value = "NV102";
        worksheet.Cell(3, 3).Value = "Trần Thị Bình";
        worksheet.Cell(3, 4).Value = "binh.tran@example.com";
        worksheet.Cell(3, 5).Value = "0908765432";
        worksheet.Cell(3, 6).Value = "SALES_STAFF";
        worksheet.Cell(3, 7).Value = "PART_TIME";
        worksheet.Cell(3, 8).Value = "CH01";
        worksheet.Cell(3, 9).Value = "Password@123";

        worksheet.Columns().AdjustToContents();

        // Sheet 2: Danh mục & Hướng dẫn
        var guideSheet = workbook.Worksheets.Add("Huong_Dan_Va_Danh_Muc");
        guideSheet.Cell(1, 1).Value = "DANH MỤC VAI TRÒ CỬA HÀNG HỢP LỆ";
        guideSheet.Cell(1, 1).Style.Font.Bold = true;
        guideSheet.Cell(2, 1).Value = "Mã Vai Trò";
        guideSheet.Cell(2, 2).Value = "Tên Chức Danh";
        guideSheet.Range(2, 1, 2, 2).Style.Font.Bold = true;
        guideSheet.Range(2, 1, 2, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var rolesData = new[]
        {
            ("STORE_MANAGER", "Cửa hàng trưởng"),
            ("SHIFT_LEADER", "Trưởng ca vận hành"),
            ("CASHIER", "Nhân viên thu ngân"),
            ("SALES_STAFF", "Nhân viên bán hàng / Quầy kệ"),
            ("SECURITY_GUARD", "Nhân viên an ninh / Bảo vệ")
        };

        for (int r = 0; r < rolesData.Length; r++)
        {
            guideSheet.Cell(3 + r, 1).Value = rolesData[r].Item1;
            guideSheet.Cell(3 + r, 2).Value = rolesData[r].Item2;
        }

        // Lấy danh sách chi nhánh hiện có
        var activeBranches = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.BranchCode)
            .ToListAsync();

        int startBranchRow = 10;
        guideSheet.Cell(startBranchRow, 1).Value = "DANH SÁCH MÃ CHI NHÁNH HOẠT ĐỘNG (DÙNG ĐIỀN CỘT MÃ CHI NHÁNH)";
        guideSheet.Cell(startBranchRow, 1).Style.Font.Bold = true;
        guideSheet.Cell(startBranchRow + 1, 1).Value = "Mã Chi Nhánh (BranchCode)";
        guideSheet.Cell(startBranchRow + 1, 2).Value = "Tên Cửa Hàng";
        guideSheet.Cell(startBranchRow + 1, 3).Value = "Quy Mô (Tier)";
        guideSheet.Range(startBranchRow + 1, 1, startBranchRow + 1, 3).Style.Font.Bold = true;
        guideSheet.Range(startBranchRow + 1, 1, startBranchRow + 1, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        for (int b = 0; b < activeBranches.Count; b++)
        {
            guideSheet.Cell(startBranchRow + 2 + b, 1).Value = activeBranches[b].BranchCode;
            guideSheet.Cell(startBranchRow + 2 + b, 2).Value = activeBranches[b].Name;
            guideSheet.Cell(startBranchRow + 2 + b, 3).Value = activeBranches[b].BranchTier.ToString();
        }

        guideSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Import_Nhan_Su.xlsx");
    }

    public async Task<ApiResponse<BulkImportResultDto>> BulkImportEmployeesAsync(
        BulkImportEmployeeRequestDto dto,
        ulong actorId,
        string actorRole,
        string? ipAddress)
    {
        var normalizedActorRole = actorRole.ToUpper();
        if (normalizedActorRole != "OPERATIONS_ADMIN" && normalizedActorRole != "OPERATIONSADMIN" && normalizedActorRole != "ADMIN")
        {
            return ApiResponse<BulkImportResultDto>.Fail(
                "Chỉ duy nhất Quản trị viên vận hành (OPERATIONS_ADMIN / ADMIN) mới có quyền thực hiện import nhân sự hàng loạt.");
        }

        if (dto.File == null || dto.File.Length == 0)
        {
            return ApiResponse<BulkImportResultDto>.Fail("Vui lòng đính kèm tệp tin danh sách nhân sự (.xlsx / .xls / .csv).");
        }

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls" && ext != ".csv")
        {
            return ApiResponse<BulkImportResultDto>.Fail("Định dạng tệp tin không hợp lệ. Chỉ chấp nhận định dạng .xlsx, .xls hoặc .csv.");
        }

        // 1. Phân tích các dòng từ file
        var parsedRows = new List<ParsedEmployeeRow>();
        try
        {
            if (ext == ".csv")
            {
                using var reader = new StreamReader(dto.File.OpenReadStream());
                int lineIndex = 0;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineIndex++;
                    if (lineIndex == 1) continue; // Bỏ qua tiêu đề
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;

                    parsedRows.Add(new ParsedEmployeeRow
                    {
                        RowNumber = lineIndex,
                        EmployeeCode = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                        FullName = parts.Length > 2 ? parts[2].Trim() : string.Empty,
                        Email = parts.Length > 3 ? parts[3].Trim() : string.Empty,
                        Phone = parts.Length > 4 ? parts[4].Trim() : string.Empty,
                        RoleCode = parts.Length > 5 ? parts[5].Trim() : string.Empty,
                        EmploymentType = parts.Length > 6 ? parts[6].Trim() : "FULL_TIME",
                        BranchIdentifier = parts.Length > 7 ? parts[7].Trim() : string.Empty,
                        Password = parts.Length > 8 && !string.IsNullOrWhiteSpace(parts[8]) ? parts[8].Trim() : "Password@123"
                    });
                }
            }
            else
            {
                using var stream = dto.File.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    return ApiResponse<BulkImportResultDto>.Fail("Tệp tin Excel không chứa trang tính (worksheet) nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1); // Bỏ qua tiêu đề
                int rowNum = 1;
                foreach (var r in rows)
                {
                    rowNum++;
                    var code = r.Cell(2).GetString()?.Trim();
                    var name = r.Cell(3).GetString()?.Trim();
                    var email = r.Cell(4).GetString()?.Trim();
                    var phone = r.Cell(5).GetString()?.Trim();
                    var role = r.Cell(6).GetString()?.Trim();
                    var type = r.Cell(7).GetString()?.Trim();
                    var branch = r.Cell(8).GetString()?.Trim();
                    var pass = r.Cell(9).GetString()?.Trim();

                    // Nếu cả hàng đều trống thì bỏ qua
                    if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name) &&
                        string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
                    {
                        continue;
                    }

                    parsedRows.Add(new ParsedEmployeeRow
                    {
                        RowNumber = rowNum,
                        EmployeeCode = code ?? string.Empty,
                        FullName = name ?? string.Empty,
                        Email = email ?? string.Empty,
                        Phone = phone ?? string.Empty,
                        RoleCode = role ?? string.Empty,
                        EmploymentType = string.IsNullOrWhiteSpace(type) ? "FULL_TIME" : type,
                        BranchIdentifier = branch ?? string.Empty,
                        Password = string.IsNullOrWhiteSpace(pass) ? "Password@123" : pass
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đọc file import nhân sự");
            return ApiResponse<BulkImportResultDto>.Fail($"Không thể đọc nội dung tệp tin: {ex.Message}");
        }

        if (parsedRows.Count == 0)
        {
            return ApiResponse<BulkImportResultDto>.Fail("Không tìm thấy dữ liệu nhân sự nào trong tệp tin.");
        }

        var result = new BulkImportResultDto
        {
            TotalRows = parsedRows.Count
        };

        // 2. Pre-fetch danh mục
        var existingUsers = await _context.Users.AsNoTracking().ToListAsync();
        var existingCodes = new HashSet<string>(existingUsers.Select(u => u.EmployeeCode.ToUpper()), StringComparer.OrdinalIgnoreCase);
        var existingEmails = new HashSet<string>(existingUsers.Select(u => u.Email.ToLower()), StringComparer.OrdinalIgnoreCase);
        var existingPhones = new HashSet<string>(existingUsers.Select(u => u.Phone), StringComparer.OrdinalIgnoreCase);

        var roles = await _context.Roles.AsNoTracking().ToListAsync();
        var rolesByCode = roles.ToDictionary(r => r.RoleCode.ToUpper(), r => r, StringComparer.OrdinalIgnoreCase);

        var branches = await _context.Branches.AsNoTracking().ToListAsync();
        var branchesByCode = branches.ToDictionary(b => b.BranchCode.ToUpper(), b => b, StringComparer.OrdinalIgnoreCase);
        var branchesById = branches.ToDictionary(b => b.Id, b => b);

        var validStoreRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STORE_MANAGER", "SHIFT_LEADER", "CASHIER", "SALES_STAFF", "SECURITY_GUARD"
        };

        var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmailsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhonesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validRowsToProcess = new List<(ParsedEmployeeRow Row, Role Role, Branch Branch)>();

        // 3. Validation từng dòng
        foreach (var r in parsedRows)
        {
            if (string.IsNullOrWhiteSpace(r.EmployeeCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, FullName = r.FullName, ErrorMessage = "Mã nhân viên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.FullName))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, ErrorMessage = "Họ và tên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.Email))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, ErrorMessage = "Email không được để trống." });
                continue;
            }

            if (!r.Email.Contains("@") || !r.Email.Contains("."))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, ErrorMessage = $"Email '{r.Email}' không đúng định dạng." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.Phone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, ErrorMessage = "Số điện thoại không được để trống." });
                continue;
            }

            var normalizedCode = r.EmployeeCode.Trim().ToUpper();
            var normalizedEmail = r.Email.Trim().ToLower();
            var normalizedPhone = r.Phone.Trim();

            // Kiểm tra trùng lặp trong tệp tin
            if (seenCodesInFile.Contains(normalizedCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Mã nhân viên '{normalizedCode}' bị trùng lặp trong tệp tin." });
                continue;
            }
            if (seenEmailsInFile.Contains(normalizedEmail))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Email '{normalizedEmail}' bị trùng lặp trong tệp tin." });
                continue;
            }
            if (seenPhonesInFile.Contains(normalizedPhone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Số điện thoại '{normalizedPhone}' bị trùng lặp trong tệp tin." });
                continue;
            }

            // Kiểm tra trùng lặp với CSDL
            if (existingCodes.Contains(normalizedCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Mã nhân viên '{normalizedCode}' đã tồn tại trong hệ thống." });
                continue;
            }
            if (existingEmails.Contains(normalizedEmail))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Email '{normalizedEmail}' đã được sử dụng trong hệ thống." });
                continue;
            }
            if (existingPhones.Contains(normalizedPhone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Số điện thoại '{normalizedPhone}' đã được sử dụng trong hệ thống." });
                continue;
            }

            // Kiểm tra Vai trò
            var normalizedRoleCode = r.RoleCode.Trim().ToUpper();
            if (!validStoreRoles.Contains(normalizedRoleCode) || !rolesByCode.TryGetValue(normalizedRoleCode, out var targetRole))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Vai trò '{r.RoleCode}' không hợp lệ. Chỉ chấp nhận: STORE_MANAGER, SHIFT_LEADER, CASHIER, SALES_STAFF, SECURITY_GUARD." });
                continue;
            }

            // Xác định Chi nhánh
            Branch? targetBranch = null;
            var branchStr = r.BranchIdentifier.Trim();
            if (!string.IsNullOrWhiteSpace(branchStr))
            {
                if (branchesByCode.TryGetValue(branchStr.ToUpper(), out var bByCode))
                {
                    targetBranch = bByCode;
                }
                else if (ulong.TryParse(branchStr, out var bId) && branchesById.TryGetValue(bId, out var bById))
                {
                    targetBranch = bById;
                }
            }
            else if (dto.DefaultBranchId.HasValue && branchesById.TryGetValue(dto.DefaultBranchId.Value, out var defaultB))
            {
                targetBranch = defaultB;
            }

            if (targetBranch == null)
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Không tìm thấy chi nhánh '{r.BranchIdentifier}'." });
                continue;
            }

            if (targetBranch.Status != "ACTIVE")
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, ErrorMessage = $"Chi nhánh '{targetBranch.Name}' đã ngừng hoạt động." });
                continue;
            }

            seenCodesInFile.Add(normalizedCode);
            seenEmailsInFile.Add(normalizedEmail);
            seenPhonesInFile.Add(normalizedPhone);

            validRowsToProcess.Add((r, targetRole, targetBranch));
        }

        // 4. Thẩm định định biên (Headcount Quota) và tạo nhân sự cho các dòng hợp lệ
        var newUsersToInsert = new List<User>();

        foreach (var (row, role, branch) in validRowsToProcess)
        {
            var quotaCheck = await _headcountService.ValidateAndConsumeQuotaAsync(
                branch.Id,
                dto.ImportRequestId,
                dto.ExpansionReason,
                actorId);

            if (!quotaCheck.IsSuccess)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    EmployeeCode = row.EmployeeCode,
                    FullName = row.FullName,
                    ErrorMessage = quotaCheck.ErrorMessage ?? $"Chi nhánh '{branch.Name}' đã vượt định biên và không có đơn mở rộng hợp lệ."
                });
                continue;
            }

            var normalizedCode = row.EmployeeCode.Trim().ToUpper();
            var normalizedEmail = row.Email.Trim().ToLower();
            var normalizedPhone = row.Phone.Trim();
            var plainPass = string.IsNullOrWhiteSpace(row.Password) ? "Password@123" : row.Password.Trim();

            var user = new User
            {
                EmployeeCode = normalizedCode,
                FullName = row.FullName.Trim(),
                Email = normalizedEmail,
                Phone = normalizedPhone,
                PasswordHash = _passwordHasher.Hash(plainPass),
                RoleId = role.Id,
                EmploymentType = string.Equals(row.EmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME",
                HomeBranchId = branch.Id,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            newUsersToInsert.Add(user);

            result.SuccessEmployees.Add(new EmployeeDetailDto
            {
                Id = user.Id,
                EmployeeCode = user.EmployeeCode,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = role.Id,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName,
                EmploymentType = user.EmploymentType,
                HomeBranchId = branch.Id,
                BranchCode = branch.BranchCode,
                BranchName = branch.Name,
                Status = user.Status,
                CreatedAt = user.CreatedAt
            });

            // Thêm vào existing list để ngăn ngừa dòng phía sau trùng nếu có
            existingCodes.Add(normalizedCode);
            existingEmails.Add(normalizedEmail);
            existingPhones.Add(normalizedPhone);
        }

        // 5. Lưu vào CSDL
        if (newUsersToInsert.Count > 0)
        {
            _context.Users.AddRange(newUsersToInsert);
            await _context.SaveChangesAsync();

            await LogAuditAsync(actorId, "BULK_IMPORT_EMPLOYEES", "users", 0, null, new
            {
                TotalImported = newUsersToInsert.Count,
                dto.ImportRequestId,
                dto.ExpansionReason
            }, ipAddress);
        }

        result.SuccessCount = newUsersToInsert.Count;
        result.FailureCount = result.Errors.Count;

        var message = $"Import nhân sự hoàn tất: {result.SuccessCount} thành công, {result.FailureCount} lỗi.";
        return ApiResponse<BulkImportResultDto>.Ok(result, message);
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
