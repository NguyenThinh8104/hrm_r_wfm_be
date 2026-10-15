using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Interface dịch vụ chấm công chuyên dụng tại trạm Kiosk cửa hàng (Validate PIN, CheckIn, CheckOut, Upload ảnh S3).
/// </summary>
public interface IKioskAttendanceService
{
    /// <summary>
    /// Bước 1 quy trình Kiosk: Xác thực mã PIN nhân viên và trả về thông tin ca làm việc hôm nay.
    /// </summary>
    Task<ApiResponse<ValidatePinResponseDto>> ValidatePinAsync(ValidatePinRequestDto request);

    /// <summary>
    /// Lấy danh sách phân công lịch làm việc hôm nay tại trạm Kiosk cửa hàng.
    /// </summary>
    Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date);

    /// <summary>
    /// Tra cứu tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý tại trạm Kiosk.
    /// Bao gồm nhân viên cơ sở gốc (HomeBranchId) và nhân viên đang được điều động hợp lệ đến cửa hàng hôm nay.
    /// </summary>
    Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null);

    /// <summary>
    /// Điểm danh Check-in tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis, so khớp khung giờ ca làm việc.
    /// </summary>
    Task<ApiResponse<AttendanceRecordDto>> CheckInAsync(KioskCheckInDto request);

    /// <summary>
    /// Điểm danh Check-out tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis, cập nhật CheckOutTime.
    /// </summary>
    Task<ApiResponse<AttendanceRecordDto>> CheckOutAsync(KioskCheckOutDto request);

    /// <summary>
    /// Upload ảnh chấm công (bắt buộc) lên S3 và cập nhật attendance record → COMPLETED.
    /// </summary>
    Task<ApiResponse<UploadAttendancePhotoResponseDto>> UploadAttendancePhotoAsync(UploadAttendancePhotoDto request);

    /// <summary>
    /// Hủy lượt điểm danh tại trạm Kiosk khi nhân viên dừng ở bước chụp ảnh hoặc muốn nhường người khác.
    /// </summary>
    Task<ApiResponse<bool>> CancelAttendanceAsync(CancelKioskAttendanceDto request);
}
