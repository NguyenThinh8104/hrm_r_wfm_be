using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            .Where(u => u.Role.RoleCode == "STORE_MANAGER" && u.Status != "DELETED")
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

        if (!Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return ApiResponse<StoreManagerDto>.Fail($"Email '{dto.Email}' không đúng định dạng.");
        }

        var cleanPhone = Regex.Replace(dto.Phone.Trim(), @"[\s\.\-\(\)]", "");
        if (cleanPhone.StartsWith("+84")) cleanPhone = "0" + cleanPhone.Substring(3);
        else if (cleanPhone.StartsWith("84") && cleanPhone.Length == 11) cleanPhone = "0" + cleanPhone.Substring(2);

        if (!Regex.IsMatch(cleanPhone, @"^0[35789][0-9]{8}$"))
        {
            return ApiResponse<StoreManagerDto>.Fail($"Số điện thoại '{dto.Phone}' không hợp lệ (yêu cầu 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09. VD: 0912345678).");
        }
        var normalizedPhone = cleanPhone;

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

        // Ràng buộc duy nhất: Mỗi chi nhánh chỉ được phép có tối đa 1 Cửa hàng trưởng đang hoạt động
        var existingActiveManager = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => ((u.OriginalHomeBranchId ?? u.HomeBranchId) == dto.HomeBranchId) 
                                   && (u.Role.RoleCode == "STORE_MANAGER" || u.RoleId == storeManagerRole.Id)
                                   && u.Status == "ACTIVE");
        if (existingActiveManager != null)
        {
            return ApiResponse<StoreManagerDto>.Fail(
                $"Chi nhánh '{branch.Name}' hiện đã có Cửa hàng trưởng đang hoạt động ({existingActiveManager.FullName} - {existingActiveManager.EmployeeCode}). Mỗi chi nhánh chỉ được phép có tối đa 1 Cửa hàng trưởng.");
        }

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

    #endregion

    #region UC 1.5 - Quản lý Hồ sơ & Hợp đồng Nhân sự Toàn chuỗi

    public async Task<ApiResponse<List<EmployeeDetailDto>>> GetEmployeesAsync(EmployeeFilterDto filter, ulong actorId, string actorRole, ulong? actorBranchId)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .Include(u => u.OriginalHomeBranch)
            .Where(u => u.Status != "DELETED")
            .Where(u => u.Role.RoleCode != "OPERATIONS_ADMIN"
                     && u.Role.RoleCode != "OPERATIONSADMIN"
                     && u.Role.RoleCode != "ADMIN"
                     && u.Role.RoleCode != "BUSINESS_OWNER"
                     && u.Role.RoleCode != "BUSINESSOWNER")
            .AsQueryable();

        // Ràng buộc phân quyền: Nếu là STORE_MANAGER thì xem nhân viên chi nhánh mình (cả cơ hữu và nhân sự đang đi chi viện nơi khác)
        var normalizedRole = actorRole.ToUpper();
        if (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
        {
            if (!actorBranchId.HasValue)
                return ApiResponse<List<EmployeeDetailDto>>.Fail("Tài khoản Quản lý chưa được gán chi nhánh.");
            query = query.Where(u => u.HomeBranchId == actorBranchId.Value || u.OriginalHomeBranchId == actorBranchId.Value);

            // Tự động loại trừ Cửa hàng trưởng khỏi danh sách nhân viên vận hành
            query = query.Where(u => u.Role.RoleCode != "STORE_MANAGER");
        }
        else if (filter.BranchId.HasValue && filter.BranchId.Value > 0)
        {
            query = query.Where(u => u.HomeBranchId == filter.BranchId.Value || u.OriginalHomeBranchId == filter.BranchId.Value);
        }

        if (filter.ExcludeStoreManager == true)
        {
            query = query.Where(u => u.Role.RoleCode != "STORE_MANAGER");
        }

        if (filter.RoleId.HasValue && filter.RoleId.Value > 0)
        {
            query = query.Where(u => u.RoleId == filter.RoleId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(filter.RoleCode))
        {
            var targetRoleCode = filter.RoleCode.Trim().ToUpper();
            query = query.Where(u => u.Role.RoleCode == targetRoleCode);
        }

        var contractType = !string.IsNullOrWhiteSpace(filter.ContractType) ? filter.ContractType : filter.EmploymentType;
        if (!string.IsNullOrWhiteSpace(contractType))
        {
            var targetContractType = contractType.Trim().ToUpper();
            query = query.Where(u => u.EmploymentType == targetContractType);
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
                ContractType = u.EmploymentType,
                HomeBranchId = u.HomeBranchId,
                BranchCode = u.HomeBranch != null ? u.HomeBranch.BranchCode : "HQ",
                BranchName = u.HomeBranch != null ? u.HomeBranch.Name : "Trụ sở chính (HQ)",
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                IsDispatched = u.OriginalHomeBranchId != null,
                OriginalHomeBranchId = u.OriginalHomeBranchId,
                OriginalBranchName = u.OriginalHomeBranch != null ? u.OriginalHomeBranch.Name : null,
                LockedAt = u.Status == "INACTIVE" ? u.UpdatedAt : null
            })
            .ToListAsync();

        return ApiResponse<List<EmployeeDetailDto>>.Ok(employees, "Lấy danh sách hồ sơ nhân sự thành công.");
    }

    public async Task<ApiResponse<EmployeeStatsDto>> GetEmployeeStatsAsync(ulong? branchId, ulong actorId, string actorRole, ulong? actorBranchId)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Where(u => u.Status != "DELETED")
            .Where(u => u.Role.RoleCode != "OPERATIONS_ADMIN"
                     && u.Role.RoleCode != "OPERATIONSADMIN"
                     && u.Role.RoleCode != "ADMIN"
                     && u.Role.RoleCode != "BUSINESS_OWNER"
                     && u.Role.RoleCode != "BUSINESSOWNER")
            .AsQueryable();

        var normalizedRole = actorRole.ToUpper();
        if (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
        {
            if (!actorBranchId.HasValue)
                return ApiResponse<EmployeeStatsDto>.Fail("Tài khoản Quản lý chưa được gán chi nhánh.");
            query = query.Where(u => u.HomeBranchId == actorBranchId.Value);
        }
        else if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(u => u.HomeBranchId == branchId.Value);
        }
        else
        {
            // Tổng tất cả chi nhánh trong chuỗi: lấy toàn bộ nhân sự công tác tại các chi nhánh
            query = query.Where(u => u.HomeBranchId != null);
        }

        var users = await query
            .Select(u => new
            {
                u.Status,
                RoleCode = u.Role != null ? u.Role.RoleCode : string.Empty
            })
            .ToListAsync();

        var total = users.Count;
        var active = users.Count(u => u.Status != "INACTIVE");
        var inactive = users.Count(u => u.Status == "INACTIVE");

        var roleStats = new EmployeeRoleStatsDto
        {
            ShiftLeader = users.Count(u => u.RoleCode == "SHIFT_LEADER"),
            Cashier = users.Count(u => u.RoleCode == "CASHIER"),
            Sales = users.Count(u => u.RoleCode == "SALES_STAFF"),
            Security = users.Count(u => u.RoleCode == "SECURITY_GUARD" || u.RoleCode == "SECURITY"),
            Manager = users.Count(u => u.RoleCode == "STORE_MANAGER")
        };

        var stats = new EmployeeStatsDto
        {
            TotalEmployees = total,
            ActiveCount = active,
            InactiveCount = inactive,
            RoleStats = roleStats
        };

        return ApiResponse<EmployeeStatsDto>.Ok(stats, "Lấy thống kê nhân sự thành công.");
    }

    public async Task<ApiResponse<EmployeeDetailDto>> GetEmployeeByIdAsync(ulong userId, ulong actorId, string actorRole, ulong? actorBranchId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .Include(u => u.OriginalHomeBranch)
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
            ContractType = user.EmploymentType,
            HomeBranchId = user.HomeBranchId,
            BranchCode = user.HomeBranch?.BranchCode,
            BranchName = user.HomeBranch?.Name,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsDispatched = user.OriginalHomeBranchId != null,
            OriginalHomeBranchId = user.OriginalHomeBranchId,
            OriginalBranchName = user.OriginalHomeBranch?.Name,
            LockedAt = user.Status == "INACTIVE" ? user.UpdatedAt : null
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

        if (!Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return ApiResponse<EmployeeDetailDto>.Fail($"Email '{dto.Email}' không đúng định dạng.");
        }

        var cleanPhone = Regex.Replace(dto.Phone.Trim(), @"[\s\.\-\(\)]", "");
        if (cleanPhone.StartsWith("+84")) cleanPhone = "0" + cleanPhone.Substring(3);
        else if (cleanPhone.StartsWith("84") && cleanPhone.Length == 11) cleanPhone = "0" + cleanPhone.Substring(2);

        if (!Regex.IsMatch(cleanPhone, @"^0[35789][0-9]{8}$"))
        {
            return ApiResponse<EmployeeDetailDto>.Fail($"Số điện thoại '{dto.Phone}' không hợp lệ (yêu cầu 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09. VD: 0912345678).");
        }
        var normalizedPhone = cleanPhone;
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

        // Kiểm tra ràng buộc duy nhất 1 Cửa hàng trưởng trên mỗi chi nhánh
        if (targetRole.RoleCode == "STORE_MANAGER")
        {
            var existingActiveManager = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => ((u.OriginalHomeBranchId ?? u.HomeBranchId) == branchIdToAssign) 
                                       && (u.Role.RoleCode == "STORE_MANAGER" || u.RoleId == targetRole.Id)
                                       && u.Status == "ACTIVE");
            if (existingActiveManager != null)
            {
                return ApiResponse<EmployeeDetailDto>.Fail(
                    $"Chi nhánh '{branch.Name}' hiện đã có Cửa hàng trưởng đang hoạt động ({existingActiveManager.FullName} - {existingActiveManager.EmployeeCode}). Mỗi chi nhánh chỉ được phép có tối đa 1 Cửa hàng trưởng.");
            }
        }

        // 4. Thẩm định định biên nhân sự theo Effective Quota (ưu tiên StaffCount > 0, mặc định theo BranchTier)
        var quotaResult = await _headcountService.ValidateHeadcountAsync(branchIdToAssign);

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
            EmploymentType = string.Equals(!string.IsNullOrWhiteSpace(dto.ContractType) ? dto.ContractType : dto.EmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME",
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
            EffectiveQuota = quotaResult.EffectiveQuota,
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
            ContractType = newUser.EmploymentType,
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

        // Kiểm tra và chuẩn hóa SĐT nếu thay đổi
        var cleanPhone = Regex.Replace(dto.Phone.Trim(), @"[\s\.\-\(\)]", "");
        if (cleanPhone.StartsWith("+84")) cleanPhone = "0" + cleanPhone.Substring(3);
        else if (cleanPhone.StartsWith("84") && cleanPhone.Length == 11) cleanPhone = "0" + cleanPhone.Substring(2);

        if (!Regex.IsMatch(cleanPhone, @"^0[35789][0-9]{8}$"))
        {
            return ApiResponse<EmployeeDetailDto>.Fail($"Số điện thoại '{dto.Phone}' không hợp lệ (yêu cầu 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09. VD: 0912345678).");
        }
        var normalizedPhone = cleanPhone;
        if (normalizedPhone != user.Phone && await _context.Users.AnyAsync(u => u.Phone == normalizedPhone && u.Id != user.Id))
            return ApiResponse<EmployeeDetailDto>.Fail($"Số điện thoại '{normalizedPhone}' đã được sử dụng bởi người khác.");

        // Kiểm tra và cập nhật Email nếu có thay đổi
        string? cleanEmail = null;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            cleanEmail = dto.Email.Trim().ToLower();
            if (!Regex.IsMatch(cleanEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return ApiResponse<EmployeeDetailDto>.Fail($"Email '{dto.Email}' không đúng định dạng.");
            }
            if (cleanEmail != user.Email.ToLower() && await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail && u.Id != user.Id))
            {
                return ApiResponse<EmployeeDetailDto>.Fail($"Địa chỉ email '{cleanEmail}' đã được sử dụng bởi người khác.");
            }
        }

        ulong targetBranchId = (normalizedActorRole != "STORE_MANAGER" && normalizedActorRole != "STOREMANAGER" && dto.HomeBranchId > 0)
            ? dto.HomeBranchId
            : user.HomeBranchId ?? 0;

        // Kiểm tra ràng buộc duy nhất 1 Cửa hàng trưởng trên mỗi chi nhánh khi cập nhật
        if (targetRole.RoleCode == "STORE_MANAGER")
        {
            var existingManager = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => ((u.OriginalHomeBranchId ?? u.HomeBranchId) == targetBranchId) 
                                       && (u.Role.RoleCode == "STORE_MANAGER" || u.RoleId == targetRole.Id)
                                       && u.Status == "ACTIVE"
                                       && u.Id != user.Id);
            if (existingManager != null)
            {
                var targetBranch = await _context.Branches.FindAsync(targetBranchId);
                return ApiResponse<EmployeeDetailDto>.Fail(
                    $"Chi nhánh '{targetBranch?.Name ?? $"#{targetBranchId}"}' hiện đã có Cửa hàng trưởng đang hoạt động ({existingManager.FullName} - {existingManager.EmployeeCode}). Mỗi chi nhánh chỉ được phép có tối đa 1 Cửa hàng trưởng.");
            }
        }

        var oldValues = new
        {
            user.FullName,
            user.Email,
            user.Phone,
            Role = user.Role.RoleCode,
            user.EmploymentType,
            user.HomeBranchId,
            user.Status
        };

        var changedFields = new Dictionary<string, (string OldVal, string NewVal)>();
        var oldEmail = user.Email;
        var oldRoleName = user.Role?.RoleName ?? user.Role?.RoleCode ?? "Nhân viên";
        var oldBranchName = user.HomeBranch?.Name ?? $"Chi nhánh #{user.HomeBranchId}";
        var oldContractType = user.EmploymentType == "PART_TIME" ? "Bán thời gian (Part-time)" : "Toàn thời gian (Full-time)";

        if (!string.Equals(user.FullName, dto.FullName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            changedFields["Họ và Tên"] = (user.FullName, dto.FullName.Trim());
            user.FullName = dto.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cleanEmail) && !string.Equals(user.Email.ToLower(), cleanEmail, StringComparison.OrdinalIgnoreCase))
        {
            changedFields["Địa Chỉ Email"] = (user.Email, cleanEmail);
            user.Email = cleanEmail;
        }

        if (!string.Equals(user.Phone, normalizedPhone, StringComparison.OrdinalIgnoreCase))
        {
            changedFields["Số Điện Thoại"] = (user.Phone, normalizedPhone);
            user.Phone = normalizedPhone;
        }

        if (user.RoleId != targetRole.Id)
        {
            changedFields["Vai Trò (Chức Danh)"] = (oldRoleName, targetRole.RoleName ?? targetRole.RoleCode);
            user.RoleId = targetRole.Id;
        }

        var effectiveEmploymentType = !string.IsNullOrWhiteSpace(dto.ContractType) ? dto.ContractType : dto.EmploymentType;
        var normalizedType = string.Equals(effectiveEmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME";
        var newContractLabel = normalizedType == "PART_TIME" ? "Bán thời gian (Part-time)" : "Toàn thời gian (Full-time)";
        if (!string.Equals(user.EmploymentType, normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            changedFields["Hình Thức Hợp Đồng"] = (oldContractType, newContractLabel);
            user.EmploymentType = normalizedType;
        }

        if (normalizedActorRole != "STORE_MANAGER" && normalizedActorRole != "STOREMANAGER")
        {
            if (dto.HomeBranchId > 0 && dto.HomeBranchId != user.HomeBranchId)
            {
                // 1. Không cho phép điều chuyển cố định khi nhân sự đang trong thời gian chi viện tạm thời
                if (user.OriginalHomeBranchId != null)
                {
                    return ApiResponse<EmployeeDetailDto>.Fail(
                        "Không thể điều chuyển chi nhánh cố định: Nhân sự hiện đang trong thời gian điều động chi viện tại chi nhánh khác. Vui lòng hoàn tất hoặc kết thúc lệnh điều động trước khi điều chuyển công tác chính thức.");
                }

                var activeDispatch = await _context.DispatchEmployees
                    .Include(de => de.Dispatch)
                    .AnyAsync(de => de.UserId == user.Id
                                 && (de.Dispatch.Status == "PENDING" || de.Dispatch.Status == "APPROVED")
                                 && de.Dispatch.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow));
                if (activeDispatch)
                {
                    return ApiResponse<EmployeeDetailDto>.Fail(
                        "Không thể điều chuyển chi nhánh cố định: Nhân sự đang có lệnh điều động chi viện đang chờ duyệt hoặc sắp có hiệu lực.");
                }

                // 2. Kiểm tra chi nhánh đích
                var targetBranch = await _context.Branches.FindAsync(dto.HomeBranchId);
                if (targetBranch == null || targetBranch.Status == "INACTIVE" || targetBranch.Status == "LOCKED")
                {
                    return ApiResponse<EmployeeDetailDto>.Fail(
                        $"Không thể điều chuyển: Chi nhánh đích '{targetBranch?.Name ?? $"#{dto.HomeBranchId}"}' không tồn tại hoặc đang bị tạm khóa.");
                }

                // 3. Thẩm định định biên nhân sự của chi nhánh đích (Quota Tier)
                var quotaResult = await _headcountService.ValidateHeadcountAsync(dto.HomeBranchId);
                if (!quotaResult.IsSuccess)
                {
                    return ApiResponse<EmployeeDetailDto>.Fail(quotaResult.ErrorMessage ?? 
                        $"Không thể điều chuyển nhân sự: Chi nhánh đích '{targetBranch.Name}' đã đạt giới hạn định biên tối đa ({quotaResult.EffectiveQuota} nhân sự). Vui lòng nâng định biên Tier trước khi tiếp nhận nhân sự.");
                }

                changedFields["Chi Nhánh Công Tác"] = (oldBranchName, targetBranch.Name);
                user.HomeBranchId = dto.HomeBranchId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var newStatus = dto.Status.ToUpper();
            if (newStatus == "ACTIVE")
            {
                var effectiveBranchId = user.HomeBranchId ?? targetBranchId;
                if (effectiveBranchId > 0)
                {
                    var bEntity = await _context.Branches.FindAsync(effectiveBranchId);
                    if (bEntity != null && (bEntity.Status == "INACTIVE" || bEntity.Status == "LOCKED"))
                    {
                        return ApiResponse<EmployeeDetailDto>.Fail(
                            $"Không thể kích hoạt tài khoản: Chi nhánh trực thuộc '{bEntity.Name}' hiện đang bị tạm khóa hoặc ngừng hoạt động. " +
                            "Vui lòng mở khóa chi nhánh hoặc điều chuyển nhân sự sang chi nhánh đang hoạt động trước khi kích hoạt lại.");
                    }
                }
            }
            if (!string.Equals(user.Status, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                changedFields["Trạng Thái Tài Khoản"] = (user.Status == "ACTIVE" ? "Đang hoạt động" : "Đã khóa", newStatus == "ACTIVE" ? "Đang hoạt động" : "Đã khóa");
                user.Status = newStatus;
            }
        }

        // Kiểm tra xem có trường thông tin nào thực sự thay đổi không
        if (changedFields.Count == 0)
        {
            return ApiResponse<EmployeeDetailDto>.Ok(new EmployeeDetailDto
            {
                Id = user.Id,
                EmployeeCode = user.EmployeeCode,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                RoleName = user.Role?.RoleName ?? user.Role?.RoleCode ?? "Nhân viên",
                RoleCode = user.Role?.RoleCode ?? "",
                HomeBranchId = user.HomeBranchId,
                BranchCode = user.HomeBranch?.BranchCode,
                BranchName = user.HomeBranch?.Name,
                EmploymentType = user.EmploymentType,
                ContractType = user.EmploymentType,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            }, "Hồ sơ nhân sự không có thông tin nào thay đổi.");
        }

        // Bắt buộc lý do khi có thay đổi thông tin (tự động gán nếu là tái tuyển dụng / kích hoạt lại)
        var effectiveReason = dto.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(effectiveReason) || effectiveReason.Length < 5)
        {
            if (user.Status != "ACTIVE" && dto.Status == "ACTIVE")
            {
                effectiveReason = "Tái tuyển dụng và kích hoạt lại hồ sơ nhân sự";
            }
            else
            {
                return ApiResponse<EmployeeDetailDto>.Fail("Vui lòng nhập lý do điều chỉnh hồ sơ nhân sự (tối thiểu 5 ký tự) để lưu vết kiểm toán và gửi email thông báo.");
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "UPDATE_EMPLOYEE", "users", user.Id, oldValues, new
        {
            user.FullName,
            user.Email,
            user.Phone,
            Role = targetRole.RoleCode,
            user.EmploymentType,
            user.HomeBranchId,
            user.Status,
            Reason = effectiveReason
        }, ipAddress);

        // Kích hoạt gửi email thông báo điều chỉnh thông tin hồ sơ
        var targetRecipientEmail = !string.IsNullOrWhiteSpace(cleanEmail) ? cleanEmail : user.Email;
        var reasonTrimmed = effectiveReason;
        _ = Task.Run(async () =>
        {
            try
            {
                var fieldsMap = changedFields.Count > 0 
                    ? changedFields 
                    : new Dictionary<string, (string OldVal, string NewVal)> { { "Thông tin hồ sơ", ("Đã ghi nhận", "Đã cập nhật lại") } };

                await _emailService.SendProfileUpdateNotificationEmailAsync(
                    targetRecipientEmail,
                    user.FullName,
                    user.EmployeeCode,
                    fieldsMap,
                    reasonTrimmed);

                // Nếu địa chỉ email bị đổi, gửi thêm 1 bản đến email cũ để đảm bảo an ninh
                if (!string.IsNullOrWhiteSpace(oldEmail) && !string.Equals(oldEmail, targetRecipientEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await _emailService.SendProfileUpdateNotificationEmailAsync(
                        oldEmail,
                        user.FullName,
                        user.EmployeeCode,
                        fieldsMap,
                        reasonTrimmed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi email thông báo cập nhật hồ sơ cho {EmployeeCode}", user.EmployeeCode);
            }
        });

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
            ContractType = user.EmploymentType,
            HomeBranchId = user.HomeBranchId,
            BranchCode = user.HomeBranch?.BranchCode,
            BranchName = user.HomeBranch?.Name,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        }, "Cập nhật thông tin hồ sơ nhân sự thành công.");
    }

    /// <summary>
    /// UC 1.5 - Phương án 1: Xóa tài khoản nhân sự (Đã bị vô hiệu hóa theo chuẩn quản trị nhân sự Enterprise).
    /// </summary>
    public Task<ApiResponse<bool>> DeleteUserAsync(ulong userId, ulong actorId, string actorRole, string? ipAddress)
    {
        return Task.FromResult(
            ApiResponse<bool>.Fail("Hệ thống không cho phép xóa tài khoản nhân sự nhằm bảo đảm tính toàn vẹn dữ liệu đối soát bảng công, tiền lương và kiểm toán doanh nghiệp. Vui lòng sử dụng tính năng 'Khóa tài khoản' để chấm dứt hoạt động của nhân sự.")
        );
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
            .OrderBy(b => b.Id)
            .Select(b => new BranchSimpleDto
            {
                Id = b.Id,
                BranchCode = b.BranchCode,
                Code = b.BranchCode,
                Name = b.Name,
                Address = b.Address,
                Status = b.Status,
                BranchTier = b.TierId ?? (int)b.BranchTier,
                TierId = b.TierId,
                TierName = b.Tier != null ? b.Tier.TierName : $"Tier {(int)b.BranchTier}",
                StaffCount = b.StaffCount > 0 
                    ? b.StaffCount 
                    : (b.Tier != null && b.Tier.MaxStaffCount.HasValue && b.Tier.MaxStaffCount.Value > 0 
                        ? b.Tier.MaxStaffCount.Value 
                        : (b.Tier != null && b.Tier.MinStaffCount > 0 ? b.Tier.MinStaffCount : 15))
            })
            .ToListAsync();

        return ApiResponse<List<BranchSimpleDto>>.Ok(branches);
    }

    #endregion

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

    public async Task<ApiResponse<DuplicateCheckResultDto>> CheckDuplicateAsync(string? employeeCode, string? email, string? phone, ulong? excludeId)
    {
        var result = new DuplicateCheckResultDto();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            var code = employeeCode.Trim().ToUpper();
            var existing = await _context.Users
                .Where(u => u.EmployeeCode == code && (!excludeId.HasValue || u.Id != excludeId.Value))
                .Select(u => new { u.Id, u.FullName, u.EmployeeCode, u.Status })
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                result.IsEmployeeCodeDuplicate = true;
                result.ExistingEmployeeName = existing.FullName;
                result.ExistingEmployeeCode = existing.EmployeeCode;
                result.ExistingUserId = existing.Id;
                result.ExistingUserStatus = existing.Status;
                result.CanUnlock = string.Equals(existing.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normEmail = email.Trim().ToLower();
            var existing = await _context.Users
                .Where(u => u.Email.ToLower() == normEmail && (!excludeId.HasValue || u.Id != excludeId.Value))
                .Select(u => new { u.Id, u.FullName, u.EmployeeCode, u.Status })
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                result.IsEmailDuplicate = true;
                result.ExistingEmailOwnerName = existing.FullName;
                if (!result.IsEmployeeCodeDuplicate)
                {
                    result.ExistingUserId = existing.Id;
                    result.ExistingEmployeeName = existing.FullName;
                    result.ExistingEmployeeCode = existing.EmployeeCode;
                    result.ExistingUserStatus = existing.Status;
                    result.CanUnlock = string.Equals(existing.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var normPhone = phone.Trim().Replace(" ", "");
            var existing = await _context.Users
                .Where(u => u.Phone == normPhone && (!excludeId.HasValue || u.Id != excludeId.Value))
                .Select(u => new { u.Id, u.FullName, u.EmployeeCode, u.Status })
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                result.IsPhoneDuplicate = true;
                result.ExistingPhoneOwnerName = existing.FullName;
                if (!result.IsEmployeeCodeDuplicate && !result.IsEmailDuplicate)
                {
                    result.ExistingUserId = existing.Id;
                    result.ExistingEmployeeName = existing.FullName;
                    result.ExistingEmployeeCode = existing.EmployeeCode;
                    result.ExistingUserStatus = existing.Status;
                    result.CanUnlock = string.Equals(existing.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return ApiResponse<DuplicateCheckResultDto>.Ok(result);
    }
}
