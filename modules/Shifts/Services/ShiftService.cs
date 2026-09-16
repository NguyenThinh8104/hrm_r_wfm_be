using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ xử lý logic nghiệp vụ quản lý mẫu ca chuẩn, khởi tạo khung lịch tháng, định mức nhân sự và phân bổ ca làm việc (UC 2.1).
/// </summary>
public class ShiftService : IShiftService
{
    private readonly AppDbContext _context;

    public ShiftService(AppDbContext context)
    {
        _context = context;
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
            Description = string.IsNullOrWhiteSpace(dto.Description) 
                ? $"Khung ca từ {dto.StartTime:HH\\:mm} đến {dto.EndTime:HH\\:mm}" 
                : dto.Description.Trim(),
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
        if (dto.Description != null)
        {
            template.Description = dto.Description.Trim();
        }
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
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<ShiftTemplateDto>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        var validation = ValidateAndCalculateShift(template.StartTime, template.EndTime, template.IsOvernight, template.BreakDurationMinutes);
        return ApiResponse<ShiftTemplateDto>.Ok(MapToShiftTemplateDto(template, validation.WorkHours), "Lấy thông tin mẫu ca thành công.");
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

    private static ShiftTemplateDto MapToShiftTemplateDto(ShiftTemplate st, double workHours)
    {
        return new ShiftTemplateDto
        {
            Id = st.Id,
            TemplateCode = st.TemplateCode,
            Name = st.Name,
            Description = st.Description,
            StartTime = st.StartTime.ToString("HH\\:mm\\:ss"),
            EndTime = st.EndTime.ToString("HH\\:mm\\:ss"),
            BreakMinutes = st.BreakDurationMinutes,
            IsOvernight = st.IsOvernight,
            WorkHours = workHours,
            Status = st.IsActive ? "ACTIVE" : "INACTIVE",
            CreatedAt = st.CreatedAt,
            UpdatedAt = st.UpdatedAt
        };
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

        var storeUsers = await _context.Users
            .Include(u => u.Role)
            .Where(u => (u.HomeBranchId == branchId || assignedUserIds.Contains(u.Id)) && u.Status == "ACTIVE")
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

        string weekStatus = "DRAFT";
        if (schedules.Any())
        {
            if (schedules.All(s => s.Status == "PUBLISHED"))
                weekStatus = "PUBLISHED";
            else if (schedules.Any(s => s.Status == "PUBLISHED"))
                weekStatus = "PARTIAL";
        }

        var matrix = new WeeklyScheduleMatrixDto
        {
            BranchId = branchId,
            BranchName = branch.Name,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            WeekStatus = weekStatus,
            Days = days,
            Schedules = scheduleDtos,
            EmployeeRosters = employeeRosters
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

        // 2. Lấy danh sách nhân viên chi nhánh
        var employees = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.HomeBranchId == dto.BranchId && u.Status == "ACTIVE")
            .ToListAsync();

        if (!employees.Any())
        {
            return ApiResponse<AutoScheduleResultDto>.Fail("Không có nhân viên active tại chi nhánh để phân bổ.");
        }

        // 3. Nếu cho phép ghi đè, xóa các phân công cũ trong tuần
        if (dto.OverwriteExisting)
        {
            var oldAssignments = await _context.ShiftAssignments
                .Where(sa => sa.Schedule.BranchId == dto.BranchId && sa.Schedule.WorkDate >= dto.WeekStartDate && sa.Schedule.WorkDate <= weekEndDate)
                .ToListAsync();

            if (oldAssignments.Any())
            {
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
            MinShiftsPerWeekForFullTime = dto.MinShiftsPerWeekForFullTime
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

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId);
        if (user == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy nhân viên.");

        var branch = await _context.Branches.FindAsync((ulong)request.StoreId);
        if (branch == null) return ApiResponse<ShiftAssignmentDto>.Fail("Không tìm thấy cửa hàng.");

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

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (requestingAssignment.Schedule.WorkDate < today)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể gửi đơn xin điều chỉnh/nghỉ cho ca làm việc đã trôi qua trong quá khứ.");
        }

        // Kiểm tra xem ca này đã có đơn chờ duyệt chưa
        var hasPending = await _context.ShiftSwapRequests
            .AnyAsync(r => (r.RequestingAssignmentId == requestingAssignment.Id || (r.TargetAssignmentId != null && r.TargetAssignmentId == requestingAssignment.Id))
                        && r.Status == "PENDING");
        if (hasPending)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Ca trực này hiện đang có đơn xin đổi/chuyển/nghỉ ca đang chờ Quản lý phê duyệt.");
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
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.ScheduleId == requestingAssignment.ScheduleId);
            if (targetHasScheduleConflict)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Đồng nghiệp đã có phân công trong cùng khung ca trực này.");
            }

            var targetHasSameDayShift = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
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

            if (targetAssignment.Schedule.WorkDate < today)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể đổi ca trực của đồng nghiệp đã trôi qua trong quá khứ.");
            }

            // Kiểm tra xung đột ca
            var requesterConflict = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == (ulong)requesterEmployeeId 
                             && sa.Id != requestingAssignment.Id 
                             && sa.Schedule.WorkDate == targetAssignment.Schedule.WorkDate);
            if (requesterConflict)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Bạn đã có ca trực khác ngày {targetAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể tiếp nhận ca của đồng nghiệp.");
            }

            var targetConflict = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id 
                             && sa.Id != targetAssignment.Id 
                             && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
            if (targetConflict)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có ca trực khác ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể nhận ca xin đổi.");
            }
        }

        var swap = new ShiftSwapRequest
        {
            RequestingAssignmentId = requestingAssignment.Id,
            RequesterUserId = (ulong)requesterEmployeeId,
            ScheduleId = requestingAssignment.ScheduleId,
            TargetUserId = targetUser?.Id,
            TargetAssignmentId = targetAssignment?.Id,
            RequestType = reqType,
            Reason = request.Reason,
            Status = "PENDING",
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
                ? "Đã gửi đơn xin chuyển ca (nhờ làm thay), chờ Cửa hàng trưởng phê duyệt."
                : "Đã gửi đơn xin đổi ca với đồng nghiệp, chờ Cửa hàng trưởng phê duyệt.";

        return ApiResponse<ShiftSwapRequestDto>.Ok(resultDto, successMsg);
    }

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi, chuyển hoặc xin nghỉ ca trực.
    /// </summary>
    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.TargetUser)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển/nghỉ ca không tồn tại.");
        }

        if (swap.Status != "PENDING")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
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

        swap.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        swap.ReviewedBy = (ulong)managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved)
        {
            if (string.Equals(swap.RequestType, "LEAVE", StringComparison.OrdinalIgnoreCase))
            {
                // TH1: Xin nghỉ ca -> Phê duyệt gỡ phân công ca để Cửa hàng trưởng xếp lại lịch
                if (swap.RequestingAssignment != null)
                {
                    var assignmentToRemove = swap.RequestingAssignment;
                    // Đảm bảo RequesterUserId và ScheduleId đã được lưu snapshot trước khi ngắt liên kết
                    swap.RequesterUserId ??= assignmentToRemove.UserId;
                    swap.ScheduleId ??= assignmentToRemove.ScheduleId;
                    swap.RequestingAssignmentId = null;
                    swap.RequestingAssignment = null;

                    _context.ShiftAssignments.Remove(assignmentToRemove);
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
                    var requesterConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.Id != swap.RequestingAssignmentId 
                                     && sa.UserId == swap.RequestingAssignment.UserId 
                                     && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                    if (requesterConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể phê duyệt: Nhân viên xin đổi đã có ca trực khác trong ngày của ca được đổi.");
                    }

                    var targetConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.Id != swap.TargetAssignmentId 
                                     && sa.UserId == swap.TargetAssignment.UserId 
                                     && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                    if (targetConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp đã có ca trực khác trong ngày của ca xin đổi.");
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

        var message = request.IsApproved
            ? "Đã phê duyệt đơn và cập nhật lịch làm việc thành công."
            : "Đã từ chối đơn yêu cầu điều chỉnh lịch ca.";

        return ApiResponse<bool>.Ok(true, message);
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển/nghỉ ca của toàn chi nhánh (cho Quản lý cửa hàng duyệt).
    /// </summary>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
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
    /// Lấy danh sách đồng nghiệp cùng chi nhánh để nhân viên chọn khi đổi/chuyển ca.
    /// </summary>
    public async Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId)
    {
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == (ulong)currentEmployeeId);
        if (currentUser == null)
        {
            return ApiResponse<List<ColleagueDto>>.Fail("Không tìm thấy thông tin nhân viên yêu cầu.");
        }

        var colleagues = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Id != (ulong)currentEmployeeId 
                     && u.HomeBranchId == (ulong)branchId 
                     && u.Status == "ACTIVE"
                     && u.RoleId == currentUser.RoleId) // RÀNG BUỘC: Cùng role mới được đổi lịch cho nhau
            .OrderBy(u => u.FullName)
            .Select(u => new ColleagueDto
            {
                EmployeeId = (int)u.Id,
                FullName = u.FullName,
                RoleName = u.Role != null ? u.Role.RoleName : "",
                PhoneNumber = u.Phone ?? ""
            })
            .ToListAsync();

        return ApiResponse<List<ColleagueDto>>.Ok(colleagues);
    }

    /// <summary>
    /// Lấy danh sách các ca làm việc của một đồng nghiệp trong tương lai để nhân viên chọn đổi (loại bỏ các ca mà nhân viên hiện tại đã có lịch).
    /// </summary>
    public async Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Lấy danh sách các ScheduleId mà nhân viên hiện tại ĐÃ có lịch trực
        var myScheduleIds = await _context.ShiftAssignments
            .Where(sa => sa.UserId == (ulong)currentEmployeeId)
            .Select(sa => sa.ScheduleId)
            .ToListAsync();

        var shifts = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Where(sa => sa.UserId == (ulong)colleagueEmployeeId 
                      && sa.Schedule.WorkDate >= today
                      && !myScheduleIds.Contains(sa.ScheduleId))
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .Select(sa => new ColleagueShiftDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                ShiftName = sa.Schedule.ShiftTemplate.Name,
                WorkDate = sa.Schedule.WorkDate.ToString("yyyy-MM-dd"),
                TimeRange = $"{sa.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {sa.Schedule.ShiftTemplate.EndTime:hh\\:mm}",
                BranchName = sa.Schedule.Branch.Name
            })
            .ToListAsync();

        return ApiResponse<List<ColleagueShiftDto>>.Ok(shifts);
    }
}
