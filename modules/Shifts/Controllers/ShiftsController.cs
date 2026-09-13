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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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
    /// </summary>
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ShiftDto>>>> GetShifts()
    {
        var result = await _shiftService.GetAllShiftsAsync();
        return Ok(result);
    }

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

    [HttpPost("assign")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> AssignShift([FromBody] CreateShiftAssignmentDto request)
    {
        var result = await _shiftService.AssignShiftAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

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

    [HttpGet("swap-requests/{storeId}")]
    [Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
    public async Task<ActionResult<ApiResponse<List<ShiftSwapRequestDto>>>> GetSwapRequests(int storeId)
    {
        var result = await _shiftService.GetSwapRequestsByStoreAsync(storeId);
        return Ok(result);
    }
}
