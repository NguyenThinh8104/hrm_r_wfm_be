using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Domain.Entities;
using Modules.Shifts.Common;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ quản lý khung ca mẫu toàn hệ thống và chi nhánh (UC 1.3).
/// </summary>
public class ShiftTemplateService : IShiftTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ShiftTemplateService> _logger;
    private readonly IEmailService _emailService;

    public ShiftTemplateService(
        AppDbContext context,
        ILogger<ShiftTemplateService> logger,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
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

    /// <summary>
    /// =========================================================================================
    /// LUỒNG DỮ LIỆU & NGHIỆP VỤ: TẠO KHUNG CA MẪU (CreateShiftTemplateAsync)
    /// -----------------------------------------------------------------------------------------
    /// 1. NƠI XUẤT PHÁT:
    ///    - Client gửi POST /api/shift-templates (CreateShiftTemplateRequest)
    ///    - Tiếp nhận bởi ShiftTemplatesController.CreateShiftTemplate
    ///
    /// 2. CÁC BẢNG CƠ SỞ DỮ LIỆU TÁC ĐỘNG:
    ///    - Bảng 'branches': Kiểm tra chi nhánh tồn tại và trạng thái khóa (nếu là ca riêng BRANCH).
    ///    - Bảng 'shift_templates': Kiểm tra trùng khung giờ (StartTime, EndTime) và trùng mã TemplateCode,
    ///                              sau đó INSERT bản ghi khung ca mới.
    ///    - Bảng 'system_audit_logs': Ghi nhận lịch sử thao tác tạo khung ca.
    ///
    /// 3. CÁC QUY TẮC NGHIỆP VỤ & TÍNH TOÁN (BUSINESS RULES):
    ///    - BƯỚC 1: Validate tên ca và cú pháp thời gian HH:mm (ShiftCalculationHelper.TryParseTime).
    ///              Giờ bắt đầu không được trùng giờ kết thúc.
    ///    - BƯỚC 2: Kiểm tra Phạm vi (Scope = 'GLOBAL' hoặc 'BRANCH').
    ///              - Nếu Scope = BRANCH: Bắt buộc có BranchId, kiểm tra chi nhánh có tồn tại trong bảng branches.
    ///                [Quy tắc 11]: Nếu chi nhánh bị khóa (IsBranchLocked), từ chối thêm ca (HTTP 423 Locked).
    ///              - Nếu Scope = GLOBAL: Gán BranchId = null (áp dụng toàn chuỗi).
    ///    - BƯỚC 3: [Quy tắc 8 - Chống trùng giờ]:
    ///              Trong cùng bộ ca (cùng Scope + BranchId), không cho phép 2 ca đang kích hoạt (IsActive)
    ///              có cùng StartTime và EndTime.
    ///    - BƯỚC 4: [Quy tắc 7 - Tự sinh TemplateCode]:
    ///              - Ca riêng BRANCH: Sinh mã theo tiền tố chi nhánh, ví dụ: 'HN01-S1', 'HN01-S2'.
    ///              - Ca chung GLOBAL: Nếu client không nhập, tự sinh 'CA_01', 'CA_02'...
    ///    - BƯỚC 5: TÍNH TOÁN GIỜ LÀM & CA QUA ĐÊM (ShiftCalculationHelper.CalculateHours):
    ///              - IsOvernight: Tính toán ở Server (EndTime < StartTime).
    ///              - PaidHours: Tổng giờ làm có trả lương (sau khi trừ thời gian nghỉ BreakDuration).
    ///              - NightHours: Số giờ rơi vào khung ban đêm (22:00 - 06:00).
    ///              - ShiftType: Tự động phân loại ca Sáng, Chiều, Tối hoặc Đêm dựa trên giờ bắt đầu.
    ///    - BƯỚC 6: Ghi Entity ShiftTemplate vào Database qua EF Core và lưu SystemAuditLog.
    ///    - BƯỚC 7: Ánh xạ sang ShiftTemplateDto và trả về kết quả thành công (HTTP 201 Created).
    /// =========================================================================================
    /// </summary>
    public async Task<ShiftOperationResult<ShiftTemplateDto>> CreateShiftTemplateAsync(
        CreateShiftTemplateRequest request, ulong? actorId = null, string? ipAddress = null)
    {
        // --- BƯỚC 1: VALIDATE DỮ LIỆU ĐẦU VÀO VÀ THỜI GIAN ---
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ShiftOperationResult<ShiftTemplateDto>.BadRequest("INVALID_DATA", "Tên ca làm việc không được để trống.");
        }

        // Kiểm tra tính hợp lệ của định dạng giờ HH:mm
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

        // --- BƯỚC 2: PHÂN LOẠI PHẠM VI (SCOPE) VÀ KIỂM TRA CHI NHÁNH ---
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

            // Truy vấn bảng 'branches' để xác minh chi nhánh tồn tại
            branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId.Value);
            if (branch == null)
            {
                return ShiftOperationResult<ShiftTemplateDto>.NotFound($"Không tìm thấy chi nhánh với ID {request.BranchId.Value}.");
            }

            // Quy tắc 11: Nếu chi nhánh đang bị khóa hoạt động, từ chối thêm ca riêng (HTTP 423 Locked)
            if (IsBranchLocked(branch))
            {
                return ShiftOperationResult<ShiftTemplateDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể thêm ca riêng.");
            }
        }
        else
        {
            request.BranchId = null;
        }

        // --- BƯỚC 3: KIỂM TRA TRÙNG LẶP KHUNG GIỜ LÀM VIỆC (QUY TẮC 8) ---
        // Truy vấn bảng 'shift_templates' để đảm bảo không có 2 ca trùng cả StartTime lẫn EndTime trong cùng phạm vi
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

        // --- BƯỚC 4: TỰ SINH MÃ KHUNG CA (TEMPLATE CODE - QUY TẮC 7) ---
        string templateCode;
        if (scope == "BRANCH")
        {
            // Ca riêng: Tự sinh mã theo tiền tố mã chi nhánh (ví dụ: HN01-S1, SG02-S2)
            var prefix = !string.IsNullOrWhiteSpace(branch!.BranchCode) ? branch.BranchCode.Trim() : $"BR{branch.Id}";
            var branchShiftsCount = await _context.ShiftTemplates
                .CountAsync(st => st.BranchId == branch.Id);
            int nextIndex = branchShiftsCount + 1;
            templateCode = $"{prefix}-S{nextIndex}".ToUpper();

            // Vòng lặp đảm bảo mã là duy nhất, không bị trùng với ca đã tồn tại
            while (await _context.ShiftTemplates.AnyAsync(st => st.TemplateCode.ToUpper() == templateCode))
            {
                nextIndex++;
                templateCode = $"{prefix}-S{nextIndex}".ToUpper();
            }
        }
        else
        {
            // Ca chung toàn chuỗi: Nếu người dùng nhập mã thì kiểm tra trùng lặp, nếu không nhập thì sinh dạng CA_01, CA_02
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

        // --- BƯỚC 5: TÍNH TOÁN CÁC THÔNG SỐ GIỜ LÀM VÀ CA QUA ĐÊM ---
        // Sử dụng ShiftCalculationHelper: Server tự tính IsOvernight (EndTime < StartTime), giờ trả lương, giờ đêm
        var (paidHours, nightHours, isOvernight) = ShiftCalculationHelper.CalculateHours(startTime, endTime, request.BreakDuration);

        // Tự động phân loại ca Sáng/Chiều/Tối/Đêm dựa vào giờ bắt đầu nếu người dùng không chọn thủ công
        var shiftType = !string.IsNullOrWhiteSpace(request.ShiftType)
            ? request.ShiftType.Trim().ToUpper()
            : ShiftCalculationHelper.InferShiftType(startTime);

        // --- BƯỚC 6: KHỞI TẠO VÀ LƯU BẢN GHI ENTITY VÀO DATABASE ---
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
            IsOvernight = isOvernight, // Gán giá trị tính toán từ Server
            BreakDurationMinutes = request.BreakDuration,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Ghi vào bảng 'shift_templates'
        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();

        // Ghi nhật ký kiểm toán vào bảng 'system_audit_logs'
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
            if (branch != null && IsBranchLocked(branch))
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
        // Tuần hiện tại và các ngày đã qua giữ nguyên 100% khung ca cũ (Snapshot).
        // Từ tuần sau trở đi (nextMonday), nếu đã xếp lịch hoặc công bố có chứa ca này thì xóa lịch và thông báo qua email.
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        // 1. Đóng băng snapshot cho toàn bộ lịch ca của tuần hiện tại và quá khứ (< nextMonday)
        var schedulesToLock = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate < nextMonday)
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

        // 2. Thu hồi toàn bộ lịch tuần sau trở đi (>= nextMonday) nếu đã xếp hoặc đã công bố, và gửi email thông báo cho nhân viên cơ sở
        string changeSummary = $"Giờ mới: {newStartTime:HH:mm} - {newEndTime:HH:mm}";
        await RevokeFutureSchedulesAndNotifyEmployeesAsync(template, nextMonday, changeSummary);

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
            if (branch != null && IsBranchLocked(branch))
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

        if (!isActive)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
            var currentMonday = today.AddDays(-daysSinceMonday);
            var nextMonday = currentMonday.AddDays(7);

            // Đóng băng tuần hiện tại
            var schedulesToLock = await _context.WorkSchedules
                .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate < nextMonday)
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

            // Thu hồi toàn bộ lịch tuần sau trở đi có ca này và gửi email thông báo cho nhân viên cơ sở
            await RevokeFutureSchedulesAndNotifyEmployeesAsync(template, nextMonday, "ngừng áp dụng khung ca");
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
        if (IsBranchLocked(branch))
        {
            return ShiftOperationResult<BranchEffectiveShiftsDto>.Locked("BRANCH_LOCKED", "Chi nhánh đang bị khóa, không thể đổi chế độ khung ca.");
        }

        // Quy tắc 2: Tự động kích hoạt ca riêng hoặc khởi tạo bộ ca riêng ban đầu từ ca chung khi bật CUSTOM
        if (normalizedMode == "CUSTOM")
        {
            var hasActiveCustom = await _context.ShiftTemplates
                .AnyAsync(st => st.Scope == "BRANCH" && st.BranchId == branchId && st.IsActive);

            if (!hasActiveCustom)
            {
                var inactiveCustoms = await _context.ShiftTemplates
                    .Where(st => st.Scope == "BRANCH" && st.BranchId == branchId && !st.IsActive)
                    .ToListAsync();

                if (inactiveCustoms.Count > 0)
                {
                    foreach (var s in inactiveCustoms)
                    {
                        s.IsActive = true;
                        s.UpdatedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }
                else
                {
                    var activeGlobals = await _context.ShiftTemplates
                        .Where(st => st.Scope == "GLOBAL" && st.IsActive)
                        .ToListAsync();

                    if (activeGlobals.Count > 0)
                    {
                        var newCustoms = activeGlobals.Select(g => new ShiftTemplate
                        {
                            Scope = "BRANCH",
                            BranchId = branchId,
                            Name = $"{g.Name} (Riêng)",
                            TemplateCode = $"CR_{branchId}_{g.TemplateCode.Replace("CA_", "").Replace("CR_", "")}",
                            Description = g.Description,
                            ShiftType = g.ShiftType,
                            StartTime = g.StartTime,
                            EndTime = g.EndTime,
                            BreakDurationMinutes = g.BreakDurationMinutes,
                            IsOvernight = g.IsOvernight,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }).ToList();

                        _context.ShiftTemplates.AddRange(newCustoms);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        return ShiftOperationResult<BranchEffectiveShiftsDto>.BadRequest(
                            "NO_ACTIVE_CUSTOM_SHIFT",
                            "Không thể chuyển sang chế độ ca riêng vì chi nhánh chưa có ca riêng nào đang hoạt động.");
                    }
                }
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

        // 1. Đóng băng snapshot cho toàn bộ lịch ca của tuần hiện tại và quá khứ (< nextMonday)
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        var schedulesToLock = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate < nextMonday)
            .ToListAsync();

        foreach (var ws in schedulesToLock)
        {
            if (!ws.SnapshotStartTime.HasValue && ws.ShiftTemplate != null)
            {
                ws.SnapshotShiftName = ws.ShiftTemplate.Name;
                ws.SnapshotStartTime = ws.ShiftTemplate.StartTime;
                ws.SnapshotEndTime = ws.ShiftTemplate.EndTime;
                ws.SnapshotIsOvernight = ws.ShiftTemplate.IsOvernight;
            }
        }

        await _context.SaveChangesAsync();

        // 2. Thu hồi toàn bộ lịch từ tuần sau (>= nextMonday) và thông báo qua email cho nhân viên cơ sở
        await RevokeFutureSchedulesForBranchAndNotifyEmployeesAsync(branch, nextMonday, normalizedMode);

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
        // Tuần hiện tại và các ngày đã qua giữ nguyên 100% khung ca cũ (Snapshot).
        // Từ tuần sau trở đi (nextMonday), nếu đã xếp lịch hoặc công bố có chứa ca này thì xóa lịch và thông báo qua email.
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        // 1. Đóng băng snapshot cho toàn bộ lịch ca của tuần hiện tại và quá khứ (< nextMonday)
        var schedulesToLock = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate < nextMonday)
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

        // 2. Thu hồi toàn bộ lịch tuần sau trở đi (>= nextMonday) nếu đã xếp hoặc đã công bố, và gửi email thông báo cho nhân viên cơ sở
        string changeSummary = $"Giờ mới: {dto.StartTime:HH:mm} - {dto.EndTime:HH:mm}";
        await RevokeFutureSchedulesAndNotifyEmployeesAsync(template, nextMonday, changeSummary);

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

        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var currentMonday = today.AddDays(-daysSinceMonday);
        var nextMonday = currentMonday.AddDays(7);

        // 1. Đóng băng snapshot cho tuần hiện tại và quá khứ (< nextMonday)
        var currentWeekSchedules = await _context.WorkSchedules
            .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate < nextMonday)
            .ToListAsync();

        foreach (var ws in currentWeekSchedules)
        {
            if (!ws.SnapshotStartTime.HasValue)
            {
                ws.SnapshotShiftName = template.Name;
                ws.SnapshotStartTime = template.StartTime;
                ws.SnapshotEndTime = template.EndTime;
                ws.SnapshotIsOvernight = template.IsOvernight;
            }
        }

        // 2. Thu hồi toàn bộ lịch tuần sau trở đi và thông báo qua email cho nhân viên cơ sở
        await RevokeFutureSchedulesAndNotifyEmployeesAsync(template, nextMonday, "xóa khung ca khỏi hệ thống");

        var schedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.ShiftTemplateId == id)
            .ToListAsync();

        var hasAttendance = schedules.Any(ws => ws.ShiftAssignments != null && ws.ShiftAssignments.Any(sa => sa.AttendanceLog != null));

        if (!hasAttendance)
        {
            if (schedules.Any())
            {
                foreach (var ws in schedules)
                {
                    if (ws.ShiftAssignments != null && ws.ShiftAssignments.Any())
                    {
                        _context.ShiftAssignments.RemoveRange(ws.ShiftAssignments);
                    }
                }
                _context.WorkSchedules.RemoveRange(schedules);
            }

            _context.ShiftTemplates.Remove(template);
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Đã xóa hoàn toàn khung ca mẫu khỏi hệ thống.");
        }

        // Nếu đã có dữ liệu chấm công thực tế gắn liền, thực hiện ngưng áp dụng (soft delete) để bảo toàn dữ liệu chấm công
        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Khung ca đã có dữ liệu chấm công thực tế nên đã chuyển sang ngừng áp dụng.");
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

    /// <summary>
    /// Thu hồi toàn bộ lịch làm việc từ tuần sau trở đi (>= nextMonday) nếu đã xếp lịch hoặc công bố có chứa ca mẫu bị đổi,
    /// đồng thời gửi email thông báo tới toàn bộ nhân viên của các cơ sở bị ảnh hưởng.
    /// </summary>
    private async Task RevokeFutureSchedulesAndNotifyEmployeesAsync(
        ShiftTemplate template,
        DateOnly nextMonday,
        string changeSummary)
    {
        try
        {
            // 1. Tìm toàn bộ các WorkSchedule từ tuần sau trở đi liên quan đến mẫu ca này
            var futureSchedules = await _context.WorkSchedules
                .Include(ws => ws.Branch)
                .Include(ws => ws.ShiftAssignments)
                    .ThenInclude(sa => sa.User)
                .Where(ws => ws.ShiftTemplateId == template.Id && ws.WorkDate >= nextMonday)
                .ToListAsync();

            if (!futureSchedules.Any())
            {
                return;
            }

            // 2. Xác định các tuần và chi nhánh bị ảnh hưởng (gom nhóm theo BranchId & WeekStartDate)
            var affectedWeeks = futureSchedules
                .GroupBy(ws => new
                {
                    ws.BranchId,
                    WeekStartDate = ws.WorkDate.AddDays(-(ws.WorkDate.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)ws.WorkDate.DayOfWeek - 1))
                })
                .Select(g => new
                {
                    g.Key.BranchId,
                    g.Key.WeekStartDate,
                    BranchName = g.First().Branch?.Name ?? $"Chi nhánh #{g.Key.BranchId}"
                })
                .ToList();

            // 3. Với mỗi tuần bị ảnh hưởng của mỗi chi nhánh: Xóa các WorkSchedule và ShiftAssignment
            var allAssignmentsToDelete = new List<ShiftAssignment>();
            var allSchedulesToDelete = new List<WorkSchedule>();
            var usersToEmailByBranch = new Dictionary<ulong, List<User>>();

            foreach (var week in affectedWeeks)
            {
                var weekEnd = week.WeekStartDate.AddDays(6);
                var weekSchedules = await _context.WorkSchedules
                    .Include(ws => ws.ShiftAssignments)
                        .ThenInclude(sa => sa.User)
                    .Where(ws => ws.BranchId == week.BranchId && ws.WorkDate >= week.WeekStartDate && ws.WorkDate <= weekEnd)
                    .ToListAsync();

                foreach (var ws in weekSchedules)
                {
                    if (ws.ShiftAssignments != null && ws.ShiftAssignments.Any())
                    {
                        allAssignmentsToDelete.AddRange(ws.ShiftAssignments);
                    }
                }
                allSchedulesToDelete.AddRange(weekSchedules);

                if (!usersToEmailByBranch.ContainsKey(week.BranchId))
                {
                    var branchUsers = await _context.Users
                        .Where(u => u.HomeBranchId == week.BranchId && u.Status == "ACTIVE" && !string.IsNullOrEmpty(u.Email))
                        .ToListAsync();
                    usersToEmailByBranch[week.BranchId] = branchUsers;
                }
            }

            if (allAssignmentsToDelete.Any())
            {
                _context.ShiftAssignments.RemoveRange(allAssignmentsToDelete.DistinctBy(sa => sa.Id));
            }

            if (allSchedulesToDelete.Any())
            {
                _context.WorkSchedules.RemoveRange(allSchedulesToDelete.DistinctBy(ws => ws.Id));
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Đã xóa {AssignCount} phân công và {ScheduleCount} lịch làm việc từ {NextMonday} do thay đổi ca mẫu {TemplateId}.",
                allAssignmentsToDelete.Count, allSchedulesToDelete.Count, nextMonday, template.Id);

            // 4. Gửi email thông báo bất đồng bộ cho nhân viên cơ sở
            _ = Task.Run(async () =>
            {
                foreach (var week in affectedWeeks)
                {
                    if (!usersToEmailByBranch.TryGetValue(week.BranchId, out var branchEmployees) || !branchEmployees.Any())
                    {
                        continue;
                    }

                    foreach (var emp in branchEmployees)
                    {
                        try
                        {
                            await _emailService.SendShiftChangeNotificationEmailAsync(
                                emp.Email,
                                emp.FullName,
                                "Điều chỉnh khung ca & Thu hồi lịch tuần sau",
                                $"Ban Quản Trị đã cập nhật khung ca mẫu '{template.Name}' ({changeSummary}). Lịch làm việc tuần ({week.WeekStartDate:dd/MM/yyyy} - {week.WeekStartDate.AddDays(6):dd/MM/yyyy}) tại cơ sở '{week.BranchName}' đã được tự động thu hồi để Quản lý cơ sở sắp xếp lại theo khung ca mới.",
                                nextMonday.ToString("dd/MM/yyyy"),
                                "Lịch làm việc tuần hiện tại của bạn vẫn được giữ nguyên không thay đổi. Vui lòng theo dõi ứng dụng để cập nhật lịch làm việc mới sau khi Quản lý chi nhánh công bố lại.",
                                "ĐÃ THU HỒI LỊCH");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Lỗi gửi email thu hồi lịch cho {Email}", emp.Email);
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thu hồi lịch tương lai cho ca mẫu {TemplateId}", template.Id);
        }
    }

    private async Task RevokeFutureSchedulesForBranchAndNotifyEmployeesAsync(
        Branch branch, DateOnly nextMonday, string newMode)
    {
        try
        {
            var futureSchedules = await _context.WorkSchedules
                .Include(ws => ws.Branch)
                .Include(ws => ws.ShiftAssignments)
                    .ThenInclude(sa => sa.User)
                .Where(ws => ws.BranchId == branch.Id && ws.WorkDate >= nextMonday)
                .ToListAsync();

            if (!futureSchedules.Any())
            {
                return;
            }

            var affectedWeeks = futureSchedules
                .GroupBy(ws => ws.WorkDate.AddDays(-(ws.WorkDate.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)ws.WorkDate.DayOfWeek - 1)))
                .Select(g => new
                {
                    WeekStartDate = g.Key,
                    BranchName = branch.Name
                })
                .ToList();

            var allAssignmentsToDelete = new List<ShiftAssignment>();
            var allSchedulesToDelete = new List<WorkSchedule>();

            foreach (var week in affectedWeeks)
            {
                var weekEnd = week.WeekStartDate.AddDays(6);
                var weekSchedules = await _context.WorkSchedules
                    .Include(ws => ws.ShiftAssignments)
                    .Where(ws => ws.BranchId == branch.Id && ws.WorkDate >= week.WeekStartDate && ws.WorkDate <= weekEnd)
                    .ToListAsync();

                foreach (var ws in weekSchedules)
                {
                    if (ws.ShiftAssignments != null && ws.ShiftAssignments.Any())
                    {
                        allAssignmentsToDelete.AddRange(ws.ShiftAssignments);
                    }
                }
                allSchedulesToDelete.AddRange(weekSchedules);
            }

            if (allAssignmentsToDelete.Any())
            {
                _context.ShiftAssignments.RemoveRange(allAssignmentsToDelete.DistinctBy(sa => sa.Id));
            }

            if (allSchedulesToDelete.Any())
            {
                _context.WorkSchedules.RemoveRange(allSchedulesToDelete.DistinctBy(ws => ws.Id));
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Đã xóa {AssignCount} phân công và {ScheduleCount} lịch làm việc từ {NextMonday} do thay đổi chế độ ca cho chi nhánh {BranchId}.",
                allAssignmentsToDelete.Count, allSchedulesToDelete.Count, nextMonday, branch.Id);

            var branchUsers = await _context.Users
                .Where(u => u.HomeBranchId == branch.Id && u.Status == "ACTIVE" && !string.IsNullOrEmpty(u.Email))
                .ToListAsync();

            if (branchUsers.Any())
            {
                string modeText = newMode == "CUSTOM" ? "Ca riêng chi nhánh" : "Ca chung toàn hệ thống";
                _ = Task.Run(async () =>
                {
                    foreach (var week in affectedWeeks)
                    {
                        foreach (var emp in branchUsers)
                        {
                            try
                            {
                                await _emailService.SendShiftChangeNotificationEmailAsync(
                                    emp.Email,
                                    emp.FullName,
                                    "Điều chỉnh chế độ khung ca & Thu hồi lịch tuần sau",
                                    $"Ban Quản Trị đã cập nhật chế độ khung ca của cơ sở '{branch.Name}' sang '{modeText}'. Lịch làm việc tuần ({week.WeekStartDate:dd/MM/yyyy} - {week.WeekStartDate.AddDays(6):dd/MM/yyyy}) tại cơ sở '{branch.Name}' đã được tự động thu hồi để Quản lý cơ sở sắp xếp lại theo khung ca mới.",
                                    nextMonday.ToString("dd/MM/yyyy"),
                                    "Lịch làm việc tuần hiện tại của bạn vẫn được giữ nguyên không thay đổi. Vui lòng theo dõi ứng dụng để cập nhật lịch làm việc mới sau khi Quản lý chi nhánh công bố lại.",
                                    "ĐÃ THU HỒI LỊCH");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Lỗi gửi email thu hồi lịch cho {Email}", emp.Email);
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thu hồi lịch tương lai khi đổi chế độ ca cho chi nhánh {BranchId}", branch.Id);
        }
    }

    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync()
    {
        return await GetAllShiftTemplatesAsync(includeInactive: false);
    }

    private static bool IsBranchLocked(Branch? branch)
    {
        if (branch == null) return false;
        if (string.Equals(branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return branch.LockedAt != null && (branch.UnlockedAt == null || branch.LockedAt > branch.UnlockedAt);
    }

}
