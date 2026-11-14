using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Dịch vụ cấp phát mã OTP điểm danh di động kèm định vị GPS Geofence cho nhân viên.
/// </summary>
public interface IAttendanceOtpService
{
    /// <summary>
    /// Đệ trình tọa độ GPS từ thiết bị di động, kiểm tra khoảng cách Geofence đến chi nhánh làm việc hôm nay
    /// và sinh mã OTP 60 giây lưu tạm trên Redis phục vụ quét điểm danh tại trạm Kiosk.
    /// </summary>
    /// <param name="userId">Mã định danh ID nhân viên yêu cầu cấp mã</param>
    /// <param name="request">DTO chứa tọa độ Latitude, Longitude và loại OTP (CHECK_IN / CHECK_OUT)</param>
    /// <returns>ApiResponse chứa mã OTP 60s, thời gian hết hạn và khoảng cách thực tế đến cửa hàng</returns>
    Task<ApiResponse<RequestAttendanceOtpResponseDto>> RequestAttendanceOtpAsync(ulong userId, RequestAttendanceOtpDto request);
}
