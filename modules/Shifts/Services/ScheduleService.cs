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

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ quản lý khung lịch tuần/tháng, định mức nhu cầu nhân sự và phân bổ ca làm việc (UC 2.1 & UC 2.3).
/// </summary>
public class ScheduleService : IScheduleService
{
    private readonly AppDbContext _context;
    private readonly IShiftTemplateService _templateService;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        AppDbContext context,
        IShiftTemplateService templateService,
        ILogger<ScheduleService> logger)
    {
        _context = context;
        _templateService = templateService;
        _logger = logger;
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

        var isGlobal = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);

        List<ShiftTemplate> templates;
        if (dto.TemplateIds != null && dto.TemplateIds.Any())
        {
            var templateQuery = _context.ShiftTemplates
                .Where(st => dto.TemplateIds.Contains(st.Id) && st.IsActive);

            if (isGlobal)
            {
                templateQuery = templateQuery.Where(st => st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope));
            }
            else
            {
                templateQuery = templateQuery.Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id);
            }

            templates = await templateQuery.ToListAsync();
        }
        else
        {
            if (isGlobal)
            {
                templates = await _context.ShiftTemplates
                    .Where(st => (st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope)) && st.IsActive)
                    .OrderBy(st => st.StartTime)
                    .ToListAsync();
            }
            else
            {
                templates = await _context.ShiftTemplates
                    .Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id && st.IsActive)
                    .OrderBy(st => st.StartTime)
                    .ToListAsync();
            }
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
                        SnapshotShiftName = template.Name,
                        SnapshotStartTime = template.StartTime,
                        SnapshotEndTime = template.EndTime,
                        SnapshotIsOvernight = template.IsOvernight,
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
        WorkSchedule? schedule = null;
        if (dto.ScheduleId > 0)
        {
            schedule = await _context.WorkSchedules
                .Include(ws => ws.Branch)
                .Include(ws => ws.ShiftTemplate)
                .Include(ws => ws.ShiftAssignments)
                    .ThenInclude(sa => sa.AssignedRole)
                .FirstOrDefaultAsync(ws => ws.Id == dto.ScheduleId);
        }
        else if (dto.BranchId.HasValue && dto.ShiftTemplateId.HasValue && dto.WorkDate.HasValue)
        {
            schedule = await _context.WorkSchedules
                .Include(ws => ws.Branch)
                .Include(ws => ws.ShiftTemplate)
                .Include(ws => ws.ShiftAssignments)
                    .ThenInclude(sa => sa.AssignedRole)
                .FirstOrDefaultAsync(ws => ws.BranchId == dto.BranchId.Value 
                                        && ws.ShiftTemplateId == dto.ShiftTemplateId.Value 
                                        && ws.WorkDate == dto.WorkDate.Value);

            if (schedule == null)
            {
                var template = await _context.ShiftTemplates.FindAsync(dto.ShiftTemplateId.Value);
                if (template == null) return ApiResponse<WorkScheduleDto>.Fail("Không tìm thấy mẫu ca.");
                var branch = await _context.Branches.FindAsync(dto.BranchId.Value);
                if (branch == null) return ApiResponse<WorkScheduleDto>.Fail("Không tìm thấy chi nhánh.");

                schedule = new WorkSchedule
                {
                    BranchId = dto.BranchId.Value,
                    ShiftTemplateId = dto.ShiftTemplateId.Value,
                    WorkDate = dto.WorkDate.Value,
                    SnapshotShiftName = template.Name,
                    SnapshotStartTime = template.StartTime,
                    SnapshotEndTime = template.EndTime,
                    SnapshotIsOvernight = template.IsOvernight,
                    RequiredCashier = dto.RequiredCashier,
                    RequiredSales = dto.RequiredSales,
                    RequiredSecurity = dto.RequiredSecurity,
                    Status = "DRAFT",
                    CreatedBy = 1,
                    CreatedAt = DateTime.UtcNow,
                    Branch = branch,
                    ShiftTemplate = template
                };
                _context.WorkSchedules.Add(schedule);
                await _context.SaveChangesAsync();
            }
        }

        if (schedule == null)
        {
            return ApiResponse<WorkScheduleDto>.Fail("Không tìm thấy ca trực trong lịch làm việc.");
        }

        if (schedule.Branch != null && !string.Equals(schedule.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<WorkScheduleDto>.Fail($"Chi nhánh '{schedule.Branch.Name}' đã bị khóa. Không thể điều chỉnh ca làm việc.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (schedule.WorkDate <= today)
        {
            return ApiResponse<WorkScheduleDto>.Fail("Không thể điều chỉnh định mức ca làm việc của ngày hôm nay hoặc ngày đã qua.");
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
            BranchName = schedule.Branch?.Name ?? string.Empty,
            ShiftTemplateId = schedule.ShiftTemplateId,
            ShiftTemplateName = schedule.SnapshotShiftName ?? schedule.ShiftTemplate?.Name ?? string.Empty,
            StartTime = schedule.SnapshotStartTime ?? schedule.ShiftTemplate?.StartTime ?? default,
            EndTime = schedule.SnapshotEndTime ?? schedule.ShiftTemplate?.EndTime ?? default,
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

        return ApiResponse<WorkScheduleDto>.Ok(resultDto, "Cập nhật định mức ca trực thành công!");
    }

    /// <summary>
    /// Xóa 1 bản ghi khung ca làm việc ở trạng thái DRAFT.
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteScheduleAsync(ulong scheduleId)
    {
        var schedule = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .FirstOrDefaultAsync(ws => ws.Id == scheduleId);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca trực trong lịch làm việc.");
        }

        if (schedule.Status != "DRAFT")
        {
            return ApiResponse<bool>.Fail("Chỉ có thể xóa khung ca ở trạng thái DRAFT (chưa công bố).");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (schedule.WorkDate <= today)
        {
            return ApiResponse<bool>.Fail("Không thể xóa khung ca của ngày hôm nay hoặc ngày đã qua.");
        }

        if (schedule.ShiftAssignments.Any())
        {
            _context.ShiftAssignments.RemoveRange(schedule.ShiftAssignments);
        }

        _context.WorkSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã xóa khung ca thành công.");
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
            .ThenBy(ws => ws.SnapshotStartTime ?? ws.ShiftTemplate.StartTime)
            .ToListAsync();

        var result = schedules.Select(ws => new WorkScheduleDto
        {
            ScheduleId = ws.Id,
            BranchId = ws.BranchId,
            BranchName = ws.Branch.Name,
            ShiftTemplateId = ws.ShiftTemplateId,
            ShiftTemplateName = ws.SnapshotShiftName ?? ws.ShiftTemplate.Name,
            StartTime = ws.SnapshotStartTime ?? ws.ShiftTemplate.StartTime,
            EndTime = ws.SnapshotEndTime ?? ws.ShiftTemplate.EndTime,
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
            if (item.WorkDate <= today)
            {
                errors.Add($"Ngày {item.WorkDate:dd/MM/yyyy} là ngày hôm nay hoặc ngày trong quá khứ, không thể phân công ca.");
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
                    SnapshotShiftName = shiftTemplate.Name,
                    SnapshotStartTime = shiftTemplate.StartTime,
                    SnapshotEndTime = shiftTemplate.EndTime,
                    SnapshotIsOvernight = shiftTemplate.IsOvernight,
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
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate.Year == year && ws.WorkDate.Month == month)
            .ToListAsync();

        if (!schedules.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy lịch ca tháng {month}/{year} để phát hành.");
        }

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            if (!schedule.SnapshotStartTime.HasValue && schedule.ShiftTemplate != null)
            {
                schedule.SnapshotShiftName = schedule.ShiftTemplate.Name;
                schedule.SnapshotStartTime = schedule.ShiftTemplate.StartTime;
                schedule.SnapshotEndTime = schedule.ShiftTemplate.EndTime;
                schedule.SnapshotIsOvernight = schedule.ShiftTemplate.IsOvernight;
            }
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

        var isGlobal = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);

        List<ShiftTemplate> templates;
        if (dto.TemplateIds != null && dto.TemplateIds.Any())
        {
            var templateQuery = _context.ShiftTemplates
                .Where(st => dto.TemplateIds.Contains(st.Id) && st.IsActive);

            if (isGlobal)
            {
                templateQuery = templateQuery.Where(st => st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope));
            }
            else
            {
                templateQuery = templateQuery.Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id);
            }

            templates = await templateQuery.ToListAsync();
        }
        else
        {
            if (isGlobal)
            {
                templates = await _context.ShiftTemplates
                    .Where(st => (st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope)) && st.IsActive)
                    .OrderBy(st => st.StartTime)
                    .ToListAsync();
            }
            else
            {
                templates = await _context.ShiftTemplates
                    .Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id && st.IsActive)
                    .OrderBy(st => st.StartTime)
                    .ToListAsync();
            }
        }

        if (!templates.Any())
        {
            return ApiResponse<WeeklyScheduleMatrixDto>.Fail("Không có mẫu ca nào hoạt động để sinh lịch tuần.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        var existingSchedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftAssignments)
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate >= dto.WeekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        // 1. Nếu người dùng chỉ chọn một số mẫu ca (dto.TemplateIds có giá trị), 
        // dọn sạch các ca DRAFT của những mẫu ca KHÔNG được chọn trong tuần
        if (dto.TemplateIds != null && dto.TemplateIds.Any())
        {
            var selectedTemplateIdSet = templates.Select(t => t.Id).ToHashSet();
            var unselectedDraftSchedules = existingSchedules
                .Where(ws => ws.Status == "DRAFT" && !selectedTemplateIdSet.Contains(ws.ShiftTemplateId))
                .ToList();

            if (unselectedDraftSchedules.Any())
            {
                var unselectedScheduleIds = unselectedDraftSchedules.Select(ws => ws.Id).ToList();
                var assignmentsToDelete = await _context.ShiftAssignments
                    .Where(sa => unselectedScheduleIds.Contains(sa.ScheduleId))
                    .ToListAsync();
                if (assignmentsToDelete.Any())
                {
                    _context.ShiftAssignments.RemoveRange(assignmentsToDelete);
                }
                _context.WorkSchedules.RemoveRange(unselectedDraftSchedules);

                existingSchedules.RemoveAll(ws => unselectedDraftSchedules.Contains(ws));
            }
        }

        var existingDict = existingSchedules
            .GroupBy(ws => $"{ws.ShiftTemplateId}_{ws.WorkDate:yyyy-MM-dd}")
            .ToDictionary(g => g.Key, g => g.First());

        var newSchedules = new List<WorkSchedule>();

        for (int i = 0; i < 7; i++)
        {
            var workDate = dto.WeekStartDate.AddDays(i);
            foreach (var template in templates)
            {
                var key = $"{template.Id}_{workDate:yyyy-MM-dd}";
                if (existingDict.TryGetValue(key, out var existing))
                {
                    // Cập nhật định mức cho các ca DRAFT
                    if (existing.Status == "DRAFT")
                    {
                        existing.RequiredCashier = dto.DefaultRequiredCashier;
                        existing.RequiredSales = dto.DefaultRequiredSales;
                        existing.RequiredSecurity = dto.DefaultRequiredSecurity;
                        existing.SnapshotShiftName = template.Name;
                        existing.SnapshotStartTime = template.StartTime;
                        existing.SnapshotEndTime = template.EndTime;
                        existing.SnapshotIsOvernight = template.IsOvernight;
                    }
                }
                else
                {
                    // Khởi tạo ca DRAFT mới
                    newSchedules.Add(new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = template.Id,
                        WorkDate = workDate,
                        SnapshotShiftName = template.Name,
                        SnapshotStartTime = template.StartTime,
                        SnapshotEndTime = template.EndTime,
                        SnapshotIsOvernight = template.IsOvernight,
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
        }

        await _context.SaveChangesAsync();

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

        var isGlobalBranch = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);

        var schedulesQuery = _context.WorkSchedules
            .Include(ws => ws.Branch)
            .Include(ws => ws.ShiftTemplate)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.User)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEndDate);

        if (isGlobalBranch)
        {
            schedulesQuery = schedulesQuery.Where(ws => ws.ShiftTemplate.Scope == "GLOBAL" || string.IsNullOrEmpty(ws.ShiftTemplate.Scope));
        }
        else
        {
            schedulesQuery = schedulesQuery.Where(ws => ws.ShiftTemplate.Scope == "BRANCH" && ws.ShiftTemplate.BranchId == branchId);
        }

        var schedules = await schedulesQuery
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.SnapshotStartTime ?? ws.ShiftTemplate.StartTime)
            .ToListAsync();

        var scheduleDtos = schedules.Select(ws => new WorkScheduleDto
        {
            ScheduleId = ws.Id,
            BranchId = ws.BranchId,
            BranchName = ws.Branch.Name,
            ShiftTemplateId = ws.ShiftTemplateId,
            ShiftTemplateName = ws.SnapshotShiftName ?? ws.ShiftTemplate.Name,
            StartTime = ws.SnapshotStartTime ?? ws.ShiftTemplate.StartTime,
            EndTime = ws.SnapshotEndTime ?? ws.ShiftTemplate.EndTime,
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
                ShiftName = sa.Schedule.SnapshotShiftName ?? sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.SnapshotStartTime ?? sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.SnapshotEndTime ?? sa.Schedule.ShiftTemplate.EndTime,
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

        var activeTemplatesRes = await _templateService.GetAllShiftTemplatesAsync(branchId, false);

        // Khóa tiêu đề cột ca mẫu theo giờ đóng băng của tuần đang xem nếu các ca trong tuần đã có snapshot
        var templatesForWeek = (activeTemplatesRes.Data ?? new List<ShiftDto>()).Select(t =>
        {
            var matchingSchedule = schedules.FirstOrDefault(s => s.ShiftTemplateId == t.Id && s.SnapshotStartTime.HasValue);
            if (matchingSchedule != null)
            {
                return new ShiftDto
                {
                    Id = t.Id,
                    TemplateCode = t.TemplateCode,
                    Name = matchingSchedule.SnapshotShiftName ?? t.Name,
                    ShiftType = t.ShiftType,
                    StartTime = matchingSchedule.SnapshotStartTime.Value.ToString("HH:mm"),
                    EndTime = matchingSchedule.SnapshotEndTime.HasValue 
                        ? matchingSchedule.SnapshotEndTime.Value.ToString("HH:mm") 
                        : t.EndTime,
                    IsOvernight = matchingSchedule.SnapshotIsOvernight ?? t.IsOvernight,
                    BreakDuration = t.BreakDuration,
                    Scope = t.Scope,
                    BranchId = t.BranchId,
                    BranchName = t.BranchName,
                    IsActive = t.IsActive,
                    Status = t.Status
                };
            }
            return t;
        }).ToList();

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
            ActiveTemplates = templatesForWeek
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

                if (workDate <= DateOnly.FromDateTime(DateTime.Today))
                {
                    errors.Add($"Ngày {workDate:dd/MM/yyyy} là ngày hôm nay hoặc ngày trong quá khứ, không thể phân công ca.");
                    continue;
                }

                // Kiểm tra điều động nhân sự (UC 4 & UC 2.1)
                ulong trueHomeBranchId = user.OriginalHomeBranchId ?? user.HomeBranchId ?? 0;
                bool isAtHomeBranch = trueHomeBranchId == dto.BranchId;

                if (!isAtHomeBranch)
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
                            errors.Add($"NV '{user.FullName}' được điều chuyển từ ngày {futureDispatch.StartDate:dd/MM} đến {futureDispatch.EndDate:dd/MM}. Không thể xếp ca ngày {workDate:dd/MM}.");
                        }
                        else
                        {
                            var expiredDispatch = await _context.TemporaryDispatches
                                .Include(d => d.DispatchEmployees)
                                .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                                       && d.TargetBranchId == dto.BranchId 
                                                       && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                                       && d.EndDate < workDate);

                            if (expiredDispatch != null)
                            {
                                errors.Add($"Lệnh điều chuyển của NV '{user.FullName}' tại chi nhánh này đã kết thúc ngày {expiredDispatch.EndDate:dd/MM}. Không thể xếp ca ngày {workDate:dd/MM} do quá hạn điều chuyển.");
                            }
                            else
                            {
                                errors.Add($"NV '{user.FullName}' không thuộc chi nhánh và không có lệnh điều động ngày {workDate:dd/MM}.");
                            }
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
                        errors.Add($"NV '{user.FullName}' đã được điều chuyển sang '{outgoingDispatch.TargetBranch?.Name ?? "cơ sở khác"}' ngày {workDate:dd/MM}.");
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

                // Kiểm tra khoảng nghỉ tối thiểu 12 giờ giữa 2 ca của 2 ngày liên tiếp
                var restCheck = await CheckMinRestBetweenShiftsAsync(
                    workDate,
                    user.Id,
                    null,
                    shiftTemplate.StartTime,
                    shiftTemplate.EndTime,
                    shiftTemplate.IsOvernight,
                    isRequester: false);

                if (!restCheck.IsValid)
                {
                    errors.Add($"NV '{user.FullName}' không thể gán ca ngày {workDate:dd/MM}: {restCheck.ErrorMessage}");
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
                        SnapshotShiftName = shiftTemplate.Name,
                        SnapshotStartTime = shiftTemplate.StartTime,
                        SnapshotEndTime = shiftTemplate.EndTime,
                        SnapshotIsOvernight = shiftTemplate.IsOvernight,
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
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == branchId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        if (!schedules.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy lịch ca tuần từ {weekStartDate:dd/MM/yyyy} để công bố.");
        }

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            if (!schedule.SnapshotStartTime.HasValue && schedule.ShiftTemplate != null)
            {
                schedule.SnapshotShiftName = schedule.ShiftTemplate.Name;
                schedule.SnapshotStartTime = schedule.ShiftTemplate.StartTime;
                schedule.SnapshotEndTime = schedule.ShiftTemplate.EndTime;
                schedule.SnapshotIsOvernight = schedule.ShiftTemplate.IsOvernight;
            }
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
        if (assignment.Schedule != null && assignment.Schedule.WorkDate <= today)
        {
            return ApiResponse<bool>.Fail("Không thể hủy/xóa phân công ca làm việc của ngày hôm nay hoặc ngày đã trôi qua.");
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

        // 1. Lấy danh sách mẫu ca chuẩn áp dụng của chi nhánh
        var isGlobal = string.Equals(branch.ShiftMode, "GLOBAL", StringComparison.OrdinalIgnoreCase);
        List<ShiftTemplate> templates;
        if (isGlobal)
        {
            templates = await _context.ShiftTemplates
                .Where(st => (st.Scope == "GLOBAL" || string.IsNullOrEmpty(st.Scope)) && st.IsActive)
                .OrderBy(st => st.StartTime)
                .ToListAsync();
        }
        else
        {
            templates = await _context.ShiftTemplates
                .Where(st => st.Scope == "BRANCH" && st.BranchId == branch.Id && st.IsActive)
                .OrderBy(st => st.StartTime)
                .ToListAsync();
        }

        if (!templates.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Không có mẫu ca nào hoạt động trong hệ thống.");
        }

        // 2. Kiểm tra khung ca hiện có của tuần
        var existingSchedules = await _context.WorkSchedules
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == dto.BranchId && ws.WorkDate >= dto.WeekStartDate && ws.WorkDate <= weekEndDate)
            .ToListAsync();

        if (!existingSchedules.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Chưa có khung ca nào được thiết lập cho tuần này. Vui lòng bấm 'Thiết lập định mức tuần' để chọn các ca áp dụng trước khi chạy xếp ca tự động.");
        }

        // CHỈ tự động xếp cho các mẫu ca mà Quản lý cửa hàng đã thiết lập / chọn cho tuần này.
        // Tuyệt đối không tự ý kéo tất cả các ca mẫu active của Admin vào cơ sở!
        var activeTemplateIdsInWeek = existingSchedules.Select(ws => ws.ShiftTemplateId).Distinct().ToHashSet();
        templates = templates.Where(t => activeTemplateIdsInWeek.Contains(t.Id)).ToList();

        if (!templates.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Không tìm thấy mẫu ca hoạt động nào tương ứng với các ca trực của tuần này.");
        }

        var existingKeys = existingSchedules.Select(ws => (ws.WorkDate, ws.ShiftTemplateId)).ToHashSet();
        var toAdd = new List<WorkSchedule>();

        for (int d = 0; d < 7; d++)
        {
            var workDate = dto.WeekStartDate.AddDays(d);
            foreach (var template in templates)
            {
                if (!existingKeys.Contains((workDate, template.Id)))
                {
                    var sampleSchedule = existingSchedules.FirstOrDefault(s => s.ShiftTemplateId == template.Id);
                    toAdd.Add(new WorkSchedule
                    {
                        BranchId = dto.BranchId,
                        ShiftTemplateId = template.Id,
                        WorkDate = workDate,
                        SnapshotShiftName = template.Name,
                        SnapshotStartTime = template.StartTime,
                        SnapshotEndTime = template.EndTime,
                        SnapshotIsOvernight = template.IsOvernight,
                        RequiredCashier = sampleSchedule?.RequiredCashier ?? 1,
                        RequiredSales = sampleSchedule?.RequiredSales ?? 1,
                        RequiredSecurity = sampleSchedule?.RequiredSecurity ?? 1,
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
            .Where(ws => ws.BranchId == dto.BranchId 
                      && ws.WorkDate >= dto.WeekStartDate 
                      && ws.WorkDate <= weekEndDate 
                      && activeTemplateIdsInWeek.Contains(ws.ShiftTemplateId))
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
            .Where(u => ((u.OriginalHomeBranchId ?? u.HomeBranchId) == dto.BranchId || u.HomeBranchId == dto.BranchId || incomingUserIds.Contains(u.Id)) && u.Status == "ACTIVE")
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

                ulong empHomeBranchId = emp.OriginalHomeBranchId ?? emp.HomeBranchId ?? 0;
                bool isEmpAtHomeBranch = empHomeBranchId == dto.BranchId;

                // Nếu là nhân sự điều chuyển đến (khác chi nhánh gốc)
                if (!isEmpAtHomeBranch)
                {
                    var inDisp = incomingDispatches.FirstOrDefault(x => 
                        (x.UserId == emp.Id || x.DispatchEmployees.Any(de => de.UserId == emp.Id && de.Status == "APPROVED"))
                        && x.StartDate <= workDate && workDate <= x.EndDate);

                    if (inDisp != null)
                    {
                        // Chỉ khả dụng trong khoảng [StartDate, EndDate]
                        employeeAvailableDates[emp.Id].Add(workDate);
                    }
                    continue;
                }

                // Nhân sự thuộc cơ sở gốc: Kiểm tra xem ngày này có bị điều chuyển đi nơi khác không
                var outDisp = outgoingDispatches.FirstOrDefault(x => 
                    (x.UserId == emp.Id || x.DispatchEmployees.Any(de => de.UserId == emp.Id && de.Status == "APPROVED")) 
                    && x.StartDate <= workDate && workDate <= x.EndDate);

                if (outDisp != null)
                {
                    // Ngày này đang làm việc ở cơ sở khác, không khả dụng ở đây
                    continue;
                }

                employeeAvailableDates[emp.Id].Add(workDate);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Thu thập các ca làm việc ĐÃ CÓ của nhân viên trong các ngày đã qua và hôm nay (workDate <= today)
        var pastAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
            .Where(sa => sa.Schedule.BranchId == dto.BranchId 
                      && sa.Schedule.WorkDate >= dto.WeekStartDate 
                      && sa.Schedule.WorkDate <= today)
            .Select(sa => new SingleAssignmentItemDto
            {
                UserId = sa.UserId,
                ShiftTemplateId = sa.Schedule.ShiftTemplateId,
                WorkDate = sa.Schedule.WorkDate
            })
            .ToListAsync();

        // 3. Nếu cho phép ghi đè, CHỈ XÓA các phân công của các ngày tương lai (WorkDate > today)
        // Tuyệt đối giữ nguyên lịch phân công của các ngày đã qua và ngày hôm nay
        if (dto.OverwriteExisting)
        {
            var futureAssignmentsToDelete = await _context.ShiftAssignments
                .Where(sa => sa.Schedule.BranchId == dto.BranchId 
                          && sa.Schedule.WorkDate > today 
                          && sa.Schedule.WorkDate <= weekEndDate)
                .ToListAsync();

            if (futureAssignmentsToDelete.Any())
            {
                var oldAssignmentIds = futureAssignmentsToDelete.Select(sa => sa.Id).ToList();
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

                _context.ShiftAssignments.RemoveRange(futureAssignmentsToDelete);
                await _context.SaveChangesAsync();
            }
        }

        // 5. Chạy solver Google OR-Tools CP-SAT với các ca quá khứ đã được nạp
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
            EmployeeAvailableDates = employeeAvailableDates,
            Today = today,
            ExistingPastAssignments = pastAssignments
        });

        if (!solverResult.IsSuccess)
        {
            return ApiResponse<AutoScheduleResultDto>.Fail(solverResult.StatusMessage);
        }

        // 6. Lưu các phân bổ tối ưu vào cơ sở dữ liệu (CHỈ LƯU các ca thuộc ngày tương lai WorkDate > today)
        var scheduleMap = schedules.ToDictionary(s => (s.ShiftTemplateId, s.WorkDate), s => s);
        var userMap = employees.ToDictionary(u => u.Id, u => u);

        var newAssignments = new List<ShiftAssignment>();
        foreach (var item in solverResult.RecommendedAssignments.Where(a => a.WorkDate > today))
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
        return await _templateService.GetAllShiftTemplatesAsync(includeInactive: false);
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
        if (request.WorkDate <= today)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail("Không thể phân công ca làm việc cho ngày hôm nay hoặc ngày đã qua.");
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
        ulong trueHomeBranchId = user.OriginalHomeBranchId ?? user.HomeBranchId ?? 0;
        bool isAtHomeBranch = trueHomeBranchId == targetBranchId;

        if (!isAtHomeBranch)
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
                // 1. Kiểm tra xem có lệnh điều chuyển nhưng chưa đến ngày bắt đầu không (StartDate > WorkDate)
                var futureDispatch = await _context.TemporaryDispatches
                    .Include(d => d.DispatchEmployees)
                    .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                           && d.TargetBranchId == targetBranchId 
                                           && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                           && d.StartDate > request.WorkDate);

                if (futureDispatch != null)
                {
                    return ApiResponse<ShiftAssignmentDto>.Fail(
                        $"Nhân viên '{user.FullName}' được điều chuyển từ ngày {futureDispatch.StartDate:dd/MM/yyyy} đến {futureDispatch.EndDate:dd/MM/yyyy}. Chỉ có thể xếp lịch cho nhân sự từ ngày {futureDispatch.StartDate:dd/MM/yyyy} trở đi!");
                }

                // 2. Kiểm tra xem lệnh điều chuyển đã kết thúc/hết hạn chưa (EndDate < WorkDate)
                var expiredDispatch = await _context.TemporaryDispatches
                    .Include(d => d.DispatchEmployees)
                    .FirstOrDefaultAsync(d => (d.UserId == user.Id || d.DispatchEmployees.Any(de => de.UserId == user.Id && de.Status == "APPROVED"))
                                           && d.TargetBranchId == targetBranchId 
                                           && (d.Status == "APPROVED" || d.Status == "PARTIAL") 
                                           && d.EndDate < request.WorkDate);

                if (expiredDispatch != null)
                {
                    return ApiResponse<ShiftAssignmentDto>.Fail(
                        $"Lệnh điều chuyển của nhân viên '{user.FullName}' tại chi nhánh này chỉ có hiệu lực từ ngày {expiredDispatch.StartDate:dd/MM/yyyy} đến {expiredDispatch.EndDate:dd/MM/yyyy}. Không thể xếp lịch vào ngày {request.WorkDate:dd/MM/yyyy} do đã quá hạn điều chuyển!");
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
                    $"Nhân viên '{user.FullName}' đã được điều chuyển sang '{outgoingDispatch.TargetBranch?.Name ?? "cơ sở khác"}' từ ngày {outgoingDispatch.StartDate:dd/MM/yyyy} đến {outgoingDispatch.EndDate:dd/MM/yyyy}. Không thể xếp lịch tại cơ sở gốc trong thời gian này!");
            }
        }

        // Kiểm tra khoảng nghỉ tối thiểu 12 giờ giữa 2 ca của 2 ngày liên tiếp
        var restCheck = await CheckMinRestBetweenShiftsAsync(
            request.WorkDate,
            user.Id,
            null,
            shiftTemplate.StartTime,
            shiftTemplate.EndTime,
            shiftTemplate.IsOvernight,
            isRequester: false);

        if (!restCheck.IsValid)
        {
            return ApiResponse<ShiftAssignmentDto>.Fail(restCheck.ErrorMessage ?? "Vi phạm khoảng nghỉ tối thiểu 12 giờ giữa 2 ca liên tiếp.");
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
                SnapshotShiftName = shiftTemplate.Name,
                SnapshotStartTime = shiftTemplate.StartTime,
                SnapshotEndTime = shiftTemplate.EndTime,
                SnapshotIsOvernight = shiftTemplate.IsOvernight,
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
            ShiftName = schedule.EffectiveShiftName,
            StartTime = schedule.EffectiveStartTime,
            EndTime = schedule.EffectiveEndTime,
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
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == (ulong)storeId && ws.WorkDate >= weekStartDate && ws.WorkDate <= weekEnd)
            .ToListAsync();

        foreach (var schedule in schedules)
        {
            schedule.Status = "PUBLISHED";
            if (!schedule.SnapshotStartTime.HasValue && schedule.ShiftTemplate != null)
            {
                schedule.SnapshotShiftName = schedule.ShiftTemplate.Name;
                schedule.SnapshotStartTime = schedule.ShiftTemplate.StartTime;
                schedule.SnapshotEndTime = schedule.ShiftTemplate.EndTime;
                schedule.SnapshotIsOvernight = schedule.ShiftTemplate.IsOvernight;
            }
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
            .ThenBy(sa => sa.Schedule.SnapshotStartTime ?? sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ShiftAssignmentDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                EmployeeId = (int)sa.UserId,
                EmployeeName = sa.User.FullName,
                EmployeeCode = sa.User.EmployeeCode,
                PositionName = sa.User.Role.RoleName,
                ShiftId = (int)sa.Schedule.ShiftTemplateId,
                ShiftName = sa.Schedule.SnapshotShiftName ?? sa.Schedule.ShiftTemplate.Name,
                StartTime = sa.Schedule.SnapshotStartTime ?? sa.Schedule.ShiftTemplate.StartTime,
                EndTime = sa.Schedule.SnapshotEndTime ?? sa.Schedule.ShiftTemplate.EndTime,
                WorkDate = sa.Schedule.WorkDate,
                StoreId = (int)sa.Schedule.BranchId,
                StoreName = sa.Schedule.Branch.Name,
                Status = sa.Status,
                IsDispatched = sa.User.HomeBranchId != sa.Schedule.BranchId
            })
            .ToListAsync();

        return ApiResponse<List<ShiftAssignmentDto>>.Ok(assignments);
    }

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
            var sStart = sa.Schedule.WorkDate.ToDateTime(sa.Schedule.EffectiveStartTime);
            var sEnd = (sa.Schedule.EffectiveIsOvernight || sa.Schedule.EffectiveEndTime < sa.Schedule.EffectiveStartTime)
                ? sa.Schedule.WorkDate.AddDays(1).ToDateTime(sa.Schedule.EffectiveEndTime)
                : sa.Schedule.WorkDate.ToDateTime(sa.Schedule.EffectiveEndTime);

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
}
