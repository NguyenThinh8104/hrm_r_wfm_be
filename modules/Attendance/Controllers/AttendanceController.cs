using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;

namespace Modules.Attendance.Controllers;

/// <summary>
/// API Controller quản lý điểm danh, nhật ký và báo cáo gian lận dành cho Web Quản trị.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// [ShiftLeader/Manager] Trưởng ca hoặc Quản lý báo cáo hành vi gian lận / vắng mặt điểm danh.
    /// </summary>
    /// <param name="request">DTO chứa AttendanceId và lý do báo cáo gian lận</param>
    /// <returns>Xác nhận tạo báo cáo ngoại lệ điểm danh</returns>

    [HttpPost("report-fraud")]
    [Authorize(Roles = "SHIFT_LEADER,STORE_MANAGER")]
    public async Task<ActionResult<ApiResponse<bool>>> ReportFraud([FromBody] ReportAttendanceFraudDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var leaderEmpId))
        {
            return Unauthorized(ApiResponse<bool>.Fail(AttendanceMessages.LeaderIdentityNotFound));
        }

        var result = await _attendanceService.ReportFraudAsync(leaderEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }


    /// <summary>
    /// [ShiftLeader/Manager/OpsAdmin] Theo dõi quân số ca trực thời gian thực kèm Presigned Temp URL xem ảnh Kiosk S3.
    /// </summary>
    [HttpGet("live-roster")]
    [Authorize(Roles = "SHIFT_LEADER,STORE_MANAGER,OPERATIONS_ADMIN,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<LiveRosterDto>>>> GetLiveRoster(
        [FromQuery] ulong storeId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var result = await _attendanceService.GetLiveRosterAsync(storeId, targetDate);
        return Ok(result);
    }

    /// <summary>
    /// [StoreManager/OpsAdmin] Phân xử khiếu nại cờ vi phạm (Khôi phục giờ công hoặc Bác bỏ).
    /// </summary>
    [HttpPost("resolve-fraud")]
    [Authorize(Roles = "STORE_MANAGER,OPERATIONS_ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveFraud([FromBody] ResolveFraudDto request)
    {
        var result = await _attendanceService.ResolveFraudAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [ShiftLeader/Manager] Lấy lịch sử danh sách bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="date">Ngày tra cứu lịch sử (định dạng YYYY-MM-DD, mặc định là hôm nay)</param>
    /// <returns>Danh sách các bản ghi điểm danh chi tiết trong ngày</returns>
    [HttpGet("history")]
    [Authorize(Roles = "SHIFT_LEADER,STORE_MANAGER,OPERATIONS_ADMIN,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<List<AttendanceRecordDto>>>> GetAttendanceHistory(
        [FromQuery] int storeId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var result = await _attendanceService.GetAttendanceHistoryAsync(storeId, targetDate);
        return Ok(result);
    }

    /// <summary>
    /// [Employee] Lấy lịch làm việc cá nhân theo tuần (Calendar View).
    /// </summary>
    /// <param name="weekStart">Ngày bắt đầu tuần (định dạng YYYY-MM-DD)</param>
    [HttpGet("my-weekly-schedule")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MyWeeklyScheduleDto>>> GetMyWeeklySchedule([FromQuery] string? weekStart)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(ApiResponse<MyWeeklyScheduleDto>.Fail("Không xác định được danh tính người dùng."));

        var startDate = string.IsNullOrEmpty(weekStart) || !DateOnly.TryParse(weekStart, out var parsedDate)
            ? GetMondayOfWeek(DateOnly.FromDateTime(DateTime.Now))
            : parsedDate;

        var result = await _attendanceService.GetMyWeeklyScheduleAsync(userId.Value, startDate);
        return Ok(result);
    }

    /// <summary>
    /// [Employee] Lấy lịch sử chấm công cá nhân theo tháng.
    /// </summary>
    /// <param name="month">Tháng (1-12, mặc định là tháng hiện tại)</param>
    /// <param name="year">Năm (mặc định là năm hiện tại)</param>
    [HttpGet("my-attendance-history")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MyAttendanceHistoryDto>>> GetMyAttendanceHistory(
        [FromQuery] int? month,
        [FromQuery] int? year)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(ApiResponse<MyAttendanceHistoryDto>.Fail("Không xác định được danh tính người dùng."));

        var now = DateTime.Now;
        int targetMonth = month is >= 1 and <= 12 ? month.Value : now.Month;
        int targetYear = year is > 2000 ? year.Value : now.Year;

        var result = await _attendanceService.GetMyAttendanceHistoryAsync(userId.Value, targetMonth, targetYear);
        return Ok(result);
    }

    private ulong? GetCurrentUserId()
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return ulong.TryParse(empIdClaim, out var userId) ? userId : null;
    }

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff);
    }
}

