using System.Security.Claims;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Services;

namespace Modules.Attendance.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceOtpController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IRedisOtpService _redisOtpService;

    public AttendanceOtpController(AppDbContext context, IRedisOtpService redisOtpService)
    {
        _context = context;
        _redisOtpService = redisOtpService;
    }

    /// <summary>
    /// [Nhân viên - Mobile App] Đệ trình GPS di động và lấy mã OTP 60 giây để điểm danh trên iPad Kiosk.
    /// </summary>
    [HttpPost("request-otp")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<RequestAttendanceOtpResponseDto>>> RequestAttendanceOtp([FromBody] RequestAttendanceOtpDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!ulong.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse<RequestAttendanceOtpResponseDto>.Fail("Không tìm thấy thông tin định danh nhân viên."));
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.Status != "ACTIVE")
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail("Tài khoản nhân viên không khả dụng."));
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        // 1. Kiểm tra xem nhân viên có lệnh điều động tạm thời có hiệu lực hôm nay hay không
        var activeDispatch = await _context.TemporaryDispatches
            .Include(td => td.TargetBranch)
            .FirstOrDefaultAsync(td => td.UserId == userId &&
                                       td.Status == "APPROVED" &&
                                       td.StartDate <= today &&
                                       today <= td.EndDate);

        Branch? branch = null;

        if (activeDispatch != null)
        {
            // Nếu có lệnh điều động sang TargetBranch, bắt buộc phải có ca trực được xếp lịch tại TargetBranch hôm nay
            var dispatchedShift = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .FirstOrDefaultAsync(sa => sa.UserId == userId &&
                                           sa.Schedule.WorkDate == today &&
                                           sa.Schedule.BranchId == activeDispatch.TargetBranchId &&
                                           (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED"));

            if (dispatchedShift == null)
            {
                return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                    string.Format(AttendanceMessages.DispatchedButNoShiftToday, activeDispatch.TargetBranch.Name)));
            }

            branch = activeDispatch.TargetBranch;
        }
        else
        {
            // Nếu không có lệnh điều động, tìm ca trực hợp lệ hôm nay của nhân viên
            var regularShift = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .FirstOrDefaultAsync(sa => sa.UserId == userId &&
                                           sa.Schedule.WorkDate == today &&
                                           (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED"));

            if (regularShift == null)
            {
                return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(AttendanceMessages.NoShiftTodayForOtp));
            }

            branch = regularShift.Schedule.Branch;
        }

        if (request.Latitude == 0 || request.Longitude == 0)
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(AttendanceMessages.LocationRequired));
        }

        double? branchLat = branch.Location?.Y ?? branch.Latitude;
        double? branchLng = branch.Location?.X ?? branch.Longitude;

        if (!branchLat.HasValue || !branchLng.HasValue)
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                string.Format(AttendanceMessages.BranchLocationNotConfigured, branch.Name)));
        }

        // Bán kính Geofence tối đa theo cấu hình riêng của cửa hàng mục tiêu (mặc định 200m)
        int maxRadius = branch.GeofenceRadiusMeters > 0 ? branch.GeofenceRadiusMeters : 200;

        // Tính khoảng cách thực tế từ GPS di động của nhân viên đến vị trí cửa hàng mục tiêu (mét)
        double distanceMeters = CalculateDistanceMeters(request.Latitude, request.Longitude, branchLat.Value, branchLng.Value);

        if (distanceMeters > maxRadius)
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                string.Format(AttendanceMessages.LocationOutOfGeofence, branch.Name, Math.Round(distanceMeters, 0), maxRadius)));
        }

        var otpType = string.Equals(request.Type, "CHECK_OUT", StringComparison.OrdinalIgnoreCase) ? "CHECK_OUT" : "CHECK_IN";
        var otpCode = await _redisOtpService.GenerateAndSaveOtpAsync(userId, otpType, 60);

        return Ok(ApiResponse<RequestAttendanceOtpResponseDto>.Ok(new RequestAttendanceOtpResponseDto
        {
            OtpCode = otpCode,
            ExpiresInSeconds = 60,
            DistanceMeters = Math.Round(distanceMeters, 1),
            BranchName = branch.Name
        }, string.Format(AttendanceMessages.OtpGeneratedSuccess, otpType)));
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371000.0; // Earth radius in meters
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }

    private static double ToRadians(double val)
    {
        return (Math.PI / 180.0) * val;
    }
}
