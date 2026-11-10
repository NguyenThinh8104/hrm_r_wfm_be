using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Data;
using Shared.Services;

namespace Modules.Attendance.Services;

/// <summary>
/// Triển khai dịch vụ xác thực vị trí GPS Geofence và cấp phát mã OTP 60 giây cho điểm danh Kiosk.
/// </summary>
public class AttendanceOtpService : IAttendanceOtpService
{
    private readonly AppDbContext _context;
    private readonly IRedisOtpService _redisOtpService;

    public AttendanceOtpService(AppDbContext context, IRedisOtpService redisOtpService)
    {
        _context = context;
        _redisOtpService = redisOtpService;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RequestAttendanceOtpResponseDto>> RequestAttendanceOtpAsync(ulong userId, RequestAttendanceOtpDto request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.Status != "ACTIVE")
        {
            return ApiResponse<RequestAttendanceOtpResponseDto>.Fail("Tài khoản nhân viên không khả dụng.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);

        // 1. Kiểm tra xem nhân viên có lệnh điều động tạm thời có hiệu lực hôm nay hoặc ca đêm hôm qua hay không
        var activeDispatch = await _context.TemporaryDispatches
            .Include(td => td.TargetBranch)
            .Include(td => td.DispatchEmployees)
            .FirstOrDefaultAsync(td => (td.UserId == userId || td.DispatchEmployees.Any(de => de.UserId == userId && de.Status == "APPROVED")) &&
                                       (td.Status == "APPROVED" || td.Status == "PARTIAL") &&
                                       td.StartDate <= today &&
                                       yesterday <= td.EndDate);

        Branch? branch = null;

        if (activeDispatch != null)
        {
            // Nếu có lệnh điều động sang TargetBranch, tìm ca trực hợp lệ hôm nay hoặc ca đêm bắt đầu từ hôm qua
            var dispatchedShifts = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .Include(sa => sa.AttendanceLog)
                .Where(sa => sa.UserId == userId &&
                             sa.Schedule.BranchId == activeDispatch.TargetBranchId &&
                             sa.Schedule.Status == "PUBLISHED" &&
                             (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED") &&
                             (sa.Schedule.WorkDate == today || sa.Schedule.WorkDate == yesterday))
                .OrderBy(sa => sa.Schedule.WorkDate)
                .ToListAsync();

            // Ưu tiên chọn ca đêm hôm qua nếu chưa hoàn tất check-out
            var dispatchedShift = dispatchedShifts.FirstOrDefault(sa =>
            {
                if (sa.Schedule.WorkDate == yesterday)
                {
                    var startTime = sa.Schedule.EffectiveStartTime;
                    var endTime = sa.Schedule.EffectiveEndTime;
                    var isOvernight = sa.Schedule.EffectiveIsOvernight
                                   || (sa.Schedule.ShiftTemplate != null && sa.Schedule.ShiftTemplate.IsOvernight)
                                   || sa.Schedule.SnapshotIsOvernight == true
                                   || endTime < startTime;

                    return isOvernight && (sa.AttendanceLog == null || !sa.AttendanceLog.CheckOutTime.HasValue);
                }
                return false;
            }) ?? dispatchedShifts.FirstOrDefault(sa => sa.Schedule.WorkDate == today);

            if (dispatchedShift == null)
            {
                return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                    string.Format(AttendanceMessages.DispatchedButNoShiftToday, activeDispatch.TargetBranch.Name));
            }

            branch = activeDispatch.TargetBranch;
        }
        else
        {
            // Nếu không có lệnh điều động, tìm ca trực hợp lệ hôm nay hoặc ca đêm bắt đầu từ hôm qua của nhân viên
            var regularShifts = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .Include(sa => sa.AttendanceLog)
                .Where(sa => sa.UserId == userId &&
                             sa.Schedule.Status == "PUBLISHED" &&
                             (sa.Status == "PUBLISHED" || sa.Status == "CONFIRMED") &&
                             (sa.Schedule.WorkDate == today || sa.Schedule.WorkDate == yesterday))
                .OrderBy(sa => sa.Schedule.WorkDate)
                .ToListAsync();

            // Ưu tiên chọn ca đêm hôm qua nếu chưa hoàn tất check-out
            var regularShift = regularShifts.FirstOrDefault(sa =>
            {
                if (sa.Schedule.WorkDate == yesterday)
                {
                    var startTime = sa.Schedule.EffectiveStartTime;
                    var endTime = sa.Schedule.EffectiveEndTime;
                    var isOvernight = sa.Schedule.EffectiveIsOvernight
                                   || (sa.Schedule.ShiftTemplate != null && sa.Schedule.ShiftTemplate.IsOvernight)
                                   || sa.Schedule.SnapshotIsOvernight == true
                                   || endTime < startTime;

                    return isOvernight && (sa.AttendanceLog == null || !sa.AttendanceLog.CheckOutTime.HasValue);
                }
                return false;
            }) ?? regularShifts.FirstOrDefault(sa => sa.Schedule.WorkDate == today);

            if (regularShift == null)
            {
                return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(AttendanceMessages.NoShiftTodayForOtp);
            }

            branch = regularShift.Schedule.Branch;
        }

        if (branch == null || !string.Equals(branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                $"Chi nhánh '{branch?.Name ?? "hiện tại"}' đã bị khóa. Không thể thực hiện chấm công.",
                new List<string> { "BRANCH_LOCKED" });
        }

        if (request.Latitude == 0 || request.Longitude == 0)
        {
            return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(AttendanceMessages.LocationRequired);
        }

        double? branchLat = branch.Location?.Y ?? branch.Latitude;
        double? branchLng = branch.Location?.X ?? branch.Longitude;

        if (!branchLat.HasValue || !branchLng.HasValue)
        {
            return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                string.Format(AttendanceMessages.BranchLocationNotConfigured, branch.Name));
        }

        // Bán kính Geofence tối đa theo cấu hình riêng của cửa hàng mục tiêu (mặc định 200m)
        int maxRadius = branch.GeofenceRadiusMeters > 0 ? branch.GeofenceRadiusMeters : 200;

        // Tính khoảng cách thực tế từ GPS di động của nhân viên đến vị trí cửa hàng mục tiêu (mét)
        double distanceMeters = CalculateDistanceMeters(request.Latitude, request.Longitude, branchLat.Value, branchLng.Value);

        if (distanceMeters > maxRadius)
        {
            return ApiResponse<RequestAttendanceOtpResponseDto>.Fail(
                string.Format(AttendanceMessages.LocationOutOfGeofence, branch.Name, Math.Round(distanceMeters, 0), maxRadius));
        }

        var otpType = string.Equals(request.Type, "CHECK_OUT", StringComparison.OrdinalIgnoreCase) ? "CHECK_OUT" : "CHECK_IN";
        var otpCode = await _redisOtpService.GenerateAttendanceOtpAsync(userId, otpType, 60);

        return ApiResponse<RequestAttendanceOtpResponseDto>.Ok(new RequestAttendanceOtpResponseDto
        {
            OtpCode = otpCode,
            ExpiresInSeconds = 60,
            DistanceMeters = Math.Round(distanceMeters, 1),
            BranchName = branch.Name
        }, string.Format(AttendanceMessages.OtpGeneratedSuccess, otpType));
    }

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371000.0; // Bán kính trái đất tính bằng mét
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
