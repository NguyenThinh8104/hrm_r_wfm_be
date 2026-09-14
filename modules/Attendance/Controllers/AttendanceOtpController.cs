using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Shared.Common;
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

        // Tìm ca trực của nhân viên hôm nay để xác định chi nhánh cửa hàng
        var assignment = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.Schedule.WorkDate == today && sa.Status == "CONFIRMED");

        var branch = assignment?.Schedule?.Branch;
        if (branch == null && user.HomeBranchId.HasValue)
        {
            branch = await _context.Branches.FindAsync(user.HomeBranchId.Value);
        }

        if (branch == null)
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail("Bạn không có lịch làm việc hôm nay và chưa được phân công chi nhánh."));
        }

        if (!branch.Latitude.HasValue || !branch.Longitude.HasValue)
        {
            // Mặc định nếu cửa hàng chưa nhập tọa độ GPS -> coi như nằm tại chỗ để không chặn nhân viên thử nghiệm
            branch.Latitude = 21.0333;
            branch.Longitude = 105.7833;
        }

        double branchLat = branch.Latitude.Value;
        double branchLng = branch.Longitude.Value;
        int maxRadius = branch.GeofenceRadiusMeters > 0 ? branch.GeofenceRadiusMeters : 50;

        // Tính khoảng cách từ GPS di động của nhân viên đến cửa hàng (mét)
        double distanceMeters = CalculateDistanceMeters(request.Latitude, request.Longitude, branchLat, branchLng);

        if (distanceMeters > maxRadius)
        {
            return BadRequest(ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                $"Bạn chưa đến phạm vi cửa hàng {branch.Name} (Cách: {Math.Round(distanceMeters, 1)} mét). Bán kính cho phép: {maxRadius}m."));
        }

        var otpType = string.Equals(request.Type, "CHECK_OUT", StringComparison.OrdinalIgnoreCase) ? "CHECK_OUT" : "CHECK_IN";
        var otpCode = await _redisOtpService.GenerateAndSaveOtpAsync(userId, otpType, 60);

        return Ok(ApiResponse<RequestAttendanceOtpResponseDto>.Ok(new RequestAttendanceOtpResponseDto
        {
            OtpCode = otpCode,
            ExpiresInSeconds = 60,
            DistanceMeters = Math.Round(distanceMeters, 1),
            BranchName = branch.Name
        }, $"Đã cấp mã OTP {otpType} 60 giây thành công."));
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
