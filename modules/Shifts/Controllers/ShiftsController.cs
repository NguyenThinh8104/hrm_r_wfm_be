using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;

namespace Modules.Shifts.Controllers;

/// <summary>
/// API Controller quản lý lịch làm việc, mẫu ca chuẩn, định mức nhu cầu nhân sự và phân bổ ca làm việc (UC 2.1).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    // ==========================================
    // 1. Quản lý Mẫu Ca Chuẩn (Operations Admin)
    // ==========================================

    /// <summary>
    /// [Operations Admin] Tạo mới mẫu ca làm việc chuẩn áp dụng cho toàn hệ thống cửa hàng.
    /// Logic đặc biệt: Yêu cầu quyền OperationsAdmin hoặc BusinessOwner, tự động kiểm tra trùng mã TemplateCode và mã hóa chuỗi chữ hoa.
    /// </summary>
    /// <param name="dto">DTO thông tin mẫu ca mới (TemplateCode, Name, StartTime, EndTime, IsOvernight, BreakDurationMinutes)</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca vừa tạo thành công (ShiftDto)</returns>
    [HttpPost("templates")]
    [Authorize(Roles = "OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<ShiftDto>>> CreateShiftTemplate([FromBody] CreateShiftTemplateDto dto)
    {
        var result = await _shiftService.CreateShiftTemplateAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin ca làm việc chuẩn.
    /// Logic đặc biệt: Yêu cầu quyền OperationsAdmin/BusinessOwner, cập nhật khung giờ, cờ ca đêm và thời gian nghỉ giữa ca.
    /// </summary>
    /// <param name="id">Mã ID mẫu ca chuẩn cần sửa</param>
    /// <param name="dto">DTO chứa dữ liệu mới cần cập nhật</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca sau khi sửa (ShiftDto)</returns>
    [HttpPut("templates/{id}")]
    [Authorize(Roles = "OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<ShiftDto>>> UpdateShiftTemplate(uint id, [FromBody] UpdateShiftTemplateDto dto)
    {
        var result = await _shiftService.UpdateShiftTemplateAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Vô hiệu hóa ca làm việc chuẩn.
    /// Logic đặc biệt: Thực hiện soft delete (IsActive = false) để giữ lại lịch sử phân công và dữ liệu chấm công.
    /// </summary>
    /// <param name="id">Mã ID mẫu ca chuẩn cần vô hiệu hóa</param>
    /// <returns>ApiResponse trả về boolean xác nhận thao tác thành công</returns>
    [HttpDelete("templates/{id}")]
    [Authorize(Roles = "OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteShiftTemplate(uint id)
    {
        var result = await _shiftService.DeleteShiftTemplateAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách mẫu ca chuẩn hệ thống.
    /// Logic đặc biệt: Cho phép truy cập công khai (AllowAnonymous), tùy chọn lấy cả các ca đã ngưng hoạt động (includeInactive = true).
    /// </summary>
    /// <param name="includeInactive">Cờ tùy chọn: True lấy cả ca ngưng hoạt động, False chỉ lấy ca active (Mặc định False)</param>
    /// <returns>ApiResponse chứa danh sách ShiftDto</returns>
    [HttpGet("templates")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ShiftDto>>>> GetShiftTemplates([FromQuery] bool includeInactive = false)
    {
        var result = await _shiftService.GetAllShiftTemplatesAsync(includeInactive);
        return Ok(result);
    }

    // =========================================================
    // 2. Khởi Tạo Khung Lịch & Định Mức Nhu Cầu (Store Manager & Admin)
    // =========================================================

    /// <summary>
    /// [Operations Admin / Store Manager] Tự động sinh khung mẫu lịch làm việc cho tất cả các ngày trong tháng.
    /// Logic đặc biệt: Tự động duyệt từ ngày 1 đến ngày cuối tháng, sinh bản ghi WorkSchedule với các ca active ở trạng thái DRAFT.
    /// </summary>
    /// <param name="dto">DTO cấu hình bao gồm BranchId, Year, Month, TemplateIds tùy chọn và định mức nhân sự mặc định</param>
    /// <returns>ApiResponse chứa danh sách các bản ghi WorkScheduleDto thô trong tháng</returns>
    [HttpPost("schedules/generate-monthly")]
    [Authorize(Roles = "OperationsAdmin,StoreManager,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<WorkScheduleDto>>>> GenerateMonthlySchedule([FromBody] GenerateMonthlyScheduleDto dto)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        ulong.TryParse(empIdClaim, out var userId);

        var result = await _shiftService.GenerateMonthlyScheduleAsync(dto, userId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Điều chỉnh định mức nhu cầu số lượng nhân sự (Thu ngân, Bán hàng, Bảo vệ) cho một ca làm việc.
    /// Logic đặc biệt: Cập nhật các chỉ tiêu RequiredCashier, RequiredSales, RequiredSecurity và tự động đếm số nhân sự đã gán.
    /// </summary>
    /// <param name="scheduleId">Mã ID bản ghi ca trực (WorkSchedule.Id)</param>
    /// <param name="dto">DTO chứa các chỉ tiêu định mức nhu cầu nhân sự mới</param>
    /// <returns>ApiResponse chứa thông tin WorkScheduleDto đã cập nhật định mức</returns>
    [HttpPut("schedules/{scheduleId}/requirements")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<WorkScheduleDto>>> UpdateScheduleRequirement(ulong scheduleId, [FromBody] UpdateScheduleRequirementDto dto)
    {
        dto.ScheduleId = scheduleId;
        var result = await _shiftService.UpdateScheduleRequirementAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách khung lịch và định mức nhu cầu nhân sự của cửa hàng trong tháng.
    /// Logic đặc biệt: Yêu cầu đăng nhập, trả về toàn bộ ca làm việc sắp xếp theo ngày và giờ bắt đầu.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm làm việc (Ví dụ: 2026)</param>
    /// <param name="month">Tháng làm việc (1 - 12)</param>
    /// <returns>ApiResponse chứa danh sách WorkScheduleDto</returns>
    [HttpGet("schedules/monthly")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<WorkScheduleDto>>>> GetMonthlySchedules(
        [FromQuery] ulong branchId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _shiftService.GetMonthlySchedulesAsync(branchId, year, month);
        return Ok(result);
    }

    // =========================================================
    // 3. Phân Bổ Nhân Sự & Công Bố Lịch (Store Manager)
    // =========================================================

    /// <summary>
    /// [Store Manager] Phân bổ hàng loạt nhân viên Full-time/Part-time vào ca trực với kiểm tra chống trùng lịch tự động.
    /// Logic đặc biệt: Tự động phát hiện và ngăn chặn nhân viên bị trùng ca trong cùng 1 ngày, báo lỗi chi tiết các trường hợp không hợp lệ.
    /// </summary>
    /// <param name="dto">DTO chứa BranchId và danh sách mảng các phân công ca trực</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto vừa phân công thành công</returns>
    [HttpPost("assignments/batch")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<ShiftAssignmentDto>>>> BatchAssignShifts([FromBody] BatchAssignShiftDto dto)
    {
        var result = await _shiftService.BatchAssignShiftsAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager Dashboard] Lấy dữ liệu ma trận bảng phân bổ ca làm việc tháng (Nhân viên x Ngày).
    /// Logic đặc biệt: Tổng hợp tất cả nhân viên thuộc chi nhánh và nhân viên biệt phái, trả về lưới phân công ca giúp Quản lý theo dõi trực quan.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm tra cứu ma trận</param>
    /// <param name="month">Tháng tra cứu ma trận (1 - 12)</param>
    /// <returns>ApiResponse chứa đối tượng MonthlyScheduleMatrixDto hiển thị ma trận lịch</returns>
    [HttpGet("schedules/monthly-matrix")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<MonthlyScheduleMatrixDto>>> GetMonthlyRosterMatrix(
        [FromQuery] ulong branchId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var result = await _shiftService.GetMonthlyRosterMatrixAsync(branchId, year, month);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Duyệt và công bố toàn bộ lịch ca làm việc trong tháng cho nhân viên cửa hàng.
    /// Logic đặc biệt: Đổi trạng thái lịch ca từ DRAFT sang PUBLISHED và xác nhận ca trực CONFIRMED cho tất cả nhân viên.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm công bố lịch</param>
    /// <param name="month">Tháng công bố lịch</param>
    /// <returns>ApiResponse trả về boolean kết quả công bố thành công</returns>
    [HttpPost("schedules/publish-monthly")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishMonthlySchedule(
        [FromQuery] ulong branchId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        ulong.TryParse(empIdClaim, out var userId);

        var result = await _shiftService.PublishMonthlyScheduleAsync(branchId, year, month, userId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 4. API Hiện Có Giữ Tương Thích
    // ==========================================

    /// <summary>
    /// Lấy danh sách ca làm việc active cho client.
    /// </summary>
    /// <returns>ApiResponse chứa danh sách ShiftDto</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ShiftDto>>>> GetShifts()
    {
        var result = await _shiftService.GetAllShiftsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách phân công lịch trực theo khoảng thời gian.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="startDate">Ngày bắt đầu (YYYY-MM-DD)</param>
    /// <param name="endDate">Ngày kết thúc (YYYY-MM-DD)</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto</returns>
    [HttpGet("schedule")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ShiftAssignmentDto>>>> GetSchedule(
        [FromQuery] int storeId,
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (!DateOnly.TryParse(startDate, out var sDate) || !DateOnly.TryParse(endDate, out var eDate))
        {
            return BadRequest(ApiResponse<List<ShiftAssignmentDto>>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var result = await _shiftService.GetScheduleAsync(storeId, sDate, eDate);
        return Ok(result);
    }

    /// <summary>
    /// Phân công ca trực đơn lẻ cho nhân viên.
    /// </summary>
    /// <param name="request">DTO gán ca làm việc lẻ</param>
    /// <returns>ApiResponse chứa ShiftAssignmentDto</returns>
    [HttpPost("assign")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> AssignShift([FromBody] CreateShiftAssignmentDto request)
    {
        var result = await _shiftService.AssignShiftAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Công bố phát hành lịch làm việc theo tuần.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="weekStartDate">Ngày bắt đầu tuần (YYYY-MM-DD)</param>
    /// <returns>ApiResponse trả về boolean</returns>
    [HttpPost("publish")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishSchedule(
        [FromQuery] int storeId,
        [FromQuery] string weekStartDate)
    {
        if (!DateOnly.TryParse(weekStartDate, out var sDate))
        {
            return BadRequest(ApiResponse<bool>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        int.TryParse(empIdClaim, out var empId);

        var result = await _shiftService.PublishScheduleAsync(storeId, sDate, empId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách ca trực cá nhân của nhân viên đang đăng nhập.
    /// </summary>
    /// <param name="startDate">Ngày bắt đầu (YYYY-MM-DD)</param>
    /// <param name="endDate">Ngày kết thúc (YYYY-MM-DD)</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto</returns>
    [HttpGet("my-shifts")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ShiftAssignmentDto>>>> GetMyShifts(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<List<ShiftAssignmentDto>>.Fail("Không xác định được danh tính nhân viên."));
        }

        if (!DateOnly.TryParse(startDate, out var sDate) || !DateOnly.TryParse(endDate, out var eDate))
        {
            return BadRequest(ApiResponse<List<ShiftAssignmentDto>>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var result = await _shiftService.GetEmployeeShiftsAsync(empId, sDate, eDate);
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu đổi ca làm việc.
    /// </summary>
    /// <param name="request">DTO tạo yêu cầu đổi ca trực</param>
    /// <returns>ApiResponse chứa ShiftSwapRequestDto</returns>
    [HttpPost("swap-request")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ShiftSwapRequestDto>>> CreateSwapRequest([FromBody] CreateSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<ShiftSwapRequestDto>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _shiftService.RequestShiftSwapAsync(empId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Quản lý duyệt hoặc từ chối yêu cầu đổi ca trực.
    /// </summary>
    /// <param name="request">DTO kết quả duyệt đổi ca</param>
    /// <returns>ApiResponse trả về boolean</returns>
    [HttpPost("swap-review")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<bool>>> ReviewSwapRequest([FromBody] ReviewSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        int.TryParse(empIdClaim, out var managerEmpId);

        var result = await _shiftService.ReviewShiftSwapAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi ca trực của cửa hàng.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách ShiftSwapRequestDto</returns>
    [HttpGet("swap-requests/{storeId}")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<ShiftSwapRequestDto>>>> GetSwapRequests(int storeId)
    {
        var result = await _shiftService.GetSwapRequestsByStoreAsync(storeId);
        return Ok(result);
    }
}
