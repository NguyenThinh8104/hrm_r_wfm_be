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
    // 1. Quản lý Mẫu Ca Chuẩn (Operations Admin)
    // ==========================================

    /// <summary>
    /// Tạo mẫu ca làm việc chuẩn mới áp dụng cho hệ thống chuỗi cửa hàng.
    /// Logic đặc biệt: Kiểm tra trùng duy nhất mã ca TemplateCode (viết hoa), hỗ trợ ca qua đêm (IsOvernight = true) và thời gian nghỉ break.
    /// </summary>
    /// <param name="dto">DTO chứa dữ liệu đầu vào bao gồm TemplateCode, Name, StartTime, EndTime, IsOvernight, BreakDurationMinutes</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca vừa tạo thành công (ShiftDto) hoặc thông báo lỗi nếu trùng mã</returns>
    public async Task<ApiResponse<ShiftDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TemplateCode) || string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<ShiftDto>.Fail("Mã mẫu ca và Tên ca làm việc không được để trống.");
        }

        var codeExists = await _context.ShiftTemplates
            .AnyAsync(st => st.TemplateCode.ToLower() == dto.TemplateCode.Trim().ToLower());

        if (codeExists)
        {
            return ApiResponse<ShiftDto>.Fail($"Mã mẫu ca '{dto.TemplateCode}' đã tồn tại trong hệ thống.");
        }

        var template = new ShiftTemplate
        {
            TemplateCode = dto.TemplateCode.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            IsOvernight = dto.IsOvernight,
            BreakDurationMinutes = dto.BreakDurationMinutes,
            IsActive = true
        };

        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftDto>.Ok(new ShiftDto
        {
            ShiftId = (int)template.Id,
            ShiftCode = template.TemplateCode,
            ShiftName = template.Name,
            StartTime = template.StartTime,
            EndTime = template.EndTime,
            IsOvernight = template.IsOvernight,
            BreakDurationMinutes = template.BreakDurationMinutes,
            IsActive = template.IsActive
        }, "Tạo mẫu ca chuẩn thành công.");
    }

    /// <summary>
    /// Cập nhật thông tin chi tiết của một mẫu ca làm việc chuẩn.
    /// Logic đặc biệt: Cho phép cập nhật giờ bắt đầu, giờ kết thúc, cờ qua đêm, số phút nghỉ giữa ca và bật/tắt trạng thái hoạt động IsActive.
    /// </summary>
    /// <param name="id">Mã ID định danh mẫu ca chuẩn (ShiftTemplate.Id)</param>
    /// <param name="dto">DTO chứa thông tin mới cần cập nhật</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca sau khi cập nhật (ShiftDto) hoặc báo lỗi nếu không tìm thấy ID</returns>
    public async Task<ApiResponse<ShiftDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto)
    {
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<ShiftDto>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        template.Name = dto.Name.Trim();
        template.StartTime = dto.StartTime;
        template.EndTime = dto.EndTime;
        template.IsOvernight = dto.IsOvernight;
        template.BreakDurationMinutes = dto.BreakDurationMinutes;
        template.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return ApiResponse<ShiftDto>.Ok(new ShiftDto
        {
            ShiftId = (int)template.Id,
            ShiftCode = template.TemplateCode,
            ShiftName = template.Name,
            StartTime = template.StartTime,
            EndTime = template.EndTime,
            IsOvernight = template.IsOvernight,
            BreakDurationMinutes = template.BreakDurationMinutes,
            IsActive = template.IsActive
        }, "Cập nhật mẫu ca thành công.");
    }

    /// <summary>
    /// Vô hiệu hóa (Soft delete) mẫu ca làm việc chuẩn.
    /// Logic đặc biệt: Đổi cờ IsActive = false thay vì xóa cứng dữ liệu để bảo toàn lịch sử chấm công và ca trực đã xếp.
    /// </summary>
    /// <param name="id">Mã ID định danh mẫu ca chuẩn cần vô hiệu hóa</param>
    /// <returns>ApiResponse trả về cờ boolean xác nhận thao tác vô hiệu hóa thành công</returns>
    public async Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id)
    {
        var template = await _context.ShiftTemplates.FindAsync(id);
        if (template == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy mẫu ca làm việc.");
        }

        template.IsActive = false;
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã vô hiệu hóa mẫu ca chuẩn.");
    }

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc mẫu trong hệ thống.
    /// Logic đặc biệt: Mặc định chỉ lấy các ca active. Nếu includeInactive = true sẽ lấy tất cả ca phục vụ màn hình quản trị Admin.
    /// </summary>
    /// <param name="includeInactive">Cờ tùy chọn: True lấy cả ca đã vô hiệu hóa, False chỉ lấy ca đang hoạt động</param>
    /// <returns>ApiResponse chứa danh sách DTO thông tin mẫu ca chuẩn (List&lt;ShiftDto&gt;)</returns>
    public async Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(bool includeInactive = false)
    {
        var query = _context.ShiftTemplates.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(st => st.IsActive);
        }

        var shifts = await query
            .OrderBy(st => st.StartTime)
            .Select(st => new ShiftDto
            {
                ShiftId = (int)st.Id,
                ShiftCode = st.TemplateCode,
                ShiftName = st.Name,
                StartTime = st.StartTime,
                EndTime = st.EndTime,
                IsOvernight = st.IsOvernight,
                BreakDurationMinutes = st.BreakDurationMinutes,
                IsActive = st.IsActive
            })
            .ToListAsync();

        return ApiResponse<List<ShiftDto>>.Ok(shifts);
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

        schedule.RequiredCashier = dto.RequiredCashier;
        schedule.RequiredSales = dto.RequiredSales;
        schedule.RequiredSecurity = dto.RequiredSecurity;

        await _context.SaveChangesAsync();

        int cashierCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "CASHIER");
        int salesCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "SALES");
        int securityCount = schedule.ShiftAssignments.Count(sa => sa.AssignedRole.RoleCode == "SECURITY");

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
            RequiredCashier = schedule.RequiredCashier,
            RequiredSales = schedule.RequiredSales,
            RequiredSecurity = schedule.RequiredSecurity,
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

        foreach (var item in dto.Assignments)
        {
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
    /// Gửi yêu cầu đổi ca trực giữa 2 nhân viên.
    /// </summary>
    /// <param name="requesterEmployeeId">ID nhân viên xin đổi ca</param>
    /// <param name="request">DTO chứa ca cần đổi và người muốn đổi cùng lý do</param>
    /// <returns>ApiResponse chứa ShiftSwapRequestDto</returns>
    public async Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
    {
        var requestingAssignment = await _context.ShiftAssignments
            .Include(sa => sa.User)
            .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.AssignmentId && sa.UserId == (ulong)requesterEmployeeId);

        if (requestingAssignment == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của bạn để yêu cầu đổi.");
        }

        var targetUser = await _context.Users.FindAsync((ulong)request.TargetEmployeeId);
        if (targetUser == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy nhân viên được yêu cầu đổi ca.");
        }

        var targetAssignment = await _context.ShiftAssignments
            .FirstOrDefaultAsync(sa => sa.UserId == (ulong)request.TargetEmployeeId && sa.ScheduleId == requestingAssignment.ScheduleId);

        if (targetAssignment == null)
        {
            targetAssignment = requestingAssignment;
        }

        var swap = new ShiftSwapRequest
        {
            RequestingAssignmentId = requestingAssignment.Id,
            TargetAssignmentId = targetAssignment.Id,
            Reason = request.Reason,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        return ApiResponse<ShiftSwapRequestDto>.Ok(new ShiftSwapRequestDto
        {
            SwapRequestId = (int)swap.Id,
            AssignmentId = (int)swap.RequestingAssignmentId,
            RequesterEmployeeId = requesterEmployeeId,
            RequesterName = requestingAssignment.User.FullName,
            TargetEmployeeId = (int)targetUser.Id,
            TargetName = targetUser.FullName,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        }, "Đã gửi yêu cầu đổi ca, chờ Quản lý cửa hàng phê duyệt.");
    }

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi ca trực.
    /// </summary>
    /// <param name="managerEmployeeId">ID quản lý duyệt</param>
    /// <param name="request">DTO chứa ID yêu cầu đổi ca và kết quả duyệt (Approved/Rejected)</param>
    /// <returns>ApiResponse trả về boolean kết quả</returns>
    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
            .Include(s => s.TargetAssignment)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca không tồn tại.");
        }

        if (swap.Status != "PENDING")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        swap.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        swap.ReviewedBy = (ulong)managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved && swap.RequestingAssignment != null && swap.TargetAssignment != null)
        {
            var tempUser = swap.RequestingAssignment.UserId;
            swap.RequestingAssignment.UserId = swap.TargetAssignment.UserId;
            swap.TargetAssignment.UserId = tempUser;
        }

        await _context.SaveChangesAsync();

        var message = request.IsApproved ? "Đã duyệt yêu cầu đổi ca." : "Đã từ chối yêu cầu đổi ca.";
        return ApiResponse<bool>.Ok(true, message);
    }

    /// <summary>
    /// Lấy danh sách danh mục các yêu cầu đổi ca trong cửa hàng.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách ShiftSwapRequestDto</returns>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.User)
            .Where(s => s.RequestingAssignment.Schedule.BranchId == (ulong)storeId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                AssignmentId = (int)s.RequestingAssignmentId,
                RequesterEmployeeId = (int)s.RequestingAssignment.UserId,
                RequesterName = s.RequestingAssignment.User.FullName,
                TargetEmployeeId = (int)s.TargetAssignment.UserId,
                TargetName = s.TargetAssignment.User.FullName,
                Reason = s.Reason,
                Status = s.Status,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }
}
