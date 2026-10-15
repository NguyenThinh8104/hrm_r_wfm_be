using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Domain.Entities;
using Modules.Shifts.Common;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ quản lý khung ca mẫu toàn hệ thống và chi nhánh (UC 1.3).
/// </summary>
public class ShiftTemplateService : IShiftTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ShiftTemplateService> _logger;

    public ShiftTemplateService(AppDbContext context, ILogger<ShiftTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

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

        // Ràng buộc đóng băng lịch ca: Thay đổi ca mẫu chỉ áp dụng từ tuần sau.
        // Tuần hiện tại, các ngày đã qua và các ca đã công bố phải giữ nguyên giờ ca cũ (Snapshot).
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        // 1. Đóng băng snapshot cho toàn bộ lịch ca của tuần hiện tại, quá khứ và các ca đã công bố
        var schedulesToLock = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && (ws.WorkDate < nextMonday || ws.Status == "PUBLISHED"))
            .ToListAsync();

        foreach (var ws in schedulesToLock)
        {
            if (!ws.SnapshotStartTime.HasValue)
            {
                ws.SnapshotShiftName = template.Name;
                ws.SnapshotStartTime = template.StartTime;
                ws.SnapshotEndTime = template.EndTime;
                ws.SnapshotIsOvernight = template.IsOvernight;
            }
        }

        // 2. Các ca ở trạng thái nháp (DRAFT) từ thứ Hai tuần sau trở đi sẽ nhận khung giờ mới
        var futureDraftSchedules = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate >= nextMonday && ws.Status == "DRAFT")
            .ToListAsync();

        foreach (var ws in futureDraftSchedules)
        {
            ws.SnapshotShiftName = !string.IsNullOrWhiteSpace(request.Name) ? request.Name.Trim() : template.Name;
            ws.SnapshotStartTime = newStartTime;
            ws.SnapshotEndTime = newEndTime;
            ws.SnapshotIsOvernight = newEndTime < newStartTime;
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

        // Ràng buộc đóng băng lịch ca: Thay đổi ca mẫu chỉ áp dụng từ tuần sau.
        // Tuần hiện tại, các ngày đã qua và các ca đã công bố phải giữ nguyên giờ ca cũ (Snapshot).
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        // 1. Đóng băng snapshot cho toàn bộ lịch ca của tuần hiện tại, quá khứ và các ca đã công bố
        var schedulesToLock = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && (ws.WorkDate < nextMonday || ws.Status == "PUBLISHED"))
            .ToListAsync();

        foreach (var ws in schedulesToLock)
        {
            if (!ws.SnapshotStartTime.HasValue)
            {
                ws.SnapshotShiftName = template.Name;
                ws.SnapshotStartTime = template.StartTime;
                ws.SnapshotEndTime = template.EndTime;
                ws.SnapshotIsOvernight = template.IsOvernight;
            }
        }

        // 2. Các ca ở trạng thái nháp (DRAFT) từ thứ Hai tuần sau trở đi sẽ nhận khung giờ mới
        var futureDraftSchedules = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate >= nextMonday && ws.Status == "DRAFT")
            .ToListAsync();

        foreach (var ws in futureDraftSchedules)
        {
            ws.SnapshotShiftName = dto.Name.Trim();
            ws.SnapshotStartTime = dto.StartTime;
            ws.SnapshotEndTime = dto.EndTime;
            ws.SnapshotIsOvernight = validation.IsOvernight;
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

        var hasSchedules = await _context.WorkSchedules.AnyAsync(ws => ws.ShiftTemplateId == id);
        if (!hasSchedules)
        {
            _context.ShiftTemplates.Remove(template);
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Đã xóa hoàn toàn khung ca mẫu khỏi hệ thống.");
        }

        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Khung ca đã có lịch phân công nên đã chuyển sang ngừng áp dụng.");
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

    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(ulong? branchId = null, bool includeInactive = false)
    {
        var query = _context.ShiftTemplates.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(st => st.IsActive);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId.Value);
            if (branch != null)
            {
                var isGlobal = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);
                if (isGlobal)
                {
                    query = query.Where(st => st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope));
                }
                else
                {
                    query = query.Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id);
                }
            }
            else
            {
                query = query.Where(st => st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope));
            }
        }
        else
        {
            // Khi không chỉ định branchId (ví dụ trang chung), CHỈ lấy ca chung GLOBAL.
            // Tuyệt đối không rò rỉ các ca riêng (BRANCH) của cơ sở khác!
            query = query.Where(st => st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope));
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

    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync()
    {
        return await GetAllShiftTemplatesAsync(includeInactive: false);
    }

}
