using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;

namespace Modules.Attendance.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpPost("validate-pin")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ValidatePinResponseDto>>> ValidatePin([FromBody] ValidatePinRequestDto request)
    {
        var result = await _attendanceService.ValidatePinAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("kiosk-roster")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<KioskEmployeeRosterDto>>>> GetKioskRoster(
        [FromQuery] int storeId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var result = await _attendanceService.GetKioskRosterAsync(storeId, targetDate);
        return Ok(result);
    }

    [HttpPost("kiosk-checkin")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckIn([FromBody] KioskPinCheckInDto request)
    {
        var result = await _attendanceService.KioskCheckInAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("kiosk-checkout")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckOut([FromBody] KioskPinCheckOutDto request)
    {
        var result = await _attendanceService.KioskCheckOutAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("report-fraud")]
    [Authorize(Roles = "ShiftLeader,StoreManager,OperationsAdmin,BusinessOwner")]
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

