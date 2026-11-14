using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Interface dịch vụ chấm công toàn diện (Facade chuyển tiếp tới các service chuyên biệt).
/// </summary>
public interface IAttendanceService
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
    /// Điểm danh Check-in tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis.
    /// </summary>
    Task<ApiResponse<AttendanceRecordDto>> CheckInAsync(KioskCheckInDto request);

    /// <summary>
    /// Điểm danh Check-out tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis.
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

    /// <summary>
    /// Lấy lịch sử danh sách các bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date);

    /// <summary>
    /// Tra cứu tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý tại trạm Kiosk.
    /// </summary>
    Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null);

    /// <summary>
    /// Lấy quân số theo dõi trực tiếp thời gian thực tại cửa hàng kèm Presigned Temporary URL ảnh S3.
    /// </summary>
    Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null);

    /// <summary>
    /// Lấy lịch làm việc cá nhân theo tuần (Calendar View).
    /// </summary>
    Task<ApiResponse<MyWeeklyScheduleDto>> GetMyWeeklyScheduleAsync(ulong userId, DateOnly weekStart);

    /// <summary>
    /// Lấy lịch sử chấm công cá nhân theo tháng.
    /// </summary>
    Task<ApiResponse<MyAttendanceHistoryDto>> GetMyAttendanceHistoryAsync(ulong userId, int month, int year);
}
