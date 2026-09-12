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
    [Authorize(Roles = "ShiftLeader,StoreManager")]
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
    /// [ShiftLeader/Manager] Lấy lịch sử danh sách bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="date">Ngày tra cứu lịch sử (định dạng YYYY-MM-DD, mặc định là hôm nay)</param>
    /// <returns>Danh sách các bản ghi điểm danh chi tiết trong ngày</returns>
    [HttpGet("history")]
    [Authorize(Roles = "ShiftLeader,StoreManager,OperationsAdmin,BusinessOwner")]
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
}
