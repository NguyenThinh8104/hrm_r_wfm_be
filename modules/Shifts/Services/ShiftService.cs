using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Domain.Constants;
using Domain.Entities;
using Modules.Shifts.Common;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ xử lý logic nghiệp vụ quản lý mẫu ca chuẩn, khởi tạo khung lịch tháng, định mức nhân sự và phân bổ ca làm việc (UC 2.1).
/// </summary>
public class ShiftService : IShiftService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ShiftService> _logger;

    public ShiftService(AppDbContext context, IEmailService emailService, ILogger<ShiftService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    // =========================================================================
    // 0. Khung Ca Chung / Riêng Toàn Diện (Enterprise Global & Custom Shifts)
    // =========================================================================

    public async Task<PagedResult<ShiftTemplateDto>> GetShiftTemplatesPagedAsync(
        string? scope, ulong? branchId, string? type, bool? isActive, string? q, int page, int pageSize)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        var query = _context.ShiftTemplates
            .Include(st => st.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(scope))
        {
            var normalizedScope = scope.Trim().ToUpper();
            query = query.Where(st => st.Scope == normalizedScope);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(st => st.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToUpper();
            query = query.Where(st => st.ShiftType == normalizedType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(st => st.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var queryText = q.Trim().ToLower();
            query = query.Where(st =>
                st.TemplateCode.ToLower().Contains(queryText) ||
                st.Name.ToLower().Contains(queryText) ||
                (st.Description != null && st.Description.ToLower().Contains(queryText)));
        }

        var totalCount = await query.CountAsync();

        var templates = await query
            .OrderBy(st => st.Scope)
            .ThenBy(st => st.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var branchesUsingGlobalCount = await _context.Branches
            .CountAsync(b => b.ShiftMode == "GLOBAL" && b.Status == "ACTIVE");

        var dtos = templates.Select(st => MapEntityToDto(st, branchesUsingGlobalCount)).ToList();

        return new PagedResult<ShiftTemplateDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ShiftTemplateStatsDto> GetShiftTemplateStatsAsync()
    {
        var totalGlobalShifts = await _context.ShiftTemplates
            .CountAsync(st => st.Scope == "GLOBAL" && st.IsActive);

        var branchesUsingGlobal = await _context.Branches
            .CountAsync(b => b.ShiftMode == "GLOBAL" && b.Status == "ACTIVE");

        var branchesUsingCustom = await _context.Branches
            .CountAsync(b => b.ShiftMode == "CUSTOM" && b.Status == "ACTIVE");

        var customBranchesWithCustomMode = await _context.Branches
            .Where(b => b.ShiftMode == "CUSTOM" && b.Status == "ACTIVE")
            .Select(b => b.Id)
            .ToListAsync();

        var activeEffectiveShifts = await _context.ShiftTemplates
            .Where(st => st.IsActive && (st.Scope == "GLOBAL" || (st.Scope == "BRANCH" && st.BranchId.HasValue && customBranchesWithCustomMode.Contains(st.BranchId.Value))))
            .ToListAsync();

        var overnightShifts = activeEffectiveShifts.Count(st => st.EndTime < st.StartTime);

        return new ShiftTemplateStatsDto
        {
            TotalGlobalShifts = totalGlobalShifts,
            BranchesUsingGlobal = branchesUsingGlobal,
            BranchesUsingCustom = branchesUsingCustom,
            OvernightShifts = overnightShifts
        };
    }

    public async Task<ShiftOperationResult<ShiftTemplateDto>> CreateShiftTemplateAsync(
        CreateShiftTemplateRequest request, ulong? actorId = null, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_DATA", "Tên ca làm việc không được để trống.");
        }

        if (!ShiftCalculationHelper.TryParseTime(request.StartTime, out var startTime))
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ bắt đầu không đúng định dạng HH:mm.");
        }

        if (!ShiftCalculationHelper.TryParseTime(request.EndTime, out var endTime))
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ kết thúc không đúng định dạng HH:mm.");
        }

        if (startTime == endTime)
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ bắt đầu và giờ kết thúc không được trùng nhau.");
        }

        var scope = (request.Scope ?? "GLOBAL").Trim().ToUpper();
        if (scope != "GLOBAL" && scope != "BRANCH")
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_SCOPE", "Scope chỉ được là 'GLOBAL' hoặc 'BRANCH'.");
        }

        Branch? branch = null;
        if (scope == "BRANCH")
        {
            if (!request.BranchId.HasValue || request.BranchId.Value == 0)
            {
                return ShiftOperationResult<ShiftTemplateDto>.BadRequest("BRANCH_REQUIRED", "Ca riêng bắt buộc phải chọn chi nhánh (branchId).");
            }

            branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId.Value);
            if (branch == null)
            {
                return ShiftOperationResult<ShiftTemplateDto>.NotFound($"Không tìm thấy chi nhánh với ID {request.BranchId.Value}.");
            }

            // Quy tắc 11: Nếu chi nhánh đang bị khóa, từ chối thêm ca riêng (HTTP 423)
            if (branch.LockedAt != null || string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) || string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            {
                return ShiftOperationResult<ShiftTemplateDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể thêm ca riêng.");
            }
        }
        else
        {
            request.BranchId = null;
        }

        // Quy tắc 8: Trong cùng một bộ ca (cùng Scope + BranchId), không cho hai ca trùng hoàn toàn StartTime và EndTime
        var duplicateTimeExists = await _context.ShiftTemplates
            .AnyAsync(st => st.Scope == scope &&
                            st.BranchId == request.BranchId &&
                            st.StartTime == startTime &&
                            st.EndTime == endTime &&
                            st.IsActive);

        if (duplicateTimeExists)
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("DUPLICATE_SHIFT_TIME", "Trong cùng một bộ ca không được có hai ca trùng hoàn toàn giờ bắt đầu và giờ kết thúc.");
        }

        // Quy tắc 7: TemplateCode không trùng. Ca riêng tự sinh mã theo tiền tố chi nhánh (ví dụ HN01-S1). Ca chung do Admin nhập hoặc tự sinh.
        string templateCode;
        if (scope == "BRANCH")
        {
            var prefix = !string.IsNullOrWhiteSpace(branch!.BranchCode) ? branch.BranchCode.Trim() : $"BR{branch.Id}";
            var branchShiftsCount = await _context.ShiftTemplates
                .CountAsync(st => st.BranchId == branch.Id);
            int nextIndex = branchShiftsCount + 1;
            templateCode = $"{prefix}-S{nextIndex}".ToUpper();

            while (await _context.ShiftTemplates.AnyAsync(st => st.TemplateCode.ToUpper() == templateCode))
            {
                nextIndex++;
                templateCode = $"{prefix}-S{nextIndex}".ToUpper();
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.TemplateCode))
            {
                templateCode = request.TemplateCode.Trim().ToUpper();
                if (await _context.ShiftTemplates.AnyAsync(st => st.TemplateCode.ToUpper() == templateCode))
                {
                    return ShiftOperationResult<ShiftTemplateDto>.BadRequest("DUPLICATE_CODE", $"Mã khung ca '{templateCode}' đã tồn tại trong hệ thống.");
                }
            }
            else
            {
                var globalCount = await _context.ShiftTemplates.CountAsync(st => st.Scope == "GLOBAL");
                int nextIndex = globalCount + 1;
                templateCode = $"CA_{nextIndex:D2}".ToUpper();
                while (await _context.ShiftTemplates.AnyAsync(st => st.TemplateCode.ToUpper() == templateCode))
                {
                    nextIndex++;
                    templateCode = $"CA_{nextIndex:D2}".ToUpper();
                }
            }
        }

        // 9. IsOvernight luôn do server tính: EndTime < StartTime
        var (paidHours, nightHours, isOvernight) = ShiftCalculationHelper.CalculateHours(startTime, endTime, request.BreakDuration);

        var shiftType = !string.IsNullOrWhiteSpace(request.ShiftType)
            ? request.ShiftType.Trim().ToUpper()
            : ShiftCalculationHelper.InferShiftType(startTime);

        var now = DateTime.UtcNow;
        var template = new ShiftTemplate
        {
            TemplateCode = templateCode,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Scope = scope,
            BranchId = request.BranchId,
            ShiftType = shiftType,
            StartTime = startTime,
            EndTime = endTime,
            IsOvernight = isOvernight,
            BreakDurationMinutes = request.BreakDuration,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();

        // 12. Ghi SystemAuditLog
        await LogAuditAsync(
            actorId,
            "CREATE_SHIFT_TEMPLATE",
            "shift_templates",
            template.Id,
            null,
            new { template.Id, template.TemplateCode, template.Name, template.Scope, template.BranchId, template.StartTime, template.EndTime, template.IsActive },
            ipAddress);

        template.Branch = branch;
        var dto = MapEntityToDto(template);
        return ShiftOperationResult<ShiftTemplateDto>.Created(dto, "Tạo khung ca thành công.");
    }

    public async Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateAsync(
        uint id, UpdateShiftTemplateRequest request, bool confirm, ulong? actorId = null, string? ipAddress = null)
    {
        var template = await _context.ShiftTemplates
            .Include(st => st.Branch)
            .FirstOrDefaultAsync(st => st.Id == id);

        if (template == null)
        {
            return ShiftOperationResult<ShiftTemplateDto>.NotFound("Không tìm thấy mẫu ca làm việc.");
        }

        // Quy tắc 11: Nếu là ca riêng và chi nhánh đang bị khóa, từ chối sửa (HTTP 423)
        if (template.Scope == "BRANCH" && template.BranchId.HasValue)
        {
            var branch = template.Branch ?? await _context.Branches.FindAsync(template.BranchId.Value);
            if (branch != null && (branch.LockedAt != null || string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) || string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)))
            {
                return ShiftOperationResult<ShiftTemplateDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể sửa ca riêng.");
            }
        }

        // Quy tắc 6: Sửa ca chung: nếu có chi nhánh đang dùng ca chung và chưa confirm, trả 409
        if (template.Scope == "GLOBAL")
        {
            var affectedBranchCount = await _context.Branches
                .CountAsync(b => b.ShiftMode == "GLOBAL" && b.Status == "ACTIVE");

            if (affectedBranchCount > 0 && !confirm)
            {
                return ShiftOperationResult<ShiftTemplateDto>.Conflict(
                    "IMPACT_CONFIRM_REQUIRED",
                    $"Có {affectedBranchCount} chi nhánh đang áp dụng ca chung này. Xác nhận cập nhật?",
                    affectedBranchCount: affectedBranchCount);
            }
        }

        TimeOnly newStartTime = template.StartTime;
        TimeOnly newEndTime = template.EndTime;

        if (!string.IsNullOrWhiteSpace(request.StartTime))
        {
            if (!ShiftCalculationHelper.TryParseTime(request.StartTime, out newStartTime))
            {
                return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ bắt đầu không đúng định dạng HH:mm.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EndTime))
        {
            if (!ShiftCalculationHelper.TryParseTime(request.EndTime, out newEndTime))
            {
                return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ kết thúc không đúng định dạng HH:mm.");
            }
        }

        if (newStartTime == newEndTime)
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_TIME", "Giờ bắt đầu và giờ kết thúc không được trùng nhau.");
        }

        // Quy tắc 8: Kiểm tra trùng giờ trong cùng bộ ca
        var duplicateTimeExists = await _context.ShiftTemplates
            .AnyAsync(st => st.Id != template.Id &&
                            st.Scope == template.Scope &&
                            st.BranchId == template.BranchId &&
                            st.StartTime == newStartTime &&
                            st.EndTime == newEndTime &&
                            st.IsActive);

        if (duplicateTimeExists)
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("DUPLICATE_SHIFT_TIME", "Trong cùng một bộ ca không được có hai ca trùng hoàn toàn giờ bắt đầu và giờ kết thúc.");
        }

        var oldValues = new
        {
            template.Name,
            template.Description,
            template.ShiftType,
            template.StartTime,
            template.EndTime,
            template.BreakDurationMinutes,
            template.IsOvernight,
            template.IsActive
        };

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            template.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            template.Description = request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ShiftType))
        {
            template.ShiftType = request.ShiftType.Trim().ToUpper();
        }

        template.StartTime = newStartTime;
        template.EndTime = newEndTime;
        template.IsOvernight = newEndTime < newStartTime;

        if (request.BreakDuration.HasValue)
        {
            template.BreakDurationMinutes = request.BreakDuration.Value;
        }

        if (request.IsActive.HasValue)
        {
            template.IsActive = request.IsActive.Value;
        }

        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var newValues = new
        {
            template.Name,
            template.Description,
            template.ShiftType,
            template.StartTime,
            template.EndTime,
            template.BreakDurationMinutes,
            template.IsOvernight,
            template.IsActive
        };

        // 12. Ghi SystemAuditLog
        await LogAuditAsync(
            actorId,
            "UPDATE_SHIFT_TEMPLATE",
            "shift_templates",
            template.Id,
            oldValues,
            newValues,
            ipAddress);

        return ShiftOperationResult<ShiftTemplateDto>.Ok(MapEntityToDto(template), "Cập nhật mẫu ca thành công.");
    }

    public async Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateActiveAsync(
        uint id, bool isActive, bool confirm, ulong? actorId = null, string? ipAddress = null)
    {
        var template = await _context.ShiftTemplates
            .Include(st => st.Branch)
            .FirstOrDefaultAsync(st => st.Id == id);

        if (template == null)
        {
            return ShiftOperationResult<ShiftTemplateDto>.NotFound("Không tìm thấy mẫu ca làm việc.");
        }

        // Quy tắc 11: Nếu là ca riêng và chi nhánh đang bị khóa, từ chối sửa (HTTP 423)
        if (template.Scope == "BRANCH" && template.BranchId.HasValue)
        {
            var branch = template.Branch ?? await _context.Branches.FindAsync(template.BranchId.Value);
            if (branch != null && (branch.LockedAt != null || string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) || string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase)))
            {
                return ShiftOperationResult<ShiftTemplateDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể sửa trạng thái ca riêng.");
            }
        }

        // Quy tắc 6: Nếu ngừng áp dụng ca chung và có chi nhánh đang dùng ca chung: trả 409 nếu chưa confirm
        if (template.Scope == "GLOBAL" && !isActive)
        {
            var affectedBranchCount = await _context.Branches
                .CountAsync(b => b.ShiftMode == "GLOBAL" && b.Status == "ACTIVE");

            if (affectedBranchCount > 0 && !confirm)
            {
                return ShiftOperationResult<ShiftTemplateDto>.Conflict(
                    "IMPACT_CONFIRM_REQUIRED",
                    $"Có {affectedBranchCount} chi nhánh đang áp dụng ca chung này. Xác nhận ngừng áp dụng ca chung?",
                    affectedBranchCount: affectedBranchCount);
            }
        }

        var oldValues = new { template.IsActive };
        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var newValues = new { template.IsActive };
        await LogAuditAsync(
            actorId,
            "UPDATE_SHIFT_TEMPLATE_STATUS",
            "shift_templates",
            template.Id,
            oldValues,
            newValues,
            ipAddress);

        var msg = isActive ? "Kích hoạt khung ca thành công." : "Ngừng áp dụng khung ca thành công.";
        return ShiftOperationResult<ShiftTemplateDto>.Ok(MapEntityToDto(template), msg);
    }

    public async Task<ShiftOperationResult<BranchEffectiveShiftsDto>> GetBranchEffectiveShiftsAsync(
        ulong branchId, ulong? currentUserId = null, string? currentUserRole = null)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch == null)
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.NotFound($"Không tìm thấy chi nhánh với ID {branchId}.");
        }

        // Phân quyền: Store Manager chỉ được đọc chi nhánh của mình
        if (!string.IsNullOrWhiteSpace(currentUserRole) &&
            (currentUserRole.Equals("StoreManager", StringComparison.OrdinalIgnoreCase) ||
             currentUserRole.Equals("STORE_MANAGER", StringComparison.OrdinalIgnoreCase)))
        {
            if (currentUserId.HasValue)
            {
                var user = await _context.Users.FindAsync(currentUserId.Value);
                if (user != null && user.HomeBranchId.HasValue && user.HomeBranchId.Value != branchId)
                {
                    return ShiftOperationResult<BranchEffectiveShiftsDto>.Forbidden("Store Manager chỉ được xem ca hiệu lực của chi nhánh được phân công.");
                }
            }
        }

        // 1. Ca hiệu lực của chi nhánh:
        // - ShiftMode = GLOBAL: ca có Scope = 'GLOBAL' và IsActive = true
        // - ShiftMode = CUSTOM: ca có Scope = 'BRANCH', BranchId = branchId, và IsActive = true
        var isGlobal = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);
        List<ShiftTemplate> shifts;

        if (isGlobal)
        {
            shifts = await _context.ShiftTemplates
                .Where(st => st.Scope == "GLOBAL" && st.IsActive)
                .OrderBy(st => st.StartTime)
                .ToListAsync();
        }
        else
        {
            shifts = await _context.ShiftTemplates
                .Where(st => st.Scope == "BRANCH" && st.BranchId == branchId && st.IsActive)
                .OrderBy(st => st.StartTime)
                .ToListAsync();
        }

        var shiftDtos = shifts.Select(st => MapEntityToDto(st)).ToList();

        var result = new BranchEffectiveShiftsDto
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            ShiftMode = branch.ShiftMode,
            Source = branch.ShiftMode,
            Shifts = shiftDtos
        };

        return ShiftOperationResult<BranchEffectiveShiftsDto>.Ok(result);
    }

    public async Task<ShiftOperationResult<List<ShiftTemplateDto>>> GetBranchCustomShiftsAsync(ulong branchId)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch == null)
        {
            return ShiftOperationResult<List<ShiftTemplateDto>>.NotFound($"Không tìm thấy chi nhánh với ID {branchId}.");
        }

        // Trả toàn bộ ca riêng của chi nhánh (kể cả khi đang ở chế độ GLOBAL), kèm cờ isActive
        var customShifts = await _context.ShiftTemplates
            .Where(st => st.Scope == "BRANCH" && st.BranchId == branchId)
            .OrderBy(st => st.StartTime)
            .ToListAsync();

        var dtos = customShifts.Select(st => MapEntityToDto(st)).ToList();
        return ShiftOperationResult<List<ShiftTemplateDto>>.Ok(dtos);
    }

    public async Task<ShiftOperationResult<BranchEffectiveShiftsDto>> UpdateBranchShiftModeAsync(
        ulong branchId, string mode, bool confirm, ulong? actorId = null, string? ipAddress = null)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch == null)
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.NotFound($"Không tìm thấy chi nhánh với ID {branchId}.");
        }

        var normalizedMode = (mode ?? string.Empty).Trim().ToUpper();
        if (normalizedMode != "GLOBAL" && normalizedMode != "CUSTOM")
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.BadRequest("INVALID_MODE", "Chế độ ca chỉ chấp nhận 'GLOBAL' hoặc 'CUSTOM'.");
        }

        // Quy tắc 11: Nếu chi nhánh đang bị khóa, từ chối đổi ShiftMode (HTTP 423)
        if (branch.LockedAt != null || string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) || string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể đổi chế độ khung ca.");
        }

        // Quy tắc 2: Không cho chuyển sang CUSTOM nếu chi nhánh chưa có ca riêng nào IsActive=true
        if (normalizedMode == "CUSTOM")
        {
            var hasActiveCustom = await _context.ShiftTemplates
                .AnyAsync(st => st.Scope == "BRANCH" && st.BranchId == branchId && st.IsActive);

            if (!hasActiveCustom)
            {
                return ShiftOperationResult<BranchEffectiveShiftsDto>.BadRequest(
                    "NO_ACTIVE_CUSTOM_SHIFT",
                    "Không thể chuyển sang chế độ ca riêng vì chi nhánh chưa có ca riêng nào đang hoạt động.");
            }
        }

        // Nếu mode không thay đổi
        if (string.Equals(branch.ShiftMode, normalizedMode, StringComparison.OrdinalIgnoreCase))
        {
            var currentEffective = await GetBranchEffectiveShiftsAsync(branchId);
            return ShiftOperationResult<BranchEffectiveShiftsDto>.Ok(currentEffective.Data!, "Chế độ ca không thay đổi.");
        }

        // Quy tắc 3: Đổi ShiftMode khi chi nhánh còn lịch tương lai xếp bằng bộ ca hiện tại
        // Đếm số lượng ca tương lai (WorkDate >= today)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureSchedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= today && ws.Status != "CANCELLED")
            .ToListAsync();

        int futureAssignmentCount = futureSchedules.Sum(ws => ws.ShiftAssignments.Count(sa => sa.Status != "CANCELLED"));
        if (futureAssignmentCount == 0 && futureSchedules.Count > 0)
        {
            futureAssignmentCount = futureSchedules.Count;
        }

        if (futureAssignmentCount > 0 && !confirm)
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.Conflict(
                "FUTURE_SCHEDULES_EXIST",
                $"Chi nhánh còn {futureAssignmentCount} ca làm việc tương lai đã xếp theo bộ ca hiện tại. Bạn có chắc chắn muốn chuyển chế độ?",
                futureAssignmentCount: futureAssignmentCount);
        }

        var oldValues = new { branch.ShiftMode };
        branch.ShiftMode = normalizedMode;
        branch.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var newValues = new { branch.ShiftMode };

        // 12. Ghi SystemAuditLog
        await LogAuditAsync(
            actorId,
            "UPDATE_BRANCH_SHIFT_MODE",
            "branches",
            branch.Id,
            oldValues,
            newValues,
            ipAddress);

        var effectiveResult = await GetBranchEffectiveShiftsAsync(branchId);
        return ShiftOperationResult<BranchEffectiveShiftsDto>.Ok(
            effectiveResult.Data!,
            $"Đã chuyển chế độ khung ca sang {normalizedMode} thành công.");
    }

    public async Task<List<BranchSelectorItemDto>> GetBranchesForSelectorAsync(string? shiftMode, string? q)
    {
        var query = _context.Branches
            .Include(b => b.ShiftTemplates)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(shiftMode))
        {
            var normalizedMode = shiftMode.Trim().ToUpper();
            query = query.Where(b => b.ShiftMode == normalizedMode);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var queryText = q.Trim().ToLower();
            query = query.Where(b => b.BranchCode.ToLower().Contains(queryText) || b.Name.ToLower().Contains(queryText));
        }

        var branches = await query
            .OrderBy(b => b.BranchCode)
            .ToListAsync();

        return branches.Select(b => new BranchSelectorItemDto
        {
            Id = b.Id,
            Name = b.Name,
            BranchCode = b.BranchCode,
            ShiftMode = b.ShiftMode,
            ActiveCustomShiftCount = b.ShiftTemplates.Count(st => st.Scope == "BRANCH" && st.IsActive)
        }).ToList();
    }

    // ==========================================
    // 1. Quản lý Mẫu Ca Chuẩn (UC 1.3 - Operations Admin)
    // ==========================================

    /// <summary>
    /// Tạo mẫu ca làm việc chuẩn mới áp dụng cho hệ thống chuỗi cửa hàng (UC 1.3).
    /// Nghiệp vụ: Validate start_time != end_time, start_time > end_time => is_overnight = true, unique code.
    /// </summary>
    public async Task<ApiResponse<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TemplateCode))
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Mã mẫu ca (code) không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Tên ca làm việc (name) không được để trống.");
        }

        var normalizedCode = dto.TemplateCode.Trim().ToUpper();
        var codeExists = await _context.ShiftTemplates
            .AnyAsync(st => st.TemplateCode.ToUpper() == normalizedCode);

        if (codeExists)
        {
            return ApiResponse<ShiftTemplateDto>.Fail($"Mã mẫu ca '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        // Validate quy tắc khung giờ
        var validation = ValidateAndCalculateShift(dto.StartTime, dto.EndTime, dto.IsOvernight, dto.BreakMinutes);
        if (!validation.IsValid)
        {
            return ApiResponse<ShiftTemplateDto>.Fail(validation.ErrorMessage ?? "Cấu hình khung ca không hợp lệ.");
        }

        var now = DateTime.UtcNow;
        var template = new ShiftTemplate
        {
            TemplateCode = normalizedCode,
            Name = dto.Name.Trim(),
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            IsOvernight = validation.IsOvernight,
            BreakDurationMinutes = dto.BreakMinutes,
            IsActive = string.IsNullOrWhiteSpace(dto.Status) || dto.Status.Trim().ToUpper() == "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftTemplateDto>.Ok(MapToShiftTemplateDto(template, validation.WorkHours), "Tạo mẫu ca chuẩn thành công.");
    }

    /// <summary>
    /// Cập nhật thông tin chi tiết của một mẫu ca làm việc chuẩn (UC 1.3).
    /// </summary>
    public async Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto)
    {
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Không tìm thấy mẫu ca làm việc cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Tên ca làm việc không được để trống.");
        }

        // Validate quy tắc khung giờ
        var validation = ValidateAndCalculateShift(dto.StartTime, dto.EndTime, dto.IsOvernight, dto.BreakMinutes);
        if (!validation.IsValid)
        {
            return ApiResponse<ShiftTemplateDto>.Fail(validation.ErrorMessage ?? "Cấu hình khung ca không hợp lệ.");
        }

        template.Name = dto.Name.Trim();
        template.StartTime = dto.StartTime;
        template.EndTime = dto.EndTime;
        template.IsOvernight = validation.IsOvernight;
        template.BreakDurationMinutes = dto.BreakMinutes;
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            template.IsActive = dto.Status.Trim().ToUpper() == "ACTIVE";
        }
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ApiResponse<ShiftTemplateDto>.Ok(MapToShiftTemplateDto(template, validation.WorkHours), "Cập nhật mẫu ca thành công.");
    }

    /// <summary>
    /// Cập nhật trạng thái mẫu ca làm việc (ACTIVE / INACTIVE) - Không xóa cứng để bảo toàn lịch sử chấm công (UC 1.3).
    /// </summary>
    public async Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateStatusAsync(uint id, UpdateShiftTemplateStatusDto dto)
    {
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        var normalizedStatus = (dto.Status ?? string.Empty).Trim().ToUpper();
        if (normalizedStatus != "ACTIVE" && normalizedStatus != "INACTIVE")
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Trạng thái không hợp lệ. Chỉ chấp nhận 'ACTIVE' hoặc 'INACTIVE'.");
        }

        template.IsActive = normalizedStatus == "ACTIVE";
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var message = normalizedStatus == "INACTIVE"
            ? "Đã vô hiệu hóa mẫu ca chuẩn (không xóa cứng để bảo toàn lịch sử chấm công)."
            : "Đã kích hoạt lại mẫu ca chuẩn.";

        var validation = ValidateAndCalculateShift(template.StartTime, template.EndTime, template.IsOvernight, template.BreakDurationMinutes);
        return ApiResponse<ShiftTemplateDto>.Ok(MapToShiftTemplateDto(template, validation.WorkHours), message);
    }

    /// <summary>
    /// Lấy thông tin chi tiết mẫu ca chuẩn theo ID.
    /// </summary>
    public async Task<ApiResponse<ShiftTemplateDto>> GetShiftTemplateByIdAsync(uint id)
    {
        var template = await _context.ShiftTemplates
            .Include(st => st.Branch)
            .FirstOrDefaultAsync(st => st.Id == id);

        if (template == null)
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        var branchesUsingGlobalCount = await _context.Branches
            .CountAsync(b => b.ShiftMode == "GLOBAL" && b.Status == "ACTIVE");

        return ApiResponse<ShiftTemplateDto>.Ok(MapEntityToDto(template, branchesUsingGlobalCount), "Lấy thông tin mẫu ca thành công.");
    }

    /// <summary>
    /// Vô hiệu hóa (Soft delete) mẫu ca làm việc chuẩn - Không xóa cứng để bảo toàn lịch sử (UC 1.3).
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id)
    {
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã vô hiệu hóa mẫu ca chuẩn thành công.");
    }

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc mẫu trong hệ thống (hỗ trợ lọc status: ACTIVE / INACTIVE).
    /// </summary>
    public async Task<ApiResponse<List<ShiftTemplateDto>>> GetAllShiftTemplatesAsync(string? status = null)
    {
        var query = _context.ShiftTemplates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var filterStatus = status.Trim().ToUpper();
            bool targetActive = filterStatus == "ACTIVE";
            query = query.Where(st => st.IsActive == targetActive);
        }

        var templates = await query
            .OrderBy(st => st.StartTime)
            .ToListAsync();

        var dtos = templates.Select(st =>
        {
            var validation = ValidateAndCalculateShift(st.StartTime, st.EndTime, st.IsOvernight, st.BreakDurationMinutes);
            return MapToShiftTemplateDto(st, validation.WorkHours);
        }).ToList();

        return ApiResponse<List<ShiftTemplateDto>>.Ok(dtos, "Lấy danh sách mẫu ca chuẩn thành công.");
    }

    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(bool includeInactive = false)
    {
        var query = _context.ShiftTemplates.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(st => st.IsActive);
        }

        var templates = await query
            .OrderBy(st => st.StartTime)
            .ToListAsync();

        var dtos = templates.Select(st =>
        {
            var validation = ValidateAndCalculateShift(st.StartTime, st.EndTime, st.IsOvernight, st.BreakDurationMinutes);
            var baseDto = MapToShiftTemplateDto(st, validation.WorkHours);
            return new ShiftDto
            {
                Id = baseDto.Id,
                TemplateCode = baseDto.TemplateCode,
                Name = baseDto.Name,
                Description = baseDto.Description,
                StartTime = baseDto.StartTime,
                EndTime = baseDto.EndTime,
                BreakMinutes = baseDto.BreakMinutes,
                IsOvernight = baseDto.IsOvernight,
                WorkHours = baseDto.WorkHours,
                Status = baseDto.Status,
                CreatedAt = baseDto.CreatedAt,
                UpdatedAt = baseDto.UpdatedAt
            };
        }).ToList();

        return ApiResponse<List<ShiftDto>>.Ok(dtos);
    }

    private static (bool IsValid, string? ErrorMessage, double WorkHours, bool IsOvernight) ValidateAndCalculateShift(
        TimeOnly startTime, TimeOnly endTime, bool isOvernight, uint breakMinutes)
    {
        if (startTime == endTime)
        {
            return (false, "Giờ bắt đầu (start_time) và giờ kết thúc (end_time) không được trùng nhau.", 0, false);
        }

        // Nếu start_time > end_time thì bắt buộc is_overnight = true (ca đêm qua ngày)
        if (startTime > endTime)
        {
            isOvernight = true;
        }
        else if (isOvernight && startTime < endTime)
        {
            isOvernight = false;
        }

        int startMinutes = startTime.Hour * 60 + startTime.Minute;
        int endMinutes = endTime.Hour * 60 + endTime.Minute;

        int totalDurationMinutes;
        if (isOvernight)
        {
            totalDurationMinutes = (24 * 60 - startMinutes) + endMinutes;
        }
        else
        {
            totalDurationMinutes = endMinutes - startMinutes;
        }

        if (totalDurationMinutes < 60)
        {
            return (false, "Thời lượng ca làm việc tối thiểu phải từ 1 giờ trở lên.", 0, isOvernight);
        }

        if (totalDurationMinutes > 16 * 60)
        {
            return (false, "Thời lượng ca làm việc không được vượt quá 16 giờ.", 0, isOvernight);
        }

        if (breakMinutes >= totalDurationMinutes)
        {
            return (false, $"Thời gian nghỉ ({breakMinutes} phút) không được lớn hơn hoặc bằng tổng thời lượng ca ({totalDurationMinutes} phút).", 0, isOvernight);
        }

        double workHours = Math.Round((totalDurationMinutes - (double)breakMinutes) / 60.0, 2);
        return (true, null, workHours, isOvernight);
    }

    private static ShiftTemplateDto MapEntityToDto(ShiftTemplate st, int? usedByBranchCount = null)
    {
        var (paidHours, nightHours, isOvernight) = ShiftCalculationHelper.CalculateHours(st.StartTime, st.EndTime, st.BreakDurationMinutes);
        return new ShiftTemplateDto
        {
            Id = st.Id,
            TemplateCode = st.TemplateCode,
            Name = st.Name,
            Description = st.Description,
            ShiftType = st.ShiftType ?? ShiftCalculationHelper.InferShiftType(st.StartTime),
            Scope = st.Scope,
            BranchId = st.BranchId,
            BranchName = st.Branch?.Name,
            StartTime = ShiftCalculationHelper.FormatTime(st.StartTime),
            EndTime = ShiftCalculationHelper.FormatTime(st.EndTime),
            BreakDuration = st.BreakDurationMinutes,
            IsOvernight = isOvernight,
            IsActive = st.IsActive,
            PaidHours = paidHours,
            NightHours = nightHours,
            UsedByBranchCount = st.Scope == "GLOBAL" ? usedByBranchCount : null,
            CreatedAt = st.CreatedAt,
            UpdatedAt = st.UpdatedAt
        };
    }

    private static ShiftTemplateDto MapToShiftTemplateDto(ShiftTemplate st, double workHours)
    {
        var dto = MapEntityToDto(st);
        dto.PaidHours = workHours;
        return dto;
    }

    private async Task LogAuditAsync(ulong? actorId, string action, string targetTable, ulong targetId, object? oldValues, object? newValues, string? ipAddress)
    {
        try
        {
            var audit = new SystemAuditLog
            {
                ActorId = actorId ?? 1,
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

    // =========================================================
    // 2. Khởi Tạo Khung Lịch & Định Mức Nhu Cầu (Store Manager & Admin)
    // =========================================================

    /// <summary>
    /// Tự động sinh khung mẫu lịch làm việc thô cho tất cả các ngày trong tháng tại chi nhánh chỉ định.
    /// Logic đặc biệt: Duyệt từng ngày từ 1 đến ngày cuối tháng (28-31 ngày), ghép với danh sách mẫu ca được chọn (hoặc tất cả mẫu ca active).
    /// Bỏ qua các ca ngày đã tồn tại (nếu chạy lại), thiết lập số lượng định mức nhu cầu nhân sự mặc định (DefaultRequiredCashier, DefaultRequiredSales, DefaultRequiredSecurity) ở trạng thái DRAFT.
    /// </summary>
    /// <param name="dto">DTO cấu hình bao gồm BranchId, Year, Month, danh sách TemplateIds và chỉ tiêu số lượng nhân sự từng vị trí</param>
    /// <param name="createdByUserId">ID người dùng thực hiện khởi tạo khung lịch (dùng để ghi vết Audit Log)</param>
    /// <returns>ApiResponse chứa danh sách khung lịch thô của tất cả các ca trong tháng (List&lt;WorkScheduleDto&gt;)</returns>
    public async Task<ApiResponse<List<WorkScheduleDto>>> GenerateMonthlyScheduleAsync(GenerateMonthlyScheduleDto dto, ulong createdByUserId)
    {
        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null)
        {
            return ApiResponse<List<WorkScheduleDto>>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        if (!string.Equals(branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<List<WorkScheduleDto>>.Fail($"Chi nhánh '{branch.Name}' đã bị khóa. Không thể tạo ca làm việc mới.");
        }

        if (dto.Year < 2020 || dto.Month < 1 || dto.Month > 12)
        {
            return ApiResponse<List<WorkScheduleDto>>.Fail("Tháng hoặc năm không hợp lệ.");
        }

        int daysInMonth = DateTime.DaysInMonth(dto.Year, dto.Month);

        List<ShiftTemplate> templates;
        if (dto.TemplateIds != null && dto.TemplateIds.Any())
        {
            templates = await _context.ShiftTemplates
                .Where(st => dto.TemplateIds.Contains(st.Id) && st.IsActive)
                .ToListAsync();
        }
        else
        {
            templates = await _context.ShiftTemplates
                .Where(st => st.IsActive)
                .ToListAsync();
        }

        if (!templates.Any())
        {
            return ApiResponse<List<WorkScheduleDto>>.Fail("Không có mẫu ca nào hoạt động để sinh lịch.");
        }

        var existingSchedules = await _context.WorkSchedules
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate.Year == dto.Year && ws.WorkDate.Month == dto.Month)
            .ToListAsync();

        var existingSet = existingSchedules
            .Select(ws => $"{ws.ShiftTemplateId}_{ws.WorkDate:yyyy-MM-dd}")
            .ToHashSet();

        var newSchedules = new List<WorkSchedule>();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var workDate = new DateOnly(dto.Year, dto.Month, day);
            foreach (var template in templates)
            {
                var key = $"{template.Id}_{workDate:yyyy-MM-dd}";
                if (!existingSet.Contains(key))
                {
                    newSchedules.Add(new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = template.Id,
                        WorkDate = workDate,
                        RequiredCashier = dto.DefaultRequiredCashier,
                        RequiredSales = dto.DefaultRequiredSales,
                        RequiredSecurity = dto.DefaultRequiredSecurity,
                        Status = "DRAFT",
                        CreatedBy = createdByUserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (newSchedules.Any())
        {
            _context.WorkSchedules.AddRange(newSchedules);
            await _context.SaveChangesAsync();
        }

        return await GetMonthlySchedulesAsync(dto.BranchId, dto.Year, dto.Month);
    }

    /// <summary>
    /// Điều chỉnh định mức nhu cầu số lượng nhân sự theo từng vị trí (Thu ngân, Bán hàng, Bảo vệ) cho 1 ca trực.
    /// Logic đặc biệt: Cập nhật định mức RequiredCashier, RequiredSales, RequiredSecurity và tự động đếm số lượng nhân sự đã phân bổ thực tế.
    /// </summary>
    /// <param name="dto">DTO chứa ScheduleId và định mức số lượng nhân sự mới từng vị trí</param>
    /// <returns>ApiResponse chứa thông tin khung ca làm việc đã cập nhật chỉ tiêu (WorkScheduleDto)</returns>
    public async Task<ApiResponse<WorkScheduleDto>> UpdateScheduleRequirementAsync(UpdateScheduleRequirementDto dto)
    {
        var schedule = await _context.WorkSchedules
            .Include(ws => ws.Branch)
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.AssignedRole)
            .FirstOrDefaultAsync(ws => ws.Id == dto.ScheduleId);

        if (schedule == null)
        {
            return ApiResponse<WorkScheduleDto>.Fail("Không tìm thấy ca trực trong lịch làm việc.");
        }

        if (schedule.Branch != null && !string.Equals(schedule.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<WorkScheduleDto>.Fail($"Chi nhánh '{schedule.Branch.Name}' đã bị khóa. Không thể điều chỉnh ca làm việc.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (schedule.WorkDate < today)
        {
            return ApiResponse<WorkScheduleDto>.Fail("Không thể điều chỉnh định mức ca làm việc đã trôi qua trong quá khứ.");
        }

        schedule.RequiredCashier = dto.RequiredCashier;
        schedule.RequiredSales = dto.RequiredSales;
        schedule.RequiredSecurity = dto.RequiredSecurity;

        await _context.SaveChangesAsync();

        int leaderCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SHIFT_LEADER" || sa.AssignedRole.RoleCode == "LEADER"));
        int cashierCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole != null && sa.AssignedRole.RoleCode == "CASHIER");
        int salesCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SALES" || sa.AssignedRole.RoleCode == "SALES_STAFF"));
        int securityCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SECURITY" || sa.AssignedRole.RoleCode == "SECURITY_GUARD"));

        var resultDto = new WorkScheduleDto
        {
            ScheduleId = schedule.Id,
            BranchId = schedule.BranchId,
            BranchName = schedule.Branch.Name,
            ShiftTemplateId = schedule.ShiftTemplateId,
            ShiftTemplateName = schedule.ShiftTemplate.Name,
            StartTime = schedule.ShiftTemplate.StartTime,
            EndTime = schedule.ShiftTemplate.EndTime,
            WorkDate = schedule.WorkDate,
            RequiredLeader = 1,
            RequiredCashier = schedule.RequiredCashier,
            RequiredSales = schedule.RequiredSales,
            RequiredSecurity = schedule.RequiredSecurity,
            AssignedLeaderCount = leaderCount,
            AssignedCashierCount = cashierCount,
            AssignedSalesCount = salesCount,
            AssignedSecurityCount = securityCount,
            Status = schedule.Status
        };

        return ApiResponse<WorkScheduleDto>.Ok(resultDto, "Cập nhật định mức nhu cầu ca thành công.");
    }

    /// <summary>
    /// Truy vấn danh sách khung lịch làm việc kèm chỉ tiêu định mức nhu cầu nhân sự của chi nhánh trong tháng.
    /// Logic đặc biệt: Lọc theo BranchId, Year, Month, sắp xếp tăng dần theo WorkDate và StartTime của ca.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm làm việc (Ví dụ: 2026)</param>
    /// <param name="month">Tháng làm việc (1 - 12)</param>
    /// <returns>ApiResponse chứa danh sách bản ghi WorkScheduleDto kèm số lượng đã gán thực tế</returns>
    public async Task<ApiResponse<List<WorkScheduleDto>>> GetMonthlySchedulesAsync(ulong branchId, int year, int month)
    {
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.Branch)
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.AssignedRole)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate.Year == year && ws.WorkDate.Month == month)
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.ShiftTemplate.StartTime)
            .ToListAsync();

        var result = schedules.Select(ws => new WorkScheduleDto
        {
            ScheduleId = ws.Id,
            BranchId = ws.BranchId,
            BranchName = ws.Branch.Name,
            ShiftTemplateId = ws.ShiftTemplateId,
            ShiftTemplateName = ws.ShiftTemplate.Name,
            StartTime = ws.ShiftTemplate.StartTime,
            EndTime = ws.ShiftTemplate.EndTime,
            WorkDate = ws.WorkDate,
            RequiredCashier = ws.RequiredCashier,
            RequiredSales = ws.RequiredSales,
            RequiredSecurity = ws.RequiredSecurity,
            AssignedCashierCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "CASHIER"),
            AssignedSalesCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "SALES"),
            AssignedSecurityCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "SECURITY"),
            Status = ws.Status
        }).ToList();

        return ApiResponse<List<WorkScheduleDto>>.Ok(result);
    }

    // =========================================================
    // 3. Phân Bổ Nhân Sự & Công Bố Lịch (Store Manager)
    // =========================================================

    /// <summary>
    /// Phân bổ hàng loạt nhân viên Full-time/Part-time vào các ca làm việc trong tháng.
    /// Logic đặc biệt: Tự động kiểm tra xung đột trùng ca (1 nhân viên không thể trực 2 ca cùng 1 ngày ở bất kỳ chi nhánh nào).
    /// Tự động lấy RoleId mặc định của nhân viên để gán vào ca. Nếu ca chưa có bản ghi WorkSchedule sẽ tự tạo tự động. Các mục lỗi sẽ được ghi nhận và trả về danh sách cảnh báo.
    /// </summary>
    /// <param name="dto">DTO chứa BranchId và danh sách các mục phân công (UserId, ShiftTemplateId, WorkDate)</param>
    /// <returns>ApiResponse chứa danh sách các bản ghi gán ca thành công (List&lt;ShiftAssignmentDto&gt;) và thông báo tổng hợp</returns>
    public async Task<ApiResponse<List<ShiftAssignmentDto>>> BatchAssignShiftsAsync(BatchAssignShiftDto dto)
    {
        if (dto.Assignments == null || !dto.Assignments.Any())
        {
            return ApiResponse<List<ShiftAssignmentDto>>.Fail("Danh sách phân bổ ca trực không được rỗng.");
        }

        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null)
        {
            return ApiResponse<List<ShiftAssignmentDto>>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var resultList = new List<ShiftAssignmentDto>();
        var errors = new List<string>();

        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var item in dto.Assignments)
        {
            if (item.WorkDate < today)
            {
                errors.Add($"Ngày {item.WorkDate:dd/MM/yyyy} là ngày trong quá khứ, không thể phân công ca.");
                continue;
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == item.UserId && u.Status == "ACTIVE");

            if (user == null)
            {
                errors.Add($"Không tìm thấy nhân viên ID {item.UserId}.");
                continue;
            }

            // Kiểm tra phân quyền điều động nhân sự (UC 4 & UC 2.1)
            if (user.HomeBranchId != dto.BranchId)
            {
                var incomingDispatch = await _context.TemporaryDispatches
                    .Include(d => d.DispatchEmployees)
                    .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                           && d.TargetBranchId == dto.BranchId 
                                           && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                           && d.StartDate <= item.WorkDate 
                                           && item.WorkDate <= d.EndDate);

                if (incomingDispatch == null)
                {
                    var futureDispatch = await _context.TemporaryDispatches
                        .Include(d => d.DispatchEmployees)
                        .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                               && d.TargetBranchId == dto.BranchId 
                                               && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                               && d.StartDate > item.WorkDate);

                    if (futureDispatch != null)
                    {
                        errors.Add($"NV '{user.FullName}' được điều chuyển từ ngày {futureDispatch.StartDate:dd/MM/yyyy}. Không thể xếp ca trước ngày điều chuyển.");
                    }
                    else
                    {
                        errors.Add($"NV '{user.FullName}' không thuộc chi nhánh và không có lệnh điều động ngày {item.WorkDate:dd/MM/yyyy}.");
                    }
                    continue;
                }
            }
            else
            {
                var outgoingDispatch = await _context.TemporaryDispatches
                    .Include(d => d.TargetBranch)
                    .Include(d => d.DispatchEmployees)
                    .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                           && d.SourceBranchId == dto.BranchId 
                                           && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                           && d.StartDate <= item.WorkDate 
                                           && item.WorkDate <= d.EndDate);

                if (outgoingDispatch != null)
                {
                    errors.Add($"NV '{user.FullName}' đã được điều chuyển sang '{outgoingDispatch.TargetBranch.Name}' ngày {item.WorkDate:dd/MM/yyyy}.");
                    continue;
                }
            }

            var conflict = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .FirstOrDefaultAsync(sa => sa.UserId == item.UserId && sa.Schedule.WorkDate == item.WorkDate);

            if (conflict != null)
            {
                errors.Add($"Nhân viên '{user.FullName}' đã có ca '{conflict.Schedule.ShiftTemplate.Name}' ngày {item.WorkDate:dd/MM/yyyy}.");
                continue;
            }

            var shiftTemplate = await _context.ShiftTemplates.FindAsync(item.ShiftTemplateId);
            if (shiftTemplate == null)
            {
                errors.Add($"Không tìm thấy ca mẫu ID {item.ShiftTemplateId}.");
                continue;
            }

            var schedule = await _context.WorkSchedules
                .FirstOrDefaultAsync(ws => ws.BranchId == dto.BranchId && ws.ShiftTemplateId == item.ShiftTemplateId && ws.WorkDate == item.WorkDate);

            if (schedule == null)
            {
                schedule = new WorkSchedule
                {
                    BranchId = dto.BranchId,
                    ShiftTemplateId = item.ShiftTemplateId,
                    WorkDate = item.WorkDate,
                    RequiredCashier = 1,
                    RequiredSales = 1,
                    RequiredSecurity = 1,
                    Status = "DRAFT",
                    CreatedBy = user.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.WorkSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }

            var assignment = new ShiftAssignment
            {
                ScheduleId = schedule.Id,
                UserId = user.Id,
                AssignedRoleId = user.RoleId,
                AssignmentType = "ASSIGNED",
                Status = schedule.Status == "PUBLISHED" ? "CONFIRMED" : "DRAFT"
            };

            _context.ShiftAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            resultList.Add(new ShiftAssignmentDto
            {
                AssignmentId = (int)assignment.Id,
                ScheduleId = (int)schedule.Id,
                EmployeeId = (int)user.Id,
                EmployeeName = user.FullName,
                EmployeeCode = user.EmployeeCode,
                PositionName = user.Role.RoleName,
                ShiftId = (int)shiftTemplate.Id,
                ShiftName = shiftTemplate.Name,
                StartTime = shiftTemplate.StartTime,
                EndTime = shiftTemplate.EndTime,
                WorkDate = item.WorkDate,
                StoreId = (int)branch.Id,
                StoreName = branch.Name,
                Status = assignment.Status,
                IsDispatched = user.HomeBranchId != dto.BranchId
            });
        }

        string msg = errors.Any()
            ? $"Đã phân bổ thành công {resultList.Count} ca. Có {errors.Count} lỗi bỏ qua."
            : "Đã phân bổ hàng loạt nhân sự vào ca thành công!";

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(resultList, msg);
    }

    /// <summary>
    /// Truy vấn dữ liệu ma trận phân bổ lịch làm việc tháng cho màn hình Store Manager Dashboard.
    /// Logic đặc biệt: Tổng hợp danh sách tất cả nhân viên thuộc chi nhánh (HomeBranchId) và các nhân viên được biệt phái/gán ca tại cửa hàng.
    /// Tạo cấu trúc lưới 2 chiều (Nhân viên x Ngày trong tháng) giúp Quản lý cửa hàng có cái nhìn toàn cảnh về phân bổ nhân sự.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm tra cứu ma trận lịch</param>
    /// <param name="month">Tháng tra cứu ma trận lịch (1 - 12)</param>
    /// <returns>ApiResponse chứa đối tượng MonthlyScheduleMatrixDto gồm dữ liệu khung ca và lưới phân bổ ca từng nhân viên</returns>
    public async Task<ApiResponse<MonthlyScheduleMatrixDto>> GetMonthlyRosterMatrixAsync(ulong branchId, int year, int month)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<MonthlyScheduleMatrixDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        int daysInMonth = DateTime.DaysInMonth(year, month);

        var schedulesResult = await GetMonthlySchedulesAsync(branchId, year, month);
        var schedules = schedulesResult.Data ?? new List<WorkScheduleDto>();

        var assignedUserIds = await _context.ShiftAssignments
            .Where(sa => sa.Schedule.BranchId == branchId && sa.Schedule.WorkDate.Year == year && sa.Schedule.WorkDate.Month == month)
            .Select(sa => sa.UserId)
            .Distinct()
            .ToListAsync();

        var storeUsers = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.HomeBranchId == branchId || assignedUserIds.Contains(u.Id))
            .OrderBy(u => u.Role.Id)
            .ThenBy(u => u.FullName)
            .ToListAsync();

        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.Schedule.BranchId == branchId && sa.Schedule.WorkDate.Year == year && sa.Schedule.WorkDate.Month == month)
            .ToListAsync();

        var employeeRosters = storeUsers.Select(user =>
        {
            var userAssignments = assignments.Where(sa => sa.UserId == user.Id).ToList();

            var rosterCells = userAssignments.Select(sa => new EmployeeRosterCellDto
            {
                Date = sa.Schedule.WorkDate,
                ShiftTemplateId = sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                AssignmentId = sa.Id,
                Status = sa.Status
            }).ToList();

            return new EmployeeMonthlyRosterDto
            {
                UserId = user.Id,
                EmployeeCode = user.EmployeeCode,
                FullName = user.FullName,
                RoleName = user.Role?.RoleName ?? string.Empty,
                RoleCode = user.Role?.RoleCode ?? string.Empty,
                AssignedShifts = rosterCells
            };
        }).ToList();

        var matrix = new MonthlyScheduleMatrixDto
        {
            BranchId = branchId,
            BranchName = branch.Name,
            Year = year,
            Month = month,
            TotalDaysInMonth = daysInMonth,
            Schedules = schedules,
            EmployeeRosters = employeeRosters
        };

        return ApiResponse<MonthlyScheduleMatrixDto>.Ok(matrix);
    }

    /// <summary>
    /// Duyệt và công bố phát hành toàn bộ lịch làm việc trong tháng cho cửa hàng.
    /// Logic đặc biệt: Chuyển trạng thái tất cả WorkSchedule trong tháng từ DRAFT sang PUBLISHED, đồng thời cập nhật toàn bộ ShiftAssignment sang CONFIRMED để nhân viên nhìn thấy trên Mobile app.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm phát hành lịch</param>
    /// <param name="month">Tháng phát hành lịch</param>
    /// <param name="publishedByUserId">Mã ID của Quản lý cửa hàng duyệt phát hành</param>
    /// <returns>ApiResponse trả về kết quả boolean xác nhận công bố thành công</returns>
    public async Task<ApiResponse<bool>> PublishMonthlyScheduleAsync(ulong branchId, int year, int month, ulong publishedByUserId)
    {
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate.Year == year && ws.WorkDate.Month == month)
            .ToListAsync();

        if (!schedules.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy lịch ca tháng {month}/{year} để phát hành.");
        }

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            foreach (var assignment in schedule.ShiftAssignments)
            {
                assignment.Status = "CONFIRMED";
            }
        }

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, $"Đã công bố thành công lịch làm việc tháng {month}/{year} cho cửa hàng!");
    }

    // =========================================================
    // 3b. Quản Lý Lịch Tuần & Xung Đột & Công Bố Tuần (UC 2.1 & UC 2.3)
    // =========================================================

    /// <summary>
    /// Khởi tạo khung mẫu ca cho 7 ngày trong tuần theo định mức mặc định (UC 2.1).
    /// </summary>
    public async Task<ApiResponse<WeeklyScheduleMatrixDto>> GenerateWeeklyScheduleAsync(GenerateWeeklyScheduleDto dto, ulong createdByUserId)
    {
        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null)
        {
            return ApiResponse<WeeklyScheduleMatrixDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var weekEndDate = dto.WeekStartDate.AddDays(6);

        List<ShiftTemplate> templates;
        if (dto.TemplateIds != null && dto.TemplateIds.Any())
        {
            templates = await _context.ShiftTemplates
                .Where(st => dto.TemplateIds.Contains(st.Id) && st.IsActive)
                .ToListAsync();
        }
        else
        {
            templates = await _context.ShiftTemplates
                .Where(st => st.IsActive)
                .ToListAsync();
        }

        if (!templates.Any())
        {
            return ApiResponse<WeeklyScheduleMatrixDto>.Fail("Không có mẫu ca nào hoạt động để sinh lịch tuần.");
        }

        var existingSchedules = await _context.WorkSchedules
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate >= dto.WeekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        var existingSet = existingSchedules
            .Select(ws => $"{ws.ShiftTemplateId}_{ws.WorkDate:yyyy-MM-dd}")
            .ToHashSet();

        var newSchedules = new List<WorkSchedule>();

        for (int i = 0; i < 7; i++)
        {
            var workDate = dto.WeekStartDate.AddDays(i);
            foreach (var template in templates)
            {
                var key = $"{template.Id}_{workDate:yyyy-MM-dd}";
                if (!existingSet.Contains(key))
                {
                    newSchedules.Add(new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = template.Id,
                        WorkDate = workDate,
                        RequiredCashier = dto.DefaultRequiredCashier,
                        RequiredSales = dto.DefaultRequiredSales,
                        RequiredSecurity = dto.DefaultRequiredSecurity,
                        Status = "DRAFT",
                        CreatedBy = createdByUserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (newSchedules.Any())
        {
            _context.WorkSchedules.AddRange(newSchedules);
            await _context.SaveChangesAsync();
        }

        return await GetWeeklyScheduleMatrixAsync(dto.BranchId, dto.WeekStartDate);
    }

    /// <summary>
    /// Lấy ma trận lịch phân bổ ca tuần (7 ngày) kèm chỉ tiêu định mức và danh sách nhân viên (UC 2.1 & UC 2.3).
    /// </summary>
    public async Task<ApiResponse<WeeklyScheduleMatrixDto>> GetWeeklyScheduleMatrixAsync(ulong branchId, DateOnly weekStartDate)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<WeeklyScheduleMatrixDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var weekEndDate = weekStartDate.AddDays(6);
        var days = new List<DateOnly>();
        for (int i = 0; i < 7; i++)
        {
            days.Add(weekStartDate.AddDays(i));
        }

        var schedules = await _context.WorkSchedules
            .Include(ws => ws.Branch)
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.User)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEndDate)
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.ShiftTemplate.StartTime)
            .ToListAsync();

        var scheduleDtos = schedules.Select(ws => new WorkScheduleDto
        {
            ScheduleId = ws.Id,
            BranchId = ws.BranchId,
            BranchName = ws.Branch.Name,
            ShiftTemplateId = ws.ShiftTemplateId,
            ShiftTemplateName = ws.ShiftTemplate.Name,
            StartTime = ws.ShiftTemplate.StartTime,
            EndTime = ws.ShiftTemplate.EndTime,
            WorkDate = ws.WorkDate,
            RequiredLeader = 1,
            RequiredCashier = ws.RequiredCashier,
            RequiredSales = ws.RequiredSales,
            RequiredSecurity = ws.RequiredSecurity,
            AssignedLeaderCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SHIFT_LEADER" || sa.AssignedRole.RoleCode == "LEADER")),
            AssignedCashierCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && sa.AssignedRole.RoleCode == "CASHIER"),
            AssignedSalesCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SALES" || sa.AssignedRole.RoleCode == "SALES_STAFF")),
            AssignedSecurityCount = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SECURITY" || sa.AssignedRole.RoleCode == "SECURITY_GUARD")),
            Status = ws.Status,
            AssignedEmployees = ws.ShiftAssignments.Select(sa => new AssignedEmployeeSummaryDto
            {
                AssignmentId = sa.Id,
                UserId = sa.UserId,
                EmployeeCode = sa.User?.EmployeeCode ?? string.Empty,
                FullName = sa.User?.FullName ?? string.Empty,
                AssignedRoleId = sa.AssignedRoleId,
                RoleCode = sa.AssignedRole?.RoleCode ?? string.Empty,
                RoleName = sa.AssignedRole?.RoleName ?? string.Empty,
                AssignmentType = sa.AssignmentType,
                Status = sa.Status
            }).ToList()
        }).ToList();

        var assignedUserIds = await _context.ShiftAssignments
            .Where(sa => sa.Schedule.BranchId == branchId && sa.Schedule.WorkDate >= weekStartDate && sa.Schedule.WorkDate <= weekEndDate)
            .Select(sa => sa.UserId)
            .Distinct()
            .ToListAsync();

        // Lấy danh sách lệnh điều động nhân sự (Incoming Dispatches và Outgoing Dispatches) trong tuần
        var incomingDispatches = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.DispatchEmployees)
            .Where(d => d.TargetBranchId == branchId 
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                     && d.StartDate <= weekEndDate 
                     && d.EndDate >= weekStartDate)
            .ToListAsync();

        var outgoingDispatches = await _context.TemporaryDispatches
            .Include(d => d.TargetBranch)
            .Include(d => d.DispatchEmployees)
            .Where(d => d.SourceBranchId == branchId 
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                     && d.StartDate <= weekEndDate 
                     && d.EndDate >= weekStartDate)
            .ToListAsync();

        var incomingUserIds = incomingDispatches
            .SelectMany(d => d.DispatchEmployees.Any()
                ? d.DispatchEmployees.Where(de => de.Status == "APPROVED").Select(de => de.UserId)
                : (d.UserId > 0 ? new[] { d.UserId } : Array.Empty<ulong>()))
            .Distinct()
            .ToList();

        var storeUsers = await _context.Users
            .Include(u => u.Role)
            .Where(u => (u.HomeBranchId == branchId || assignedUserIds.Contains(u.Id) || incomingUserIds.Contains(u.Id)) && u.Status == "ACTIVE")
            .OrderBy(u => u.Role.Id)
            .ThenBy(u => u.FullName)
            .ToListAsync();

        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.Schedule.BranchId == branchId && sa.Schedule.WorkDate >= weekStartDate && sa.Schedule.WorkDate <= weekEndDate)
            .ToListAsync();

        var employeeRosters = storeUsers.Select(user =>
        {
            var userAssignments = assignments.Where(sa => sa.UserId == user.Id).ToList();

            var rosterCells = userAssignments.Select(sa => new EmployeeRosterCellDto
            {
                Date = sa.Schedule.WorkDate,
                ShiftTemplateId = sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                AssignmentId = sa.Id,
                Status = sa.Status
            }).ToList();

            var incoming = incomingDispatches.FirstOrDefault(d => d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"));
            var outgoing = outgoingDispatches.FirstOrDefault(d => d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"));

            return new EmployeeMonthlyRosterDto
            {
                UserId = user.Id,
                EmployeeCode = user.EmployeeCode,
                FullName = user.FullName,
                RoleName = user.Role?.RoleName ?? string.Empty,
                RoleCode = user.Role?.RoleCode ?? string.Empty,
                EmploymentType = user.EmploymentType ?? "FULL_TIME",
                IsDispatched = incoming != null,
                DispatchStartDate = incoming?.StartDate,
                DispatchEndDate = incoming?.EndDate,
                OriginBranchId = incoming?.SourceBranchId,
                OriginBranchName = incoming?.SourceBranch?.Name,
                IsDispatchedAway = outgoing != null,
                DispatchAwayStartDate = outgoing?.StartDate,
                DispatchAwayEndDate = outgoing?.EndDate,
                DestinationBranchName = outgoing?.TargetBranch?.Name,
                AssignedShifts = rosterCells
            };
        }).ToList();

        string weekStatus = "DRAFT";
        if (schedules.Any())
        {
            if (schedules.All(s => s.Status == "PUBLISHED"))
                weekStatus = "PUBLISHED";
            else if (schedules.Any(s => s.Status == "PUBLISHED"))
                weekStatus = "PARTIAL";
        }

        var activeTemplatesRes = await GetAllShiftTemplatesAsync(false);

        var matrix = new WeeklyScheduleMatrixDto
        {
            BranchId = branchId,
            BranchName = branch.Name,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            WeekStatus = weekStatus,
            Days = days,
            Schedules = scheduleDtos,
            EmployeeRosters = employeeRosters,
            ActiveTemplates = activeTemplatesRes.Data ?? new List<ShiftDto>()
        };

        return ApiResponse<WeeklyScheduleMatrixDto>.Ok(matrix);
    }

    /// <summary>
    /// Phân bổ nhanh danh sách nhân viên Full-time vào ca trực trong tuần (UC 2.1).
    /// Tự động chặn trùng giờ/trùng ngày ở bất kỳ chi nhánh nào.
    /// </summary>
    public async Task<ApiResponse<List<ShiftAssignmentDto>>> AssignFullTimeBatchAsync(AssignFullTimeBatchDto dto)
    {
        if (dto.UserIds == null || !dto.UserIds.Any())
        {
            return ApiResponse<List<ShiftAssignmentDto>>.Fail("Vui lòng chọn ít nhất một nhân viên.");
        }

        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null) return ApiResponse<List<ShiftAssignmentDto>>.Fail("Không tìm thấy chi nhánh.");

        var shiftTemplate = await _context.ShiftTemplates.FindAsync(dto.ShiftTemplateId);
        if (shiftTemplate == null) return ApiResponse<List<ShiftAssignmentDto>>.Fail("Không tìm thấy mẫu ca làm việc.");
        if (!shiftTemplate.IsActive) return ApiResponse<List<ShiftAssignmentDto>>.Fail($"Mẫu ca '{shiftTemplate.Name}' đã bị Quản trị viên vô hiệu hóa (INACTIVE), không thể phân công.");

        var daysOfWeek = (dto.DaysOfWeek != null && dto.DaysOfWeek.Any()) 
            ? dto.DaysOfWeek 
            : new List<int> { 1, 2, 3, 4, 5, 6 }; // Default Thứ 2 -> Thứ 7

        var resultList = new List<ShiftAssignmentDto>();
        var errors = new List<string>();

        foreach (var userId in dto.UserIds)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && u.Status == "ACTIVE");

            if (user == null)
            {
                errors.Add($"Không tìm thấy nhân viên ID {userId}.");
                continue;
            }

            foreach (var dayNum in daysOfWeek)
            {
                if (dayNum < 1 || dayNum > 7) continue;
                var workDate = dto.WeekStartDate.AddDays(dayNum - 1);

                if (workDate < DateOnly.FromDateTime(DateTime.Today))
                {
                    errors.Add($"Ngày {workDate:dd/MM/yyyy} là ngày trong quá khứ, không thể phân công ca.");
                    continue;
                }

                // Kiểm tra điều động nhân sự (UC 4 & UC 2.1)
                if (user.HomeBranchId != dto.BranchId)
                {
                    var incomingDispatch = await _context.TemporaryDispatches
                        .Include(d => d.DispatchEmployees)
                        .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                               && d.TargetBranchId == dto.BranchId 
                                               && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                               && d.StartDate <= workDate 
                                               && workDate <= d.EndDate);

                    if (incomingDispatch == null)
                    {
                        var futureDispatch = await _context.TemporaryDispatches
                            .Include(d => d.DispatchEmployees)
                            .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                                   && d.TargetBranchId == dto.BranchId 
                                                   && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                                   && d.StartDate > workDate);

                        if (futureDispatch != null)
                        {
                            errors.Add($"NV '{user.FullName}' được điều chuyển từ ngày {futureDispatch.StartDate:dd/MM}. Không thể xếp ca ngày {workDate:dd/MM}.");
                        }
                        else
                        {
                            errors.Add($"NV '{user.FullName}' không thuộc chi nhánh và không có lệnh điều động ngày {workDate:dd/MM}.");
                        }
                        continue;
                    }
                }
                else
                {
                    var outgoingDispatch = await _context.TemporaryDispatches
                        .Include(d => d.TargetBranch)
                        .Include(d => d.DispatchEmployees)
                        .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                               && d.SourceBranchId == dto.BranchId 
                                               && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                               && d.StartDate <= workDate 
                                               && workDate <= d.EndDate);

                    if (outgoingDispatch != null)
                    {
                        errors.Add($"NV '{user.FullName}' đã được điều chuyển sang '{outgoingDispatch.TargetBranch.Name}' ngày {workDate:dd/MM}.");
                        continue;
                    }
                }

                // Tự động kiểm tra xung đột trùng ca trong ngày
                var conflict = await _context.ShiftAssignments
                    .Include(sa => sa.Schedule)
                        .ThenInclude(s => s.ShiftTemplate)
                    .Include(sa => sa.Schedule)
                        .ThenInclude(s => s.Branch)
                    .FirstOrDefaultAsync(sa => sa.UserId == user.Id && sa.Schedule.WorkDate == workDate);

                if (conflict != null)
                {
                    errors.Add($"NV '{user.FullName}' đã có ca '{conflict.Schedule.ShiftTemplate.Name}' ngày {workDate:dd/MM} tại '{conflict.Schedule.Branch.Name}'. Tự động chặn gán trùng!");
                    continue;
                }

                // Tìm hoặc tạo WorkSchedule
                var schedule = await _context.WorkSchedules
                    .FirstOrDefaultAsync(ws => ws.BranchId == dto.BranchId 
                                            && ws.ShiftTemplateId == dto.ShiftTemplateId 
                                            && ws.WorkDate == workDate);

                if (schedule == null)
                {
                    schedule = new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = dto.ShiftTemplateId,
                        WorkDate = workDate,
                        RequiredCashier = 1,
                        RequiredSales = 2,
                        RequiredSecurity = 1,
                        Status = "DRAFT",
                        CreatedBy = user.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.WorkSchedules.Add(schedule);
                    await _context.SaveChangesAsync();
                }

                var assignment = new ShiftAssignment
                {
                    ScheduleId = schedule.Id,
                    UserId = user.Id,
                    AssignedRoleId = user.RoleId,
                    AssignmentType = "ASSIGNED",
                    Status = schedule.Status == "PUBLISHED" ? "CONFIRMED" : "DRAFT"
                };

                _context.ShiftAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                resultList.Add(new ShiftAssignmentDto
                {
                    AssignmentId = (int)assignment.Id,
                    ScheduleId = (int)schedule.Id,
                    EmployeeId = (int)user.Id,
                    EmployeeName = user.FullName,
                    EmployeeCode = user.EmployeeCode,
                    PositionName = user.Role.RoleName,
                    ShiftId = (int)shiftTemplate.Id,
                    ShiftName = shiftTemplate.Name,
                    StartTime = shiftTemplate.StartTime,
                    EndTime = shiftTemplate.EndTime,
                    WorkDate = workDate,
                    StoreId = (int)branch.Id,
                    StoreName = branch.Name,
                    Status = assignment.Status,
                    IsDispatched = user.HomeBranchId != dto.BranchId
                });
            }
        }

        string msg = errors.Any()
            ? $"Đã phân bổ thành công {resultList.Count} lượt ca. Bỏ qua {errors.Count} lượt trùng lịch."
            : $"Đã phân bổ thành công {resultList.Count} ca cho nhân viên Full-time!";

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(resultList, msg);
    }

    /// <summary>
    /// Rà soát xung đột và kiểm tra tình trạng đủ/thiếu định mức trước khi công bố lịch tuần (UC 2.3).
    /// </summary>
    public async Task<ApiResponse<ScheduleConflictCheckResultDto>> CheckWeeklyConflictsAsync(ulong branchId, DateOnly weekStartDate)
    {
        var weekEndDate = weekStartDate.AddDays(6);
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.AssignedRole)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        var issues = new List<string>();
        int understaffedCount = 0;
        int totalAssignments = 0;

        foreach (var ws in schedules)
        {
            totalAssignments += ws.ShiftAssignments.Count;
            int leaders = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SHIFT_LEADER" || sa.AssignedRole.RoleCode == "LEADER"));
            int cashiers = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && sa.AssignedRole.RoleCode == "CASHIER");
            int sales = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SALES" || sa.AssignedRole.RoleCode == "SALES_STAFF"));
            int security = ws.ShiftAssignments.Count(sa => sa.AssignedRole != null && (sa.AssignedRole.RoleCode == "SECURITY" || sa.AssignedRole.RoleCode == "SECURITY_GUARD"));

            var missingRoles = new List<string>();
            int requiredLeader = 1;
            if (leaders < requiredLeader) missingRoles.Add($"Thiếu {requiredLeader - leaders} Trưởng ca trực");
            if (cashiers < ws.RequiredCashier) missingRoles.Add($"Thiếu {ws.RequiredCashier - cashiers} Thu ngân");
            if (sales < ws.RequiredSales) missingRoles.Add($"Thiếu {ws.RequiredSales - sales} Nhân viên bán hàng");
            if (security < ws.RequiredSecurity) missingRoles.Add($"Thiếu {ws.RequiredSecurity - security} Bảo vệ");

            if (missingRoles.Any())
            {
                understaffedCount++;
                issues.Add($"Ngày {ws.WorkDate:dd/MM} ({ws.ShiftTemplate.Name}): {string.Join(", ", missingRoles)} (Chỉ tiêu: {requiredLeader} Trưởng ca, {ws.RequiredCashier} TN, {ws.RequiredSales} BH, {ws.RequiredSecurity} BV).");
            }
        }

        bool isReady = issues.Count == 0;
        string summary = isReady 
            ? "Tất cả các ca trong tuần đã đủ định mức nhân sự và không phát hiện xung đột. Sẵn sàng công bố!"
            : $"Có {understaffedCount} ca trực chưa đáp ứng đủ định mức nhân sự tối thiểu.";

        return ApiResponse<ScheduleConflictCheckResultDto>.Ok(new ScheduleConflictCheckResultDto
        {
            HasConflicts = false,
            TotalAssignments = totalAssignments,
            UnderstaffedShiftsCount = understaffedCount,
            Issues = issues,
            IsReadyToPublish = isReady,
            SummaryMessage = summary
        });
    }

    /// <summary>
    /// Store Manager duyệt và công bố phát hành lịch tuần (UC 2.3).
    /// Chuyển trạng thái sang PUBLISHED và CONFIRMED.
    /// </summary>
    public async Task<ApiResponse<bool>> PublishWeeklyScheduleAsync(ulong branchId, DateOnly weekStartDate, ulong publishedByUserId)
    {
        var weekEndDate = weekStartDate.AddDays(6);
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        if (!schedules.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy lịch ca tuần từ {weekStartDate:dd/MM/yyyy} để công bố.");
        }

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            foreach (var assignment in schedule.ShiftAssignments)
            {
                assignment.Status = "CONFIRMED";
            }
        }

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, $"Đã công bố thành công lịch làm việc tuần từ {weekStartDate:dd/MM/yyyy} đến {weekEndDate:dd/MM/yyyy}! Hệ thống đã phát thông báo tới nhân viên.");
    }

    /// <summary>
    /// Xóa/Hủy 1 phân công ca làm việc của nhân viên.
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteShiftAssignmentAsync(ulong assignmentId)
    {
        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
            .FirstOrDefaultAsync(sa => sa.Id == assignmentId);

        if (assignment == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy bản ghi phân công ca.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (assignment.Schedule != null && assignment.Schedule.WorkDate < today)
        {
            return ApiResponse<bool>.Fail("Không thể hủy/xóa phân công ca làm việc đã trôi qua trong quá khứ.");
        }

        // Xử lý các yêu cầu đổi ca liên quan trước khi xóa phân công ca (tránh lỗi khóa ngoại)
        var relatedSwapRequests = await _context.ShiftSwapRequests
            .Where(r => r.RequestingAssignmentId == assignmentId || r.TargetAssignmentId == assignmentId)
            .ToListAsync();

        foreach (var req in relatedSwapRequests)
        {
            if (req.RequestingAssignmentId == assignmentId)
            {
                req.RequesterUserId ??= assignment.UserId;
                req.ScheduleId ??= assignment.ScheduleId;
                req.RequestingAssignmentId = null;
                if (req.Status == "PENDING")
                {
                    req.Status = "CANCELLED";
                }
            }
            if (req.TargetAssignmentId == assignmentId)
            {
                req.TargetAssignmentId = null;
                if (req.Status == "PENDING")
                {
                    req.Status = "CANCELLED";
                }
            }
        }

        _context.ShiftAssignments.Remove(assignment);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã hủy phân công ca làm việc thành công.");
    }

    /// <summary>
    /// Tự động giải và xếp lịch ca tuần tối ưu bằng Google OR-Tools Constraint Programming Solver (UC 2.1).
    /// </summary>
    public async Task<ApiResponse<AutoScheduleResultDto>> AutoScheduleWeeklyAsync(AutoScheduleWeeklyDto dto, ulong userId)
    {
        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null) return ApiResponse<AutoScheduleResultDto>.Fail("Không tìm thấy chi nhánh.");

        var weekEndDate = dto.WeekStartDate.AddDays(6);

        // 1. Lấy danh sách mẫu ca chuẩn đang hoạt động
        var templates = await _context.ShiftTemplates
            .Where(st => st.IsActive)
            .OrderBy(st => st.StartTime)
            .ToListAsync();

        if (!templates.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Không có mẫu ca nào hoạt động trong hệ thống.");
        }

        // 2. Đảm bảo đầy đủ khung ca cho toàn bộ 7 ngày trong tuần
        var existingSchedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate >= dto.WeekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        var existingKeys = existingSchedules.Select(ws => (ws.WorkDate, ws.ShiftTemplateId)).ToHashSet();
        var toAdd = new List<WorkSchedule>();

        for (int d = 0; d < 7; d++)
        {
            var workDate = dto.WeekStartDate.AddDays(d);
            foreach (var template in templates)
            {
                if (!existingKeys.Contains((workDate, template.Id)))
                {
                    toAdd.Add(new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = template.Id,
                        WorkDate = workDate,
                        RequiredCashier = 1,
                        RequiredSales = 1,
                        RequiredSecurity = 1,
                        Status = "DRAFT",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (toAdd.Any())
        {
            _context.WorkSchedules.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate >= dto.WeekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        // 2. Lấy danh sách nhân viên chi nhánh (bao gồm nhân sự được điều động đến trong tuần)
        var incomingDispatches = await _context.TemporaryDispatches
            .Include(d => d.DispatchEmployees)
            .Where(d => d.TargetBranchId == dto.BranchId 
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                     && d.StartDate <= weekEndDate 
                     && d.EndDate >= dto.WeekStartDate)
            .ToListAsync();

        var outgoingDispatches = await _context.TemporaryDispatches
            .Include(d => d.DispatchEmployees)
            .Where(d => d.SourceBranchId == dto.BranchId 
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                     && d.StartDate <= weekEndDate 
                     && d.EndDate >= dto.WeekStartDate)
            .ToListAsync();

        var incomingUserIds = incomingDispatches
            .SelectMany(d => d.DispatchEmployees.Any()
                ? d.DispatchEmployees.Where(de => de.Status == "APPROVED").Select(de => de.UserId)
                : (d.UserId > 0 ? new[] { d.UserId } : Array.Empty<ulong>()))
            .Distinct()
            .ToList();

        var employees = await _context.Users
            .Include(u => u.Role)
            .Where(u => (u.HomeBranchId == dto.BranchId || incomingUserIds.Contains(u.Id)) && u.Status == "ACTIVE")
            .ToListAsync();

        if (!employees.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Không có nhân viên active tại chi nhánh để phân bổ.");
        }

        // Xây dựng tập ngày khả dụng cho từng nhân viên (đảm bảo nhân sự điều chuyển chỉ được xếp từ ngày bắt đầu đến kết thúc điều chuyển)
        var employeeAvailableDates = new Dictionary<ulong, HashSet<DateOnly>>();
        for (int d = 0; d < 7; d++)
        {
            var workDate = dto.WeekStartDate.AddDays(d);
            foreach (var emp in employees)
            {
                if (!employeeAvailableDates.ContainsKey(emp.Id))
                {
                    employeeAvailableDates[emp.Id] = new HashSet<DateOnly>();
                }

                var inDisp = incomingDispatches.FirstOrDefault(x => x.UserId == emp.Id);
                if (inDisp != null)
                {
                    // Nhân sự điều chuyển đến: Chỉ khả dụng từ ngày bắt đầu đến ngày kết thúc điều chuyển
                    if (workDate >= inDisp.StartDate && workDate <= inDisp.EndDate)
                    {
                        employeeAvailableDates[emp.Id].Add(workDate);
                    }
                    continue;
                }

                var outDisp = outgoingDispatches.FirstOrDefault(x => x.UserId == emp.Id && x.StartDate <= workDate && workDate <= x.EndDate);
                if (outDisp != null)
                {
                    // Ngày này đang làm việc ở cơ sở khác, không khả dụng ở đây
                    continue;
                }

                employeeAvailableDates[emp.Id].Add(workDate);
            }
        }

        // 3. Nếu cho phép ghi đè, xóa các phân công cũ trong tuần
        if (dto.OverwriteExisting)
        {
            var oldAssignments = await _context.ShiftAssignments
                .Where(sa => sa.Schedule.BranchId == dto.BranchId && sa.Schedule.WorkDate >= dto.WeekStartDate && sa.Schedule.WorkDate <= weekEndDate)
                .ToListAsync();

            if (oldAssignments.Any())
            {
                var oldAssignmentIds = oldAssignments.Select(sa => sa.Id).ToList();
                var relatedSwapRequests = await _context.ShiftSwapRequests
                    .Where(r => (r.RequestingAssignmentId != null && oldAssignmentIds.Contains(r.RequestingAssignmentId.Value))
                             || (r.TargetAssignmentId != null && oldAssignmentIds.Contains(r.TargetAssignmentId.Value)))
                    .ToListAsync();

                foreach (var req in relatedSwapRequests)
                {
                    if (req.RequestingAssignmentId.HasValue && oldAssignmentIds.Contains(req.RequestingAssignmentId.Value))
                    {
                        req.RequestingAssignmentId = null;
                        if (req.Status == "PENDING") req.Status = "CANCELLED";
                    }
                    if (req.TargetAssignmentId.HasValue && oldAssignmentIds.Contains(req.TargetAssignmentId.Value))
                    {
                        req.TargetAssignmentId = null;
                        if (req.Status == "PENDING") req.Status = "CANCELLED";
                    }
                }

                _context.ShiftAssignments.RemoveRange(oldAssignments);
                await _context.SaveChangesAsync();
            }
        }

        // 5. Chạy solver Google OR-Tools CP-SAT
        var solver = new ShiftSchedulerSolver();
        var solverResult = solver.Solve(new ShiftSchedulerSolver.SolverInput
        {
            BranchId = dto.BranchId,
            WeekStartDate = dto.WeekStartDate,
            Employees = employees,
            Templates = templates,
            Schedules = schedules,
            MaxShiftsPerWeekPerEmployee = dto.MaxShiftsPerWeekPerEmployee,
            MinShiftsPerWeekForFullTime = dto.MinShiftsPerWeekForFullTime,
            EmployeeAvailableDates = employeeAvailableDates
        });

        if (!solverResult.IsSuccess)
        {
            return ApiResponse<AutoScheduleResultDto>.Fail(solverResult.StatusMessage);
        }

        // 6. Lưu các phân bổ tối ưu vào cơ sở dữ liệu
        var scheduleMap = schedules.ToDictionary(s => (s.ShiftTemplateId, s.WorkDate), s => s);
        var userMap = employees.ToDictionary(u => u.Id, u => u);

        var newAssignments = new List<ShiftAssignment>();
        foreach (var item in solverResult.RecommendedAssignments)
        {
            if (scheduleMap.TryGetValue((item.ShiftTemplateId, item.WorkDate), out var schedule) &&
                userMap.TryGetValue(item.UserId, out var emp))
            {
                newAssignments.Add(new ShiftAssignment
                {
                    ScheduleId = schedule.Id,
                    UserId = emp.Id,
                    AssignedRoleId = emp.RoleId,
                    AssignmentType = "ASSIGNED",
                    Status = schedule.Status == "PUBLISHED" ? "CONFIRMED" : "DRAFT"
                });
            }
        }

        if (newAssignments.Any())
        {
            _context.ShiftAssignments.AddRange(newAssignments);
            await _context.SaveChangesAsync();
        }

        var matrixRes = await GetWeeklyScheduleMatrixAsync(dto.BranchId, dto.WeekStartDate);

        return ApiResponse<AutoScheduleResultDto>.Ok(new AutoScheduleResultDto
        {
            Success = true,
            Message = $"{solverResult.StatusMessage} Đã tự động xếp thành công {newAssignments.Count} lượt ca.",
            TotalAssignmentsCreated = newAssignments.Count,
            Matrix = matrixRes.Data
        }, "Tự động xếp lịch ca thành công với Google OR-Tools!");
    }



    /// <summary>
    /// Lấy danh sách cơ sở/chi nhánh mà tài khoản được quyền lập lịch ca tuần (Branch Isolation).
    /// </summary>
    public async Task<ApiResponse<AccessibleBranchesDto>> GetAccessibleBranchesAsync(ulong userId, string role, ulong? storeId)
    {
        var normalizedRole = (role ?? string.Empty).Trim().ToUpperInvariant();
        bool isGlobal = normalizedRole == "OPERATIONS_ADMIN" || 
                        normalizedRole == "BUSINESS_OWNER" || 
                        normalizedRole == "OPERATIONSADMIN" || 
                        normalizedRole == "BUSINESSOWNER" || 
                        normalizedRole == "ADMIN";

        // Query active branches
        var allActiveBranches = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.Id)
            .ToListAsync();

        if (isGlobal)
        {
            var branches = allActiveBranches.Select(b => new AccessibleBranchItemDto
            {
                Id = b.Id,
                BranchCode = b.BranchCode,
                Name = b.Name,
                Address = b.Address,
                Status = b.Status,
                Tier = (int)b.BranchTier
            }).ToList();

            return ApiResponse<AccessibleBranchesDto>.Ok(new AccessibleBranchesDto
            {
                IsGlobalManager = true,
                AssignedBranchId = null,
                AssignedBranchName = null,
                AssignedBranchCode = null,
                Branches = branches
            });
        }

        // Store Manager or Shift Leader:
        // First, if storeId was null or 0, fallback to user in DB
        ulong? assignedId = storeId;
        if (!assignedId.HasValue || assignedId.Value == 0)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            assignedId = user?.HomeBranchId;
        }

        if (!assignedId.HasValue || assignedId.Value == 0)
        {
            return ApiResponse<AccessibleBranchesDto>.Fail("Tài khoản quản lý chưa được phân công cơ sở chi nhánh cụ thể. Vui lòng liên hệ Quản trị viên.");
        }

        var assignedBranch = allActiveBranches.FirstOrDefault(b => b.Id == assignedId.Value)
            ?? await _context.Branches.FirstOrDefaultAsync(b => b.Id == assignedId.Value);

        if (assignedBranch == null)
        {
            return ApiResponse<AccessibleBranchesDto>.Fail($"Cơ sở chi nhánh được phân công (ID: {assignedId}) không tồn tại trong hệ thống.");
        }

        var singleBranchList = new List<AccessibleBranchItemDto>
        {
            new AccessibleBranchItemDto
            {
                Id = assignedBranch.Id,
                BranchCode = assignedBranch.BranchCode,
                Name = assignedBranch.Name,
                Address = assignedBranch.Address,
                Status = assignedBranch.Status,
                Tier = (int)assignedBranch.BranchTier
            }
        };

        return ApiResponse<AccessibleBranchesDto>.Ok(new AccessibleBranchesDto
        {
            IsGlobalManager = false,
            AssignedBranchId = assignedBranch.Id,
            AssignedBranchName = assignedBranch.Name,
            AssignedBranchCode = assignedBranch.BranchCode,
            Branches = singleBranchList
        });
    }

    /// <summary>
    /// Lấy BranchId của phân công ca trực để kiểm tra phân quyền sở hữu.
    /// </summary>
    public async Task<ulong?> GetBranchIdByAssignmentIdAsync(ulong assignmentId)
    {
        return await _context.ShiftAssignments
            .Where(sa => sa.Id == assignmentId)
            .Select(sa => (ulong?)sa.Schedule.BranchId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Lấy BranchId của khung lịch ca để kiểm tra phân quyền sở hữu.
    /// </summary>
    public async Task<ulong?> GetBranchIdByScheduleIdAsync(ulong scheduleId)
    {
        return await _context.WorkSchedules
            .Where(ws => ws.Id == scheduleId)
            .Select(ws => (ulong?)ws.BranchId)
            .FirstOrDefaultAsync();
    }

    // ==========================================
    // 4. Các Phương Thức Tương Thích Hiện Có
    // ==========================================



    /// <summary>
    /// Lấy danh sách mẫu ca làm việc active cho client.
    /// </summary>
    /// <returns>ApiResponse chứa danh sách ShiftDto</returns>
    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync()
    {
        return await GetAllShiftTemplatesAsync(includeInactive: false);
    }

    /// <summary>
    /// Lấy lịch phân công nhân sự theo khoảng thời gian từ startDate đến endDate.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="startDate">Ngày bắt đầu tra cứu</param>
    /// <param name="endDate">Ngày kết thúc tra cứu</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto</returns>
    public async Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Where(sa => sa.Schedule.BranchId == (ulong)storeId && sa.Schedule.WorkDate >= startDate && sa.Schedule.WorkDate <= endDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                EmployeeId = (int)sa.UserId,
                EmployeeName = sa.User.FullName,
                EmployeeCode = sa.User.EmployeeCode,
                PositionName = sa.User.Role.RoleName,
                ShiftId = (int)sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                WorkDate = sa.Schedule.WorkDate,
                StoreId = (int)sa.Schedule.BranchId,
                StoreName = sa.Schedule.Branch.Name,
                Status = sa.Status,
                IsDispatched = sa.User.HomeBranchId != (ulong)storeId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    /// <summary>
    /// Phân công 1 ca lẻ cho nhân viên.
    /// </summary>
    /// <param name="request">DTO chứa thông tin gán ca đơn lẻ</param>
    /// <returns>ApiResponse chứa ShiftAssignmentDto thành công</returns>
    public async Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.WorkDate < today)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail("Không thể phân công ca làm việc cho ngày đã trôi qua trong quá khứ.");
        }

        var conflict = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.EmployeeId && sa.Schedule.WorkDate == request.WorkDate);

        if (conflict != null)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail(
                $"Nhân viên đã được xếp ca '{conflict.Schedule.ShiftTemplate.Name}' tại '{conflict.Schedule.Branch.Name}' ngày {request.WorkDate:dd/MM/yyyy}. Hệ thống tự động chặn gán trùng!");
        }

        var shiftTemplate = await _context.ShiftTemplates.FindAsync((uint)request.ShiftId);
        if (shiftTemplate == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy khung ca làm việc.");
        if (!shiftTemplate.IsActive) return ApiResponse<ShiftAssignmentDto>.Fail($"Mẫu ca '{shiftTemplate.Name}' đã bị Quản trị viên vô hiệu hóa (INACTIVE), không thể phân công.");

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId);
        if (user == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy nhân viên.");

        var branch = await _context.Branches.FindAsync((ulong)request.StoreId);
        if (branch == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy cửa hàng.");

        // Kiểm tra phân quyền điều động nhân sự (UC 4 & UC 2.1)
        ulong targetBranchId = (ulong)request.StoreId;
        if (user.HomeBranchId != targetBranchId)
        {
            var incomingDispatch = await _context.TemporaryDispatches
                .Include(d => d.SourceBranch)
                .Include(d => d.DispatchEmployees)
                .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                       && d.TargetBranchId == targetBranchId 
                                       && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                       && d.StartDate <= request.WorkDate 
                                       && request.WorkDate <= d.EndDate);

            if (incomingDispatch == null)
            {
                // Kiểm tra xem có lệnh điều chuyển nhưng chưa đến ngày bắt đầu không
                var futureDispatch = await _context.TemporaryDispatches
                    .Include(d => d.DispatchEmployees)
                    .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                           && d.TargetBranchId == targetBranchId 
                                           && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                           && d.StartDate > request.WorkDate);

                if (futureDispatch != null)
                {
                    return ApiResponse<ShiftAssignmentDto>.Fail(
                        $"Nhân viên '{user.FullName}' được điều chuyển từ ngày {futureDispatch.StartDate:dd/MM/yyyy}. Chỉ có thể xếp lịch cho nhân sự từ ngày điều chuyển trở đi!");
                }

                return ApiResponse<ShiftAssignmentDto>.Fail(
                    $"Nhân viên '{user.FullName}' không thuộc biên chế chi nhánh này và không có lệnh điều động hợp lệ vào ngày {request.WorkDate:dd/MM/yyyy}.");
            }
        }
        else
        {
            // Nhân sự thuộc chi nhánh gốc: Kiểm tra xem ngày này có bị điều chuyển đi cơ sở khác không
            var outgoingDispatch = await _context.TemporaryDispatches
                .Include(d => d.TargetBranch)
                .Include(d => d.DispatchEmployees)
                .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                       && d.SourceBranchId == targetBranchId 
                                       && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                       && d.StartDate <= request.WorkDate 
                                       && request.WorkDate <= d.EndDate);

            if (outgoingDispatch != null)
            {
                return ApiResponse<ShiftAssignmentDto>.Fail(
                    $"Nhân viên '{user.FullName}' đã được điều chuyển sang '{outgoingDispatch.TargetBranch.Name}' từ ngày {outgoingDispatch.StartDate:dd/MM/yyyy} đến {outgoingDispatch.EndDate:dd/MM/yyyy}. Không thể xếp lịch tại cơ sở gốc trong thời gian này!");
            }
        }

        var schedule = await _context.WorkSchedules
            .FirstOrDefaultAsync(ws => ws.BranchId == (ulong)request.StoreId 
                                    && ws.ShiftTemplateId == (uint)request.ShiftId 
                                    && ws.WorkDate == request.WorkDate);

        if (schedule == null)
        {
            schedule = new WorkSchedule
            {
                BranchId = (ulong)request.StoreId,
                ShiftTemplateId = (uint)request.ShiftId,
                WorkDate = request.WorkDate,
                RequiredCashier = 1,
                RequiredSales = 1,
                RequiredSecurity = 1,
                Status = "PUBLISHED",
                CreatedBy = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
        }

        var assignment = new ShiftAssignment
        {
            ScheduleId = schedule.Id,
            UserId = (ulong)request.EmployeeId,
            AssignedRoleId = user.RoleId,
            AssignmentType = "ASSIGNED",
            Status = "CONFIRMED"
        };

        _context.ShiftAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftAssignmentDto>.Ok(new ShiftAssignmentDto
        {
            AssignmentId = (int)assignment.Id,
            ScheduleId = (int)schedule.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PositionName = user.Role.RoleName,
            ShiftId = (int)shiftTemplate.Id,
            ShiftName = shiftTemplate.Name,
            StartTime = shiftTemplate.StartTime,
            EndTime = shiftTemplate.EndTime,
            WorkDate = request.WorkDate,
            StoreId = (int)branch.Id,
            StoreName = branch.Name,
            Status = assignment.Status,
            IsDispatched = user.HomeBranchId != (ulong)request.StoreId
        }, "Gán ca làm việc thành công.");
    }

    /// <summary>
    /// Phát hành lịch làm việc theo tuần cho cửa hàng.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="weekStartDate">Ngày bắt đầu tuần (Thứ Hai)</param>
    /// <param name="publishedByEmployeeId">ID người duyệt phát hành</param>
    /// <returns>ApiResponse trả về boolean kết quả</returns>
    public async Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId)
    {
        var weekEnd = weekStartDate.AddDays(6);
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == (ulong)storeId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEnd)
            .ToListAsync();

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            foreach (var assignment in schedule.ShiftAssignments)
            {
                assignment.Status = "CONFIRMED";
            }
        }

        await _context.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, $"Đã công bố lịch làm việc tuần {weekStartDate:dd/MM/yyyy} thành công!");
    }

    /// <summary>
    /// Lấy danh sách ca làm việc cá nhân của một nhân viên trong khoảng thời gian.
    /// </summary>
    /// <param name="employeeId">ID nhân viên</param>
    /// <param name="startDate">Ngày bắt đầu</param>
    /// <param name="endDate">Ngày kết thúc</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto cá nhân</returns>
    public async Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate)
    {
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Include(sa => sa.User)
                .ThenInclude(u => u.Role)
            .Where(sa => sa.UserId == (ulong)employeeId && sa.Schedule.WorkDate >= startDate && sa.Schedule.WorkDate <= endDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                EmployeeId = (int)sa.UserId,
                EmployeeName = sa.User.FullName,
                EmployeeCode = sa.User.EmployeeCode,
                PositionName = sa.User.Role.RoleName,
                ShiftId = (int)sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.ShiftTemplate.EndTime,
                WorkDate = sa.Schedule.WorkDate,
                StoreId = (int)sa.Schedule.BranchId,
                StoreName = sa.Schedule.Branch.Name,
                Status = sa.Status,
                IsDispatched = sa.User.HomeBranchId != sa.Schedule.BranchId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

    /// <summary>
    /// Gửi yêu cầu đổi ca trực hoặc chuyển ca (nhờ làm thay) giữa các nhân viên.
    /// <summary>
    /// Gửi yêu cầu đổi ca trực, chuyển ca (nhờ làm thay) hoặc xin nghỉ ca (gửi Cửa hàng trưởng xếp lại).
    /// </summary>
    public async Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
    {
        var requestingAssignment = await _context.ShiftAssignments
            .Include(sa => sa.User)
            .Include(sa => sa.AssignedRole)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.AssignmentId && sa.UserId == (ulong)requesterEmployeeId);

        if (requestingAssignment == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của bạn để yêu cầu đổi/chuyển/nghỉ.");
        }

        if (requestingAssignment.Schedule?.Branch != null && !string.Equals(requestingAssignment.Schedule.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail($"Chi nhánh '{requestingAssignment.Schedule.Branch.Name}' đã bị khóa. Không thể tạo đơn mới.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var minAllowedDate = today.AddDays(1); // RÀNG BUỘC: Phải báo trước ít nhất 1 ngày (chỉ cho phép từ ngày mai trở đi)
        if (requestingAssignment.Schedule.WorkDate < minAllowedDate)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Đơn xin đổi/nghỉ ca phải được gửi trước ngày làm việc ít nhất 1 ngày. Không thể tạo đơn cho ca trực hôm nay hoặc trong quá khứ.");
        }

        // Kiểm tra xem ca này đã có đơn chờ duyệt chưa (bao gồm cả đơn chờ đồng nghiệp xác nhận)
        var hasPending = await _context.ShiftSwapRequests
            .AnyAsync(r => (r.RequestingAssignmentId == requestingAssignment.Id || (r.TargetAssignmentId != null && r.TargetAssignmentId == requestingAssignment.Id))
                        && (r.Status == "PENDING" || r.Status == "PENDING_PEER"));
        if (hasPending)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Ca trực này hiện đang có đơn xin đổi/chuyển/nghỉ ca đang chờ xử lý.");
        }

        // VALIDATE BIÊN THỜI GIAN BIỆT PHÁI: Nếu ca trực thuộc chi nhánh khác HomeBranch,
        // kiểm tra nhân viên có lệnh biệt phái hợp lệ bao phủ ngày ca trực không.
        var requesterUser = requestingAssignment.User;
        var assignmentBranchId = requestingAssignment.Schedule.BranchId;
        if (requesterUser != null && assignmentBranchId != requesterUser.HomeBranchId)
        {
            var hasValidDispatch = await _context.Set<Domain.Entities.TemporaryDispatch>()
                .AnyAsync(d => d.UserId == (ulong)requesterEmployeeId
                            && d.TargetBranchId == assignmentBranchId
                            && d.Status == "APPROVED"
                            && d.StartDate <= requestingAssignment.Schedule.WorkDate
                            && d.EndDate >= requestingAssignment.Schedule.WorkDate);
            if (!hasValidDispatch)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Ngày ca trực nằm ngoài thời gian điều chuyển hợp lệ của bạn tại chi nhánh này.");
            }
        }

        var reqType = (request.RequestType ?? "SWAP").ToUpper().Trim();
        User? targetUser = null;
        ShiftAssignment? targetAssignment = null;

        if (reqType == "LEAVE")
        {
            // TH1: Xin nghỉ không có người làm thay -> Gửi Cửa hàng trưởng
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng nhập lý do xin nghỉ ca trực để Cửa hàng trưởng xem xét.");
            }
        }
        else if (reqType == "TRANSFER")
        {
            // TH2a: Chuyển ca 1 chiều - nhờ đồng nghiệp làm thay
            if (!request.TargetEmployeeId.HasValue || request.TargetEmployeeId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn hoặc nhập mã nhân viên đồng nghiệp đồng ý làm thay.");
            }

            targetUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == (ulong)request.TargetEmployeeId.Value);

            if (targetUser == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy thông tin nhân viên được nhờ làm thay.");
            }

            if (targetUser.Id == (ulong)requesterEmployeeId)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể gửi đơn nhờ làm thay cho chính bản thân bạn.");
            }

            // Kiểm tra trùng ca trong cùng ngày của đồng nghiệp
            var targetHasScheduleConflict = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.ScheduleId == requestingAssignment.ScheduleId && sa.Status != "CANCELLED");
            if (targetHasScheduleConflict)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Đồng nghiệp đã có phân công trong cùng khung ca trực này.");
            }

            var targetHasSameDayShift = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate && sa.Status != "CANCELLED");
            if (targetHasSameDayShift)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có lịch trực ca khác trong ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể tiếp nhận ca này.");
            }
        }
        else
        {
            // TH2b: Đổi ca 2 chiều (SWAP)
            if (!request.TargetEmployeeId.HasValue || request.TargetEmployeeId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn đồng nghiệp để thực hiện đổi ca.");
            }

            targetUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == (ulong)request.TargetEmployeeId.Value);

            if (targetUser == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy thông tin đồng nghiệp được chọn.");
            }

            if (targetUser.Id == (ulong)requesterEmployeeId)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể đổi ca với chính bản thân bạn.");
            }

            // RÀNG BUỘC: Chỉ những người có cùng vai trò mới được đổi lịch cho nhau
            if (requestingAssignment.User.RoleId != targetUser.RoleId)
            {
                var reqRoleName = requestingAssignment.AssignedRole?.RoleName ?? requestingAssignment.User.Role?.RoleName ?? "Vai trò hiện tại";
                var targetRoleName = targetUser.Role?.RoleName ?? "Vai trò khác";
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Chỉ những nhân viên có cùng vai trò mới được đổi lịch cho nhau. Bạn là '{reqRoleName}', còn đồng nghiệp là '{targetRoleName}'.");
            }

            if (!request.TargetAssignmentId.HasValue || request.TargetAssignmentId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn ca trực của đồng nghiệp để tráo đổi.");
            }

            targetAssignment = await _context.ShiftAssignments
                .Include(sa => sa.User)
                .Include(sa => sa.AssignedRole)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.TargetAssignmentId.Value && sa.UserId == targetUser.Id);

            if (targetAssignment == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của đồng nghiệp để đổi.");
            }

            if (targetAssignment.Id == requestingAssignment.Id)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Hai ca trực xin đổi không thể là cùng một ca.");
            }

            // RÀNG BUỘC: Ca đồng nghiệp cũng phải trước ít nhất 1 ngày
            if (targetAssignment.Schedule.WorkDate < minAllowedDate)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Ca trực của đồng nghiệp được chọn phải diễn ra sau ngày hôm nay ít nhất 1 ngày.");
            }

            bool isSameDaySwap = requestingAssignment.Schedule.WorkDate == targetAssignment.Schedule.WorkDate;

            if (isSameDaySwap)
            {
                // 1. Chặn nếu cùng một khung ca (ScheduleId)
                if (requestingAssignment.ScheduleId == targetAssignment.ScheduleId)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail("Hai nhân viên đang cùng trực chung một khung ca, không thể tráo đổi cho nhau.");
                }

                // 2. Đổi cùng ngày: Bỏ qua kiểm tra trùng ngày của 2 ca đang đổi.
                // Chỉ kiểm tra xem requester có ca THỨ 3 nào khác trong ngày không
                var requesterHasOtherShift = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == (ulong)requesterEmployeeId 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
                if (requesterHasOtherShift)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Bạn đã có ca trực khác chưa hoàn tất trong ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}.");
                }

                // Chỉ kiểm tra xem đồng nghiệp có ca THỨ 3 nào khác trong ngày không
                var targetHasOtherShift = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == targetUser.Id 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == targetAssignment.Schedule.WorkDate);
                if (targetHasOtherShift)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có ca trực khác chưa hoàn tất trong ngày {targetAssignment.Schedule.WorkDate:dd/MM/yyyy}.");
                }
            }
            else
            {
                // Đổi ca khác ngày: Kiểm tra xung đột ngày chéo giữa 2 bên
                var requesterConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == (ulong)requesterEmployeeId 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == targetAssignment.Schedule.WorkDate);
                if (requesterConflict)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Bạn đã có ca trực khác ngày {targetAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể tiếp nhận ca của đồng nghiệp.");
                }

                var targetConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == targetUser.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
                if (targetConflict)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có ca trực khác ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể nhận ca xin đổi.");
                }
            }
        }

        // ── KIỂM TRA LUẬT LAO ĐỘNG (Điều 110, 105/107, 111 BLLĐ) ──────────────────
        if (reqType != "LEAVE")
        {
            var laborCheck = await ValidateSwapLaborLawsAsync(
                reqType: reqType,
                requesterId: (ulong)requesterEmployeeId,
                requesterExcludeId: requestingAssignment.Id,
                requesterNewShiftDate: targetAssignment?.Schedule.WorkDate ?? requestingAssignment.Schedule.WorkDate,
                requesterNewShiftStart: targetAssignment?.Schedule.ShiftTemplate?.StartTime ?? requestingAssignment.Schedule.ShiftTemplate!.StartTime,
                requesterNewShiftEnd: targetAssignment?.Schedule.ShiftTemplate?.EndTime ?? requestingAssignment.Schedule.ShiftTemplate!.EndTime,
                requesterNewIsOvernight: targetAssignment?.Schedule.ShiftTemplate?.IsOvernight ?? requestingAssignment.Schedule.ShiftTemplate!.IsOvernight,
                targetId: targetUser?.Id,
                targetExcludeId: targetAssignment?.Id,
                targetNewShiftDate: requestingAssignment.Schedule.WorkDate,
                targetNewShiftStart: requestingAssignment.Schedule.ShiftTemplate!.StartTime,
                targetNewShiftEnd: requestingAssignment.Schedule.ShiftTemplate!.EndTime,
                targetNewIsOvernight: requestingAssignment.Schedule.ShiftTemplate!.IsOvernight);

            if (!laborCheck.IsValid)
                return ApiResponse<ShiftSwapRequestDto>.Fail(laborCheck.ErrorMessage!);
        }
        // ────────────────────────────────────────────────────────────────────────────

        // LEAVE → PENDING (gửi thẳng Quản lý), SWAP/TRANSFER → PENDING_PEER (chờ đồng nghiệp xác nhận trước)
        var initialStatus = reqType == "LEAVE" ? "PENDING" : "PENDING_PEER";

        var swap = new ShiftSwapRequest
        {
            RequestingAssignmentId = requestingAssignment.Id,
            RequesterUserId = (ulong)requesterEmployeeId,
            ScheduleId = requestingAssignment.ScheduleId,
            TargetUserId = targetUser?.Id,
            TargetAssignmentId = targetAssignment?.Id,
            RequestType = reqType,
            Reason = request.Reason,
            Status = initialStatus,
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        var resultDto = new ShiftSwapRequestDto
        {
            SwapRequestId = (int)swap.Id,
            RequestType = swap.RequestType,
            AssignmentId = (int)requestingAssignment.Id,
            RequesterEmployeeId = requesterEmployeeId,
            RequesterName = requestingAssignment.User.FullName,
            RequesterRoleName = requestingAssignment.AssignedRole != null ? requestingAssignment.AssignedRole.RoleName : (requestingAssignment.User.Role != null ? requestingAssignment.User.Role.RoleName : ""),
            RequesterShiftName = requestingAssignment.Schedule.ShiftTemplate?.Name ?? "",
            RequesterWorkDate = requestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd"),
            RequesterTimeRange = requestingAssignment.Schedule.ShiftTemplate != null
                ? $"{requestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {requestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                : "",
            TargetEmployeeId = targetUser != null ? (int)targetUser.Id : null,
            TargetName = targetUser != null ? targetUser.FullName : "Không có (Xin nghỉ ca)",
            TargetRoleName = targetUser != null && targetUser.Role != null ? targetUser.Role.RoleName : "",
            TargetAssignmentId = targetAssignment != null ? (int)targetAssignment.Id : null,
            TargetShiftName = targetAssignment?.Schedule?.ShiftTemplate?.Name,
            TargetWorkDate = targetAssignment?.Schedule?.WorkDate.ToString("yyyy-MM-dd"),
            TargetTimeRange = targetAssignment?.Schedule?.ShiftTemplate != null
                ? $"{targetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {targetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                : null,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        };

        var successMsg = reqType == "LEAVE"
            ? "Đã gửi đơn xin nghỉ ca trực cho Cửa hàng trưởng phê duyệt và sắp xếp lại."
            : reqType == "TRANSFER"
                ? "Đã gửi đơn xin chuyển ca (nhờ làm thay), chờ đồng nghiệp xác nhận."
                : "Đã gửi đơn xin đổi ca với đồng nghiệp, chờ đồng nghiệp xác nhận.";

        // Gửi thông báo email nếu là yêu cầu đổi ca / chuyển ca cho đồng nghiệp
        if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var reqShiftInfo = $"{requestingAssignment.Schedule.ShiftTemplate?.Name} ({requestingAssignment.Schedule.WorkDate:dd/MM/yyyy} {requestingAssignment.Schedule.ShiftTemplate?.StartTime:hh\\:mm}-{requestingAssignment.Schedule.ShiftTemplate?.EndTime:hh\\:mm})";
                    var actionText = reqType == "TRANSFER" ? "Yêu cầu nhờ nhận ca làm việc" : "Yêu cầu đổi ca làm việc";
                    // PENDING_PEER: Gửi email cho đồng nghiệp với trạng thái CHỜ BẠN XÁC NHẬN
                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        targetUser.Email,
                        targetUser.FullName,
                        $"{actionText} từ {requestingAssignment.User.FullName}",
                        $"Ca của đồng nghiệp: {reqShiftInfo}" + (targetAssignment?.Schedule != null ? $" | Ca dự kiến của bạn: {targetAssignment.Schedule.ShiftTemplate?.Name} ({targetAssignment.Schedule.WorkDate:dd/MM/yyyy})" : ""),
                        requestingAssignment.Schedule.WorkDate.ToString("dd/MM/yyyy"),
                        request.Reason ?? "Không có ghi chú thêm",
                        "CHỜ BẠN XÁC NHẬN"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email cho nhân viên nhận đổi ca");
                }
            });
        }

        return ApiResponse<ShiftSwapRequestDto>.Ok(resultDto, successMsg);
    }

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi, chuyển hoặc xin nghỉ ca trực.
    /// </summary>
    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.TargetUser)
            .Include(s => s.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển/nghỉ ca không tồn tại.");
        }

        if (swap.Status != "PENDING")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        // PHÂN QUYỀN CHI NHÁNH: Quản lý chỉ duyệt được đơn thuộc chi nhánh mình
        var assignmentBranchId = swap.RequestingAssignment?.Schedule?.BranchId ?? swap.Schedule?.BranchId;
        if (assignmentBranchId.HasValue)
        {
            var manager = await _context.Users.FindAsync((ulong)managerEmployeeId);
            if (manager != null && manager.HomeBranchId != assignmentBranchId.Value)
            {
                // Kiểm tra thêm: Quản lý có thể là global role (OPERATIONS_ADMIN, BUSINESS_OWNER)
                var managerRole = (await _context.Roles.FindAsync(manager.RoleId))?.RoleName?.ToUpperInvariant();
                bool isGlobal = managerRole == "OPERATIONS_ADMIN" || managerRole == "BUSINESS_OWNER" || managerRole == "ADMIN";
                if (!isGlobal)
                {
                    return ApiResponse<bool>.Fail("Bạn không phải quản lý của chi nhánh này. Không thể phê duyệt đơn.");
                }
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (swap.RequestingAssignment != null && swap.RequestingAssignment.Schedule.WorkDate < today)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt đơn điều chỉnh cho ca làm việc đã trôi qua trong quá khứ.");
        }
        if (swap.TargetAssignment != null && swap.TargetAssignment.Schedule.WorkDate < today)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt đơn điều chỉnh cho ca làm việc đã trôi qua trong quá khứ.");
        }

        // ── KIỂM TRA LUẬT LAO ĐỘNG khi Manager duyệt (Điều 110, 105/107, 111) ─────
        if (!string.Equals(swap.RequestType, "LEAVE", StringComparison.OrdinalIgnoreCase)
            && swap.RequestingAssignment?.Schedule?.ShiftTemplate != null)
        {
            var ra = swap.RequestingAssignment;
            var ta = swap.TargetAssignment;

            var laborCheck = await ValidateSwapLaborLawsAsync(
                reqType: swap.RequestType,
                requesterId: swap.RequesterUserId ?? ra.UserId,
                requesterExcludeId: ra.Id,
                requesterNewShiftDate: ta?.Schedule.WorkDate ?? ra.Schedule.WorkDate,
                requesterNewShiftStart: ta?.Schedule.ShiftTemplate?.StartTime ?? ra.Schedule.ShiftTemplate.StartTime,
                requesterNewShiftEnd: ta?.Schedule.ShiftTemplate?.EndTime ?? ra.Schedule.ShiftTemplate.EndTime,
                requesterNewIsOvernight: ta?.Schedule.ShiftTemplate?.IsOvernight ?? ra.Schedule.ShiftTemplate.IsOvernight,
                targetId: swap.TargetUserId,
                targetExcludeId: ta?.Id,
                targetNewShiftDate: ra.Schedule.WorkDate,
                targetNewShiftStart: ra.Schedule.ShiftTemplate.StartTime,
                targetNewShiftEnd: ra.Schedule.ShiftTemplate.EndTime,
                targetNewIsOvernight: ra.Schedule.ShiftTemplate.IsOvernight);

            if (!laborCheck.IsValid)
                return ApiResponse<bool>.Fail(laborCheck.ErrorMessage!);
        }
        // ────────────────────────────────────────────────────────────────────────────

        // Lưu thông tin người làm đơn & đối tác trước khi thay đổi quan hệ DB
        var requesterUser = swap.RequesterUser ?? swap.RequestingAssignment?.User;
        if (requesterUser == null && swap.RequesterUserId.HasValue)
        {
            requesterUser = await _context.Users.FindAsync(swap.RequesterUserId.Value);
        }

        var targetUser = swap.TargetUser ?? swap.TargetAssignment?.User;
        if (targetUser == null && swap.TargetUserId.HasValue)
        {
            targetUser = await _context.Users.FindAsync(swap.TargetUserId.Value);
        }

        var requesterShiftTemplate = swap.RequestingAssignment?.Schedule?.ShiftTemplate ?? swap.Schedule?.ShiftTemplate;
        var requesterWorkDate = swap.RequestingAssignment?.Schedule?.WorkDate ?? swap.Schedule?.WorkDate ?? today;
        var targetShiftTemplate = swap.TargetAssignment?.Schedule?.ShiftTemplate;
        var targetWorkDate = swap.TargetAssignment?.Schedule?.WorkDate ?? today;

        swap.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        swap.ReviewedBy = (ulong)managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved)
        {
            if (string.Equals(swap.RequestType, "LEAVE", StringComparison.OrdinalIgnoreCase))
            {
                // TH1: Xin nghỉ ca -> Phê duyệt đổi trạng thái ca thành CANCELLED (giữ lại bản ghi, không xóa hẳn)
                if (swap.RequestingAssignment != null)
                {
                    swap.RequesterUserId ??= swap.RequestingAssignment.UserId;
                    swap.ScheduleId ??= swap.RequestingAssignment.ScheduleId;
                    swap.RequestingAssignment.Status = "CANCELLED";
                }
            }
            else if (string.Equals(swap.RequestType, "TRANSFER", StringComparison.OrdinalIgnoreCase))
            {
                // TH2a: Chuyển ca 1 chiều -> Gán lại UserId cho TargetUser
                if (swap.RequestingAssignment != null && swap.TargetUserId.HasValue)
                {
                    var isTargetAlreadyAssigned = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.Id != swap.RequestingAssignmentId 
                                     && sa.UserId == swap.TargetUserId.Value 
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                    if (isTargetAlreadyAssigned)
                    {
                        return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp nhận ca đã có phân công ca trực khác trong cùng ngày.");
                    }

                    swap.RequestingAssignment.UserId = swap.TargetUserId.Value;
                    swap.RequestingAssignment.Status = "CONFIRMED";
                }
            }
            else
            {
                // TH2b: Đổi ca 2 chiều -> Hoán đổi UserId giữa 2 ca trực
                if (swap.RequestingAssignment != null && swap.TargetAssignment != null)
                {
                    bool isSameDaySwap = swap.RequestingAssignment.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate;

                    if (isSameDaySwap)
                    {
                        if (swap.RequestingAssignment.ScheduleId == swap.TargetAssignment.ScheduleId)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Hai nhân viên đang trực chung một khung ca.");
                        }

                        var requesterHasOtherShift = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.UserId == swap.RequestingAssignment.UserId 
                                         && sa.Id != swap.RequestingAssignmentId 
                                         && sa.Id != swap.TargetAssignmentId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                        if (requesterHasOtherShift)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Nhân viên xin đổi đã có ca trực khác chưa hoàn tất trong ngày.");
                        }

                        var targetHasOtherShift = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.UserId == swap.TargetAssignment.UserId 
                                         && sa.Id != swap.RequestingAssignmentId 
                                         && sa.Id != swap.TargetAssignmentId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                        if (targetHasOtherShift)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp đã có ca trực khác chưa hoàn tất trong ngày.");
                        }
                    }
                    else
                    {
                        var requesterConflict = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.Id != swap.RequestingAssignmentId 
                                         && sa.UserId == swap.RequestingAssignment.UserId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                        if (requesterConflict)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Nhân viên xin đổi đã có ca trực khác trong ngày của ca được đổi.");
                        }

                        var targetConflict = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.Id != swap.TargetAssignmentId 
                                         && sa.UserId == swap.TargetAssignment.UserId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                        if (targetConflict)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp đã có ca trực khác trong ngày của ca xin đổi.");
                        }
                    }

                    var tempUserId = swap.RequestingAssignment.UserId;
                    swap.RequestingAssignment.UserId = swap.TargetAssignment.UserId;
                    swap.TargetAssignment.UserId = tempUserId;
                    swap.RequestingAssignment.Status = "CONFIRMED";
                    swap.TargetAssignment.Status = "CONFIRMED";
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt: Phát sinh xung đột phân công ca trực trong hệ thống.");
        }

        // Gửi email thông báo kết quả duyệt lịch cho các bên liên quan
        _ = Task.Run(async () =>
        {
            try
            {
                var reqShiftInfo = $"{requesterShiftTemplate?.Name} ({requesterWorkDate:dd/MM/yyyy} {requesterShiftTemplate?.StartTime:hh\\:mm}-{requesterShiftTemplate?.EndTime:hh\\:mm})";
                var statusText = request.IsApproved ? "ĐÃ ĐƯỢC PHÊ DUYỆT" : "ĐÃ BỊ TỪ CHỐI";

                // 1. Gửi email cho người làm đơn
                if (requesterUser != null && !string.IsNullOrWhiteSpace(requesterUser.Email))
                {
                    var changeTitle = swap.RequestType switch
                    {
                        "LEAVE" => "Đơn xin nghỉ ca trực",
                        "TRANSFER" => "Đơn xin chuyển ca trực (nhờ làm thay)",
                        _ => "Đơn xin đổi ca trực với đồng nghiệp"
                    };

                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        requesterUser.Email,
                        requesterUser.FullName,
                        changeTitle,
                        reqShiftInfo,
                        requesterWorkDate.ToString("dd/MM/yyyy"),
                        swap.Reason ?? "Không có ghi chú thêm",
                        statusText
                    );
                }

                // 2. Gửi email cho đồng nghiệp tiếp nhận (nếu có)
                if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
                {
                    var targetTitle = swap.RequestType == "TRANSFER"
                        ? "Phân công nhận ca thay từ đồng nghiệp"
                        : "Kết quả đổi ca trực với đồng nghiệp";

                    var targetDetails = swap.RequestType == "TRANSFER"
                        ? $"Bạn nhận ca: {reqShiftInfo}"
                        : $"Ca mới của bạn: {reqShiftInfo} (Đổi ca của bạn: {targetShiftTemplate?.Name} {targetWorkDate:dd/MM/yyyy})";

                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        targetUser.Email,
                        targetUser.FullName,
                        targetTitle,
                        targetDetails,
                        requesterWorkDate.ToString("dd/MM/yyyy"),
                        swap.Reason ?? "Không có ghi chú thêm",
                        statusText
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email thông báo kết quả phê duyệt đổi ca");
            }
        });

        var message = request.IsApproved
            ? "Đã phê duyệt đơn và cập nhật lịch làm việc thành công."
            : "Đã từ chối đơn yêu cầu điều chỉnh lịch ca.";

        return ApiResponse<bool>.Ok(true, message);
    }

    /// <summary>
    /// Đồng nghiệp (TargetUser) xác nhận hoặc từ chối đơn đổi/chuyển ca (Bước 1 của luồng 2 bước).
    /// Chỉ đơn có Status = "PENDING_PEER" mới được xử lý.
    /// </summary>
    public async Task<ApiResponse<bool>> RespondToSwapRequestAsync(int peerEmployeeId, PeerReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.TargetUser)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển ca không tồn tại.");
        }

        // Chỉ TargetUser mới được phản hồi
        if (swap.TargetUserId != (ulong)peerEmployeeId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải là người được yêu cầu xác nhận đơn này.");
        }

        if (swap.Status != "PENDING_PEER")
        {
            return ApiResponse<bool>.Fail("Đơn này không ở trạng thái chờ bạn xác nhận.");
        }

        // Lazy expiry: Kiểm tra nếu WorkDate đã qua hoặc bằng hôm nay → auto EXPIRED
        var today = DateOnly.FromDateTime(DateTime.Today);
        var workDate = swap.RequestingAssignment?.Schedule?.WorkDate;
        if (workDate.HasValue && workDate.Value <= today)
        {
            swap.Status = "EXPIRED";
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Fail("Đơn đã hết hạn vì ngày ca trực đã qua hoặc quá gần.");
        }

        if (request.IsAccepted)
        {
            // Re-validate xung đột ca trực (lịch có thể thay đổi trong lúc chờ peer review)
            if (swap.RequestType == "TRANSFER" && swap.RequestingAssignment != null)
            {
                var targetHasConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == swap.TargetUserId.Value
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                if (targetHasConflict)
                {
                    return ApiResponse<bool>.Fail("Không thể xác nhận: Bạn đã có ca trực khác trong ngày này kể từ khi đơn được tạo.");
                }
            }
            else if (swap.RequestType == "SWAP" && swap.TargetAssignment != null && swap.RequestingAssignment != null)
            {
                bool isSameDay = swap.RequestingAssignment.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate;
                if (!isSameDay)
                {
                    // Kiểm tra requester có ca vào ngày ca target
                    var requesterConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.UserId == swap.RequesterUserId.Value
                                     && sa.Id != swap.RequestingAssignmentId
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                    if (requesterConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể xác nhận: Nhân viên xin đổi đã có ca trực mới trong ngày ca của bạn.");
                    }

                    // Kiểm tra target có ca vào ngày ca requester
                    var targetConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.UserId == swap.TargetUserId.Value
                                     && sa.Id != swap.TargetAssignmentId
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                    if (targetConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể xác nhận: Bạn đã có ca trực khác trong ngày ca của đồng nghiệp.");
                    }
                }
            }

            // ── KIỂM TRA LUẬT LAO ĐỘNG khi Peer xác nhận (Điều 110, 105/107, 111) ──
            if (swap.RequestingAssignment?.Schedule?.ShiftTemplate != null)
            {
                var ra = swap.RequestingAssignment;
                var ta = swap.TargetAssignment;

                var laborCheck = await ValidateSwapLaborLawsAsync(
                    reqType: swap.RequestType,
                    requesterId: swap.RequesterUserId ?? ra.UserId,
                    requesterExcludeId: ra.Id,
                    requesterNewShiftDate: ta?.Schedule.WorkDate ?? ra.Schedule.WorkDate,
                    requesterNewShiftStart: ta?.Schedule.ShiftTemplate?.StartTime ?? ra.Schedule.ShiftTemplate.StartTime,
                    requesterNewShiftEnd: ta?.Schedule.ShiftTemplate?.EndTime ?? ra.Schedule.ShiftTemplate.EndTime,
                    requesterNewIsOvernight: ta?.Schedule.ShiftTemplate?.IsOvernight ?? ra.Schedule.ShiftTemplate.IsOvernight,
                    targetId: swap.TargetUserId,
                    targetExcludeId: ta?.Id,
                    targetNewShiftDate: ra.Schedule.WorkDate,
                    targetNewShiftStart: ra.Schedule.ShiftTemplate.StartTime,
                    targetNewShiftEnd: ra.Schedule.ShiftTemplate.EndTime,
                    targetNewIsOvernight: ra.Schedule.ShiftTemplate.IsOvernight);

                if (!laborCheck.IsValid)
                    return ApiResponse<bool>.Fail(laborCheck.ErrorMessage!);
            }
            // ────────────────────────────────────────────────────────────────────────

            swap.Status = "PENDING";
            await _context.SaveChangesAsync();

            // Gửi email cho người tạo đơn: Đồng nghiệp đã đồng ý
            if (swap.RequesterUser != null && !string.IsNullOrWhiteSpace(swap.RequesterUser.Email))
            {
                var peerName = swap.TargetUser?.FullName ?? "Đồng nghiệp";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                        await _emailService.SendShiftChangeNotificationEmailAsync(
                            swap.RequesterUser.Email,
                            swap.RequesterUser.FullName,
                            $"{peerName} đã đồng ý đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} của bạn",
                            $"Ca trực: {shiftInfo}. Đơn đang chờ Quản lý chi nhánh phê duyệt.",
                            swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                            swap.Reason ?? "",
                            "CHỜ QUẢN LÝ DUYỆT"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email thông báo peer accept");
                    }
                });
            }

            return ApiResponse<bool>.Ok(true, $"Bạn đã đồng ý đơn. Đơn chuyển sang Quản lý chi nhánh phê duyệt.");
        }
        else
        {
            swap.Status = "REJECTED";
            await _context.SaveChangesAsync();

            // Gửi email cho người tạo đơn: Đồng nghiệp đã từ chối
            if (swap.RequesterUser != null && !string.IsNullOrWhiteSpace(swap.RequesterUser.Email))
            {
                var peerName = swap.TargetUser?.FullName ?? "Đồng nghiệp";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                        await _emailService.SendShiftChangeNotificationEmailAsync(
                            swap.RequesterUser.Email,
                            swap.RequesterUser.FullName,
                            $"{peerName} đã từ chối đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} của bạn",
                            $"Ca trực: {shiftInfo}. Bạn có thể tạo đơn mới với đồng nghiệp khác.",
                            swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                            swap.Reason ?? "",
                            "ĐÃ BỊ TỪ CHỐI"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email thông báo peer reject");
                    }
                });
            }

            return ApiResponse<bool>.Ok(true, "Bạn đã từ chối đơn đổi/chuyển ca.");
        }
    }

    /// <summary>
    /// Nhân viên tạo đơn (RequesterUser) hủy đơn đổi/chuyển/nghỉ ca đang chờ xử lý.
    /// Chỉ áp dụng cho đơn có Status = "PENDING" hoặc "PENDING_PEER".
    /// </summary>
    public async Task<ApiResponse<bool>> CancelSwapRequestAsync(int requesterEmployeeId, int swapRequestId)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.TargetUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)swapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển/nghỉ ca không tồn tại.");
        }

        if (swap.RequesterUserId != (ulong)requesterEmployeeId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải là người tạo đơn này.");
        }

        if (swap.Status != "PENDING" && swap.Status != "PENDING_PEER")
        {
            return ApiResponse<bool>.Fail("Đơn này đã được xử lý, không thể hủy.");
        }

        swap.Status = "CANCELLED";
        await _context.SaveChangesAsync();

        // Gửi email cho TargetUser (nếu có) thông báo đơn bị hủy
        if (swap.TargetUser != null && !string.IsNullOrWhiteSpace(swap.TargetUser.Email))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        swap.TargetUser.Email,
                        swap.TargetUser.FullName,
                        $"Đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} đã bị hủy bởi người tạo",
                        $"Ca trực: {shiftInfo}",
                        swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                        swap.Reason ?? "",
                        "ĐÃ BỊ HỦY"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email thông báo hủy đơn swap");
                }
            });
        }

        return ApiResponse<bool>.Ok(true, "Đã hủy đơn thành công.");
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển/nghỉ ca của toàn chi nhánh (cho Quản lý cửa hàng duyệt).
    /// </summary>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
        // LAZY AUTO-EXPIRY: Tự động hết hạn các đơn chờ mà ngày ca trực đã qua hoặc bằng hôm nay
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expiredRequests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.Schedule)
            .Where(s => (s.Status == "PENDING" || s.Status == "PENDING_PEER")
                     && ((s.RequestingAssignment != null && s.RequestingAssignment.Schedule.WorkDate <= today)
                      || (s.Schedule != null && s.Schedule.WorkDate <= today))
                     && ((s.Schedule != null && s.Schedule.BranchId == (ulong)storeId)
                      || (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.BranchId == (ulong)storeId)))
            .ToListAsync();

        if (expiredRequests.Any())
        {
            foreach (var r in expiredRequests)
            {
                r.Status = "EXPIRED";
            }
            await _context.SaveChangesAsync();
        }
        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
                    .ThenInclude(u => u.Role)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.TargetUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.ReviewedByUser)
            .Where(s => (s.Schedule != null && s.Schedule.BranchId == (ulong)storeId) 
                     || (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.BranchId == (ulong)storeId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                RequestType = s.RequestType,
                AssignmentId = (int)(s.RequestingAssignmentId ?? 0),
                RequesterEmployeeId = (int)(s.RequesterUserId ?? (s.RequestingAssignment != null ? s.RequestingAssignment.UserId : 0)),
                RequesterName = s.RequesterUser != null 
                    ? s.RequesterUser.FullName 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.User.FullName : "Nhân viên"),
                RequesterRoleName = s.RequesterUser != null && s.RequesterUser.Role != null 
                    ? s.RequesterUser.Role.RoleName 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.AssignedRole != null 
                        ? s.RequestingAssignment.AssignedRole.RoleName 
                        : (s.RequestingAssignment != null && s.RequestingAssignment.User.Role != null ? s.RequestingAssignment.User.Role.RoleName : "")),
                RequesterShiftName = s.Schedule != null && s.Schedule.ShiftTemplate != null 
                    ? s.Schedule.ShiftTemplate.Name 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null 
                        ? s.RequestingAssignment.Schedule.ShiftTemplate.Name : ""),
                RequesterWorkDate = s.Schedule != null 
                    ? s.Schedule.WorkDate.ToString("yyyy-MM-dd") 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : ""),
                RequesterTimeRange = s.Schedule != null && s.Schedule.ShiftTemplate != null
                    ? $"{s.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null
                        ? $"{s.RequestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.RequestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                        : ""),
                TargetEmployeeId = s.TargetUserId != null ? (int)s.TargetUserId : null,
                TargetName = s.TargetUser != null ? s.TargetUser.FullName : "Không có (Xin nghỉ ca)",
                TargetRoleName = s.TargetUser != null && s.TargetUser.Role != null ? s.TargetUser.Role.RoleName : "",
                TargetAssignmentId = s.TargetAssignmentId != null ? (int)s.TargetAssignmentId : null,
                TargetShiftName = s.TargetAssignment != null ? s.TargetAssignment.Schedule.ShiftTemplate.Name : null,
                TargetWorkDate = s.TargetAssignment != null ? s.TargetAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : null,
                TargetTimeRange = s.TargetAssignment != null && s.TargetAssignment.Schedule.ShiftTemplate != null
                    ? $"{s.TargetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.TargetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : null,
                Reason = s.Reason,
                Status = s.Status,
                ReviewedByName = s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null,
                ReviewedAt = s.ReviewedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển/nghỉ ca của chính nhân viên (đã gửi hoặc được nhờ).
    /// </summary>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetMySwapRequestsAsync(int employeeId)
    {
        // LAZY AUTO-EXPIRY: Tự động hết hạn các đơn chờ mà ngày ca trực đã qua hoặc bằng hôm nay
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expiredRequests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.Schedule)
            .Where(s => (s.Status == "PENDING" || s.Status == "PENDING_PEER")
                     && ((s.RequesterUserId != null && s.RequesterUserId == (ulong)employeeId)
                      || (s.TargetUserId != null && s.TargetUserId == (ulong)employeeId))
                     && ((s.RequestingAssignment != null && s.RequestingAssignment.Schedule.WorkDate <= today)
                      || (s.Schedule != null && s.Schedule.WorkDate <= today)))
            .ToListAsync();

        if (expiredRequests.Any())
        {
            foreach (var r in expiredRequests)
            {
                r.Status = "EXPIRED";
            }
            await _context.SaveChangesAsync();
        }

        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
                    .ThenInclude(u => u.Role)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.TargetUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.ReviewedByUser)
            .Where(s => (s.RequesterUserId != null && s.RequesterUserId == (ulong)employeeId)
                     || (s.RequestingAssignment != null && s.RequestingAssignment.UserId == (ulong)employeeId) 
                     || (s.TargetUserId != null && s.TargetUserId == (ulong)employeeId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                RequestType = s.RequestType,
                AssignmentId = (int)(s.RequestingAssignmentId ?? 0),
                RequesterEmployeeId = (int)(s.RequesterUserId ?? (s.RequestingAssignment != null ? s.RequestingAssignment.UserId : 0)),
                RequesterName = s.RequesterUser != null 
                    ? s.RequesterUser.FullName 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.User.FullName : "Nhân viên"),
                RequesterRoleName = s.RequesterUser != null && s.RequesterUser.Role != null 
                    ? s.RequesterUser.Role.RoleName 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.AssignedRole != null 
                        ? s.RequestingAssignment.AssignedRole.RoleName 
                        : (s.RequestingAssignment != null && s.RequestingAssignment.User.Role != null ? s.RequestingAssignment.User.Role.RoleName : "")),
                RequesterShiftName = s.Schedule != null && s.Schedule.ShiftTemplate != null 
                    ? s.Schedule.ShiftTemplate.Name 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null 
                        ? s.RequestingAssignment.Schedule.ShiftTemplate.Name : ""),
                RequesterWorkDate = s.Schedule != null 
                    ? s.Schedule.WorkDate.ToString("yyyy-MM-dd") 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : ""),
                RequesterTimeRange = s.Schedule != null && s.Schedule.ShiftTemplate != null
                    ? $"{s.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null
                        ? $"{s.RequestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.RequestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                        : ""),
                TargetEmployeeId = s.TargetUserId != null ? (int)s.TargetUserId : null,
                TargetName = s.TargetUser != null ? s.TargetUser.FullName : "Không có (Xin nghỉ ca)",
                TargetRoleName = s.TargetUser != null && s.TargetUser.Role != null ? s.TargetUser.Role.RoleName : "",
                TargetAssignmentId = s.TargetAssignmentId != null ? (int)s.TargetAssignmentId : null,
                TargetShiftName = s.TargetAssignment != null ? s.TargetAssignment.Schedule.ShiftTemplate.Name : null,
                TargetWorkDate = s.TargetAssignment != null ? s.TargetAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : null,
                TargetTimeRange = s.TargetAssignment != null && s.TargetAssignment.Schedule.ShiftTemplate != null
                    ? $"{s.TargetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.TargetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : null,
                Reason = s.Reason,
                Status = s.Status,
                ReviewedByName = s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null,
                ReviewedAt = s.ReviewedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }

    /// <summary>
    /// Helper: Xác định chi nhánh làm việc thực tế của nhân viên vào một ngày cụ thể.
    /// Nếu nhân viên đang có lệnh điều chuyển (TemporaryDispatch APPROVED) bao phủ ngày workDate → trả về TargetBranchId.
    /// Ngược lại → trả về HomeBranchId.
    /// </summary>
    private async Task<ulong> GetEffectiveBranchIdAsync(ulong userId, DateOnly workDate)
    {
        var dispatch = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Where(d => d.UserId == userId
                     && d.Status == "APPROVED"
                     && d.StartDate <= workDate
                     && d.EndDate >= workDate)
            .FirstOrDefaultAsync();

        if (dispatch != null)
        {
            return dispatch.TargetBranchId;
        }

        var user = await _context.Users.FindAsync(userId);
        return user?.HomeBranchId ?? 0;
    }

    /// <summary>
    /// Lấy danh sách đồng nghiệp đang thực sự làm việc tại chi nhánh vào ngày cụ thể để nhân viên chọn khi đổi/chuyển ca.
    /// Bao gồm: (1) NV gốc chi nhánh không bị biệt phái đi nơi khác, (2) NV từ chi nhánh khác được biệt phái đến.
    /// Ràng buộc: Cùng RoleId, loại trừ chính mình.
    /// </summary>
    public async Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId, DateOnly? workDate = null)
    {
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == (ulong)currentEmployeeId);
        if (currentUser == null)
        {
            return ApiResponse<List<ColleagueDto>>.Fail("Không tìm thấy thông tin nhân viên yêu cầu.");
        }

        var effectiveWorkDate = workDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        // === NHÓM 1: NV gốc của chi nhánh, KHÔNG bị biệt phái đi nơi khác vào workDate ===
        // Lấy danh sách ID nhân viên gốc chi nhánh đang bị dispatch đi chỗ khác
        var dispatchedAwayUserIds = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Where(d => d.Status == "APPROVED"
                     && d.StartDate <= effectiveWorkDate
                     && d.EndDate >= effectiveWorkDate
                     && d.SourceBranchId == (ulong)branchId)
            .Select(d => d.UserId)
            .ToListAsync();

        var nativeColleagues = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Id != (ulong)currentEmployeeId
                     && u.HomeBranchId == (ulong)branchId
                     && u.Status == "ACTIVE"
                     && u.RoleId == currentUser.RoleId
                     && !dispatchedAwayUserIds.Contains(u.Id))
            .Select(u => new ColleagueDto
            {
                EmployeeId = (int)u.Id,
                FullName = u.FullName,
                RoleName = u.Role != null ? u.Role.RoleName : "",
                PhoneNumber = u.Phone ?? ""
            })
            .ToListAsync();

        // === NHÓM 2: NV từ chi nhánh khác được biệt phái ĐẾN chi nhánh này vào workDate ===
        var dispatchedInUserIds = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Where(d => d.Status == "APPROVED"
                     && d.StartDate <= effectiveWorkDate
                     && d.EndDate >= effectiveWorkDate
                     && d.TargetBranchId == (ulong)branchId)
            .Select(d => d.UserId)
            .ToListAsync();

        // Loại bỏ chính mình và lọc cùng role
        var dispatchedInColleagues = dispatchedInUserIds.Contains((ulong)currentEmployeeId)
            ? new List<ColleagueDto>()
            : await _context.Users
                .Include(u => u.Role)
                .Where(u => dispatchedInUserIds.Contains(u.Id)
                         && u.Id != (ulong)currentEmployeeId
                         && u.Status == "ACTIVE"
                         && u.RoleId == currentUser.RoleId)
                .Select(u => new ColleagueDto
                {
                    EmployeeId = (int)u.Id,
                    FullName = u.FullName,
                    RoleName = u.Role != null ? u.Role.RoleName : "",
                    PhoneNumber = u.Phone ?? ""
                })
                .ToListAsync();

        // Gộp 2 nhóm, loại trùng, sắp xếp theo tên
        var colleagues = nativeColleagues
            .Concat(dispatchedInColleagues)
            .GroupBy(c => c.EmployeeId)
            .Select(g => g.First())
            .OrderBy(c => c.FullName)
            .ToList();

        return ApiResponse<List<ColleagueDto>>.Ok(colleagues);
    }

    /// <summary>
    /// Lấy danh sách các ca làm việc của một đồng nghiệp trong tương lai để nhân viên chọn đổi.
    /// Hỗ trợ kiểm tra báo trước 1 ngày và xử lý đổi ca cùng ngày.
    /// </summary>
    public async Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId, int? requestingAssignmentId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var minAllowedDate = today.AddDays(1); // RÀNG BUỘC: Chỉ hiển thị các ca diễn ra từ ngày mai trở đi

        DateOnly? requestingWorkDate = null;
        ulong? requestingScheduleId = null;

        if (requestingAssignmentId.HasValue && requestingAssignmentId.Value > 0)
        {
            var reqAssignment = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                .FirstOrDefaultAsync(sa => sa.Id == (ulong)requestingAssignmentId.Value && sa.UserId == (ulong)currentEmployeeId);

            if (reqAssignment != null)
            {
                requestingWorkDate = reqAssignment.Schedule.WorkDate;
                requestingScheduleId = reqAssignment.ScheduleId;
            }
        }

        // Lấy danh sách ca trực hiện có của nhân viên yêu cầu từ ngày mai trở đi
        var myActiveAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
            .Where(sa => sa.UserId == (ulong)currentEmployeeId 
                      && sa.Status != "CANCELLED"
                      && sa.Schedule.WorkDate >= minAllowedDate)
            .ToListAsync();

        // Danh sách các ngày nhân viên đã có lịch bận (loại trừ ca đang đem ra đổi)
        var myBusyDates = myActiveAssignments
            .Where(sa => !requestingAssignmentId.HasValue || sa.Id != (ulong)requestingAssignmentId.Value)
            .Select(sa => sa.Schedule.WorkDate)
            .ToHashSet();

        // Danh sách khung ca nhân viên hiện tại đang trực
        var myActiveScheduleIds = myActiveAssignments
            .Select(sa => sa.ScheduleId)
            .ToHashSet();

        // Lấy danh sách ca khả dụng của đồng nghiệp
        var colleagueShifts = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Where(sa => sa.UserId == (ulong)colleagueEmployeeId 
                      && sa.Status != "CANCELLED"
                      && sa.Schedule.WorkDate >= minAllowedDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var availableShifts = new List<ColleagueShiftDto>();

        foreach (var sa in colleagueShifts)
        {
            // Trùng khung giờ trực chung
            if (myActiveScheduleIds.Contains(sa.ScheduleId))
            {
                continue;
            }

            // FILTER THEO CHI NHÁNH: Ca của đồng nghiệp phải thuộc chi nhánh mà nhân viên hiện tại được phép làm việc vào ngày đó
            var myEffectiveBranchId = await GetEffectiveBranchIdAsync((ulong)currentEmployeeId, sa.Schedule.WorkDate);
            if (sa.Schedule.BranchId != myEffectiveBranchId)
            {
                continue;
            }

            // Nếu là ca cùng ngày với ca đem ra đổi
            if (requestingWorkDate.HasValue && sa.Schedule.WorkDate == requestingWorkDate.Value)
            {
                // Cho phép đổi cùng ngày miễn là nhân viên không có ca thứ 3 bận trong ngày đó
                if (myBusyDates.Contains(sa.Schedule.WorkDate))
                {
                    continue;
                }
            }
            else
            {
                // Khác ngày: Nhân viên không được có ca trực trong ngày đó
                if (myBusyDates.Contains(sa.Schedule.WorkDate))
                {
                    continue;
                }
            }

            availableShifts.Add(new ColleagueShiftDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                WorkDate = sa.Schedule.WorkDate.ToString("yyyy-MM-dd"),
                TimeRange = $"{sa.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {sa.Schedule.ShiftTemplate.EndTime:hh\\:mm}",
                BranchName = sa.Schedule.Branch.Name
            });
        }

        return ApiResponse<List<ColleagueShiftDto>>.Ok(availableShifts);
    }

    // ============================================================
    // PRIVATE HELPERS: Kiểm tra Luật Lao động khi Swap / Transfer
    // ============================================================

    /// <summary>
    /// Orchestrator: Chạy 3 bộ kiểm tra luật lao động cho cả 2 bên (requester và target).
    /// SWAP: check cả 2 (mỗi người nhận ca của người kia).
    /// TRANSFER: chỉ check target (người nhận ca mới).
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> ValidateSwapLaborLawsAsync(
        string reqType,
        ulong requesterId,
        ulong requesterExcludeId,
        DateOnly requesterNewShiftDate,
        TimeOnly requesterNewShiftStart,
        TimeOnly requesterNewShiftEnd,
        bool requesterNewIsOvernight,
        ulong? targetId,
        ulong? targetExcludeId,
        DateOnly targetNewShiftDate,
        TimeOnly targetNewShiftStart,
        TimeOnly targetNewShiftEnd,
        bool targetNewIsOvernight)
    {
        // --- Kiểm tra cho REQUESTER (chỉ với SWAP – người nhận ca của target) ---
        if (string.Equals(reqType, "SWAP", StringComparison.OrdinalIgnoreCase))
        {
            double requesterNewHours = GetShiftNetWorkingHours(
                requesterNewShiftStart, requesterNewShiftEnd, requesterNewIsOvernight);

            var r1 = await CheckMinRestBetweenShiftsAsync(
                requesterNewShiftDate, requesterId, requesterExcludeId,
                requesterNewShiftStart, requesterNewShiftEnd, requesterNewIsOvernight,
                isRequester: true);
            if (!r1.IsValid) return r1;

            var r2 = await CheckMaxDailyWorkingHoursAsync(
                requesterId, requesterExcludeId,
                requesterNewShiftDate, requesterNewHours,
                isRequester: true);
            if (!r2.IsValid) return r2;

            var r3 = await CheckWeeklyRestRequirementAsync(
                requesterId, requesterExcludeId,
                requesterNewShiftDate, requesterNewShiftStart,
                requesterNewShiftEnd, requesterNewIsOvernight,
                isRequester: true);
            if (!r3.IsValid) return r3;
        }

        // --- Kiểm tra cho TARGET (SWAP hoặc TRANSFER – người nhận ca của requester) ---
        if (targetId.HasValue)
        {
            double targetNewHours = GetShiftNetWorkingHours(
                targetNewShiftStart, targetNewShiftEnd, targetNewIsOvernight);

            var t1 = await CheckMinRestBetweenShiftsAsync(
                targetNewShiftDate, targetId.Value, targetExcludeId,
                targetNewShiftStart, targetNewShiftEnd, targetNewIsOvernight,
                isRequester: false);
            if (!t1.IsValid) return t1;

            var t2 = await CheckMaxDailyWorkingHoursAsync(
                targetId.Value, targetExcludeId,
                targetNewShiftDate, targetNewHours,
                isRequester: false);
            if (!t2.IsValid) return t2;

            var t3 = await CheckWeeklyRestRequirementAsync(
                targetId.Value, targetExcludeId,
                targetNewShiftDate, targetNewShiftStart,
                targetNewShiftEnd, targetNewIsOvernight,
                isRequester: false);
            if (!t3.IsValid) return t3;
        }

        return (true, null);
    }

    /// <summary>
    /// Tính số giờ làm việc thực của một ca (trừ break). Xử lý ca overnight.
    /// </summary>
    private static double GetShiftNetWorkingHours(
        TimeOnly startTime, TimeOnly endTime, bool isOvernight)
    {
        double totalMinutes = isOvernight
            ? (24 * 60 - startTime.ToTimeSpan().TotalMinutes + endTime.ToTimeSpan().TotalMinutes)
            : (endTime.ToTimeSpan().TotalMinutes - startTime.ToTimeSpan().TotalMinutes);
        return Math.Max(0, totalMinutes / 60.0);
    }

    /// <summary>
    /// Điều 110 BLLĐ: Khoảng nghỉ tối thiểu 12 giờ liên tục giữa 2 ca liên tiếp.
    /// Kiểm tra ca liền trước (∆t₁) và ca liền sau (∆t₂) ca mới.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckMinRestBetweenShiftsAsync(
        DateOnly newShiftDate,
        ulong userId,
        ulong? excludeAssignmentId,
        TimeOnly newShiftStart,
        TimeOnly newShiftEnd,
        bool newIsOvernight,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Chuyển ca mới sang DateTime
        var newStartDt = newShiftDate.ToDateTime(newShiftStart);
        var newEndDt = newIsOvernight
            ? newShiftDate.AddDays(1).ToDateTime(newShiftEnd)
            : newShiftDate.ToDateTime(newShiftEnd);

        // Query tất cả ca trong khoảng ±2 ngày để bắt được ca overnight lân cận
        var windowStart = newShiftDate.AddDays(-2);
        var windowEnd = newShiftDate.AddDays(2);

        var nearbyAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && sa.Schedule.WorkDate >= windowStart
                      && sa.Schedule.WorkDate <= windowEnd
                      && sa.Schedule.ShiftTemplate != null)
            .ToListAsync();

        DateTime? latestEndBeforeNew = null;  // ca liền trước
        DateTime? earliestStartAfterNew = null; // ca liền sau

        foreach (var sa in nearbyAssignments)
        {
            var t = sa.Schedule.ShiftTemplate!;
            var sStart = sa.Schedule.WorkDate.ToDateTime(t.StartTime);
            var sEnd = t.IsOvernight
                ? sa.Schedule.WorkDate.AddDays(1).ToDateTime(t.EndTime)
                : sa.Schedule.WorkDate.ToDateTime(t.EndTime);

            if (sEnd <= newStartDt) // ca kết thúc TRƯỚC ca mới bắt đầu
            {
                if (latestEndBeforeNew == null || sEnd > latestEndBeforeNew)
                    latestEndBeforeNew = sEnd;
            }
            else if (sStart >= newEndDt) // ca bắt đầu SAU ca mới kết thúc
            {
                if (earliestStartAfterNew == null || sStart < earliestStartAfterNew)
                    earliestStartAfterNew = sStart;
            }
        }

        int minRest = HeadcountConstants.MinRestBetweenShiftsHours;

        if (latestEndBeforeNew.HasValue)
        {
            double gapHours = (newStartDt - latestEndBeforeNew.Value).TotalHours;
            if (gapHours < minRest)
                return (false,
                    $"{who} vi phạm quy định nghỉ tối thiểu {minRest} giờ trước khi chuyển sang ca mới " +
                    $"(khoảng cách hiện tại: {gapHours:F1} giờ < {minRest} giờ). [Điều 110 BLLĐ]");
        }

        if (earliestStartAfterNew.HasValue)
        {
            double gapHours = (earliestStartAfterNew.Value - newEndDt).TotalHours;
            if (gapHours < minRest)
                return (false,
                    $"{who} vi phạm quy định nghỉ tối thiểu {minRest} giờ sau ca mới trước ca tiếp theo " +
                    $"(khoảng cách hiện tại: {gapHours:F1} giờ < {minRest} giờ). [Điều 110 BLLĐ]");
        }

        return (true, null);
    }

    /// <summary>
    /// Điều 105 &amp; 107 BLLĐ: Tổng giờ làm việc trong ngày ≤ 12 giờ.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckMaxDailyWorkingHoursAsync(
        ulong userId,
        ulong? excludeAssignmentId,
        DateOnly targetDate,
        double newShiftHours,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Lấy tất cả ca trong ngày targetDate (bao gồm ca overnight bắt đầu ngày trước)
        var dayAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && (sa.Schedule.WorkDate == targetDate
                          || (sa.Schedule.ShiftTemplate!.IsOvernight && sa.Schedule.WorkDate == targetDate.AddDays(-1))))
            .ToListAsync();

        double existingHours = 0;
        foreach (var sa in dayAssignments)
        {
            var t = sa.Schedule.ShiftTemplate!;
            // Với ca overnight bắt đầu hôm trước: chỉ tính phần giờ rơi vào targetDate
            if (t.IsOvernight && sa.Schedule.WorkDate == targetDate.AddDays(-1))
            {
                existingHours += t.EndTime.ToTimeSpan().TotalHours; // giờ từ 00:00 đến EndTime
            }
            else
            {
                existingHours += GetShiftNetWorkingHours(t.StartTime, t.EndTime, t.IsOvernight);
            }
        }

        double totalHours = existingHours + newShiftHours;
        int maxHours = HeadcountConstants.MaxWorkingHoursPerDay;

        if (totalHours > maxHours)
            return (false,
                $"{who}: Tổng giờ làm việc trong ngày {targetDate:dd/MM/yyyy} sẽ đạt {totalHours:F1} giờ, " +
                $"vượt quá giới hạn {maxHours} giờ/ngày. [Điều 105 & 107 BLLĐ]");

        return (true, null);
    }

    /// <summary>
    /// Điều 111 BLLĐ: Trong chu kỳ 7 ngày chứa ca mới, phải có ít nhất 1 khoảng trống ≥ 24 giờ liên tục.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckWeeklyRestRequirementAsync(
        ulong userId,
        ulong? excludeAssignmentId,
        DateOnly newShiftDate,
        TimeOnly newShiftStart,
        TimeOnly newShiftEnd,
        bool newIsOvernight,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Cửa sổ kiểm tra: 6 ngày trước + ngày ca mới = 7 ngày liên tiếp
        var windowStart = newShiftDate.AddDays(-(HeadcountConstants.WeeklyRestWindowDays - 1));
        var windowEnd   = newShiftDate;
        var windowStartDt = windowStart.ToDateTime(TimeOnly.MinValue);
        var windowEndDt   = windowEnd.AddDays(1).ToDateTime(TimeOnly.MinValue); // exclusive

        // Query tất cả ca trong cửa sổ (±1 ngày để bắt overnight)
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && sa.Schedule.WorkDate >= windowStart.AddDays(-1)
                      && sa.Schedule.WorkDate <= windowEnd.AddDays(1)
                      && sa.Schedule.ShiftTemplate != null)
            .ToListAsync();

        // Xây dựng danh sách (StartDt, EndDt) bao gồm ca mới
        var intervals = new List<(DateTime Start, DateTime End)>();

        foreach (var sa in assignments)
        {
            var t = sa.Schedule.ShiftTemplate!;
            var sStart = sa.Schedule.WorkDate.ToDateTime(t.StartTime);
            var sEnd = t.IsOvernight
                ? sa.Schedule.WorkDate.AddDays(1).ToDateTime(t.EndTime)
                : sa.Schedule.WorkDate.ToDateTime(t.EndTime);
            // Chỉ đưa vào nếu interval cắt qua cửa sổ [windowStartDt, windowEndDt]
            if (sEnd > windowStartDt && sStart < windowEndDt)
                intervals.Add((sStart, sEnd));
        }

        // Thêm ca mới
        var newStart = newShiftDate.ToDateTime(newShiftStart);
        var newEnd   = newIsOvernight
            ? newShiftDate.AddDays(1).ToDateTime(newShiftEnd)
            : newShiftDate.ToDateTime(newShiftEnd);
        intervals.Add((newStart, newEnd));

        // Sắp xếp và merge overlapping/adjacent intervals
        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(DateTime Start, DateTime End)>();
        foreach (var iv in intervals)
        {
            if (merged.Count == 0 || iv.Start > merged[^1].End)
                merged.Add(iv);
            else
                merged[^1] = (merged[^1].Start, iv.End > merged[^1].End ? iv.End : merged[^1].End);
        }

        // Kiểm tra các khoảng trống trong cửa sổ
        int minRest = HeadcountConstants.MinWeeklyRestHours;
        bool hasAdequateRest = false;

        // Khoảng trống trước ca đầu tiên
        if (merged.Count == 0 || (merged[0].Start - windowStartDt).TotalHours >= minRest)
        {
            hasAdequateRest = true;
        }

        if (!hasAdequateRest)
        {
            // Khoảng trống giữa các ca liên tiếp
            for (int i = 0; i < merged.Count - 1; i++)
            {
                double gap = (merged[i + 1].Start - merged[i].End).TotalHours;
                if (gap >= minRest) { hasAdequateRest = true; break; }
            }
        }

        if (!hasAdequateRest)
        {
            // Khoảng trống sau ca cuối cùng
            if (merged.Count > 0 && (windowEndDt - merged[^1].End).TotalHours >= minRest)
                hasAdequateRest = true;
        }

        if (!hasAdequateRest)
            return (false,
                $"{who} vi phạm quy định nghỉ tuần tối thiểu {minRest} giờ liên tục trong chu kỳ 7 ngày " +
                $"({windowStart:dd/MM} – {windowEnd:dd/MM/yyyy}). Nhận ca này sẽ không còn khoảng nghỉ đủ {minRest} giờ. [Điều 111 BLLĐ]");

        return (true, null);
    }
}
