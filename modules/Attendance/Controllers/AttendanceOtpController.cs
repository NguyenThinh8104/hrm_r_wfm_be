using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;

namespace Modules.Attendance.Controllers;

/// <summary>
/// API Controller phục vụ nhân viên yêu cầu cấp phát mã OTP điểm danh di động kèm Geofencing.
/// </summary>
[ApiController]
[Route("api/attendance")]
public class AttendanceOtpController : ControllerBase
{
    private readonly IAttendanceOtpService _attendanceOtpService;

    public AttendanceOtpController(IAttendanceOtpService attendanceOtpService)
    {
        _attendanceOtpService = attendanceOtpService;
    }

    /// <summary>
    /// [Nhân viên - Mobile App] Đệ trình GPS di động và lấy mã OTP 60 giây để điểm danh trên iPad Kiosk.
    /// </summary>
    [HttpPost("request-otp")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<RequestAttendanceOtpResponseDto>>> RequestAttendanceOtp([FromBody] RequestAttendanceOtpDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("EmployeeId")?.Value;

        if (!ulong.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<RequestAttendanceOtpResponseDto>.Fail("Không tìm thấy thông tin định danh nhân viên."));
        }

        var result = await _attendanceOtpService.RequestAttendanceOtpAsync(userId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
