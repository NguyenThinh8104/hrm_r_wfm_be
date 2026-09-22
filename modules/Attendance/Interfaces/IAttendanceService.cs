using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Interface dịch vụ chấm công Kiosk và xử lý gian lận điểm danh tại quầy cửa hàng.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Bước 1 quy trình Kiosk: Xác thực mã PIN nhân viên và trả về thông tin ca làm việc hôm nay.
    /// </summary>
    /// <param name="request">DTO chứa mã nhân viên EmployeeCode, mã PIN và StoreId cửa hàng</param>
    /// <returns>ApiResponse chứa thông tin nhân viên, ca trực hôm nay và trạng thái điểm danh vào/ra</returns>
    Task<ApiResponse<ValidatePinResponseDto>> ValidatePinAsync(ValidatePinRequestDto request);

    /// <summary>
    /// Lấy danh sách phân công lịch làm việc hôm nay tại trạm Kiosk cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID cửa hàng</param>
    /// <param name="date">Ngày làm việc cần lấy danh sách (mặc định hôm nay)</param>
    /// <returns>ApiResponse chứa danh sách nhân sự xếp ca kèm thời gian và trạng thái điểm danh</returns>
    Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date);

    /// <summary>
    /// Điểm danh Check-in tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis, so khớp khung giờ ca làm việc.
    /// </summary>
    /// <param name="request">DTO chứa KioskDeviceToken và OtpCode 60s</param>
    /// <returns>ApiResponse chứa bản ghi điểm danh đầu ca (Status PENDING chờ ảnh)</returns>
    Task<ApiResponse<AttendanceRecordDto>> CheckInAsync(KioskCheckInDto request);

    /// <summary>
    /// Điểm danh Check-out tại quầy Kiosk: Xác thực Kiosk Token, mã OTP 60s, bóc tách UserId từ Redis, cập nhật CheckOutTime.
    /// </summary>
    /// <param name="request">DTO chứa KioskDeviceToken và OtpCode 60s</param>
    /// <returns>ApiResponse chứa bản ghi điểm danh cập nhật thời gian ra ca (Status PENDING chờ ảnh)</returns>
    Task<ApiResponse<AttendanceRecordDto>> CheckOutAsync(KioskCheckOutDto request);

    /// <summary>
    /// Upload ảnh chấm công (bắt buộc) lên S3 và cập nhật attendance record → COMPLETED.
    /// </summary>
    /// <param name="request">DTO chứa KioskDeviceToken, AttendanceId, ImageBase64, PhotoType</param>
    /// <returns>ApiResponse chứa photoKey, presignedUrl và status COMPLETED</returns>
    Task<ApiResponse<UploadAttendancePhotoResponseDto>> UploadAttendancePhotoAsync(UploadAttendancePhotoDto request);

    /// <summary>
    /// Trưởng ca / Quản lý báo cáo gian lận điểm danh hoặc vắng mặt của nhân viên.
    /// </summary>
    /// <param name="leaderEmployeeId">ID nhân viên của Trưởng ca lập báo cáo</param>
    /// <param name="request">DTO chứa AssignmentId, loại lỗi gian lận và ghi chú chi tiết</param>
    /// <returns>ApiResponse xác nhận kết quả lập báo cáo ngoại lệ gian lận thành công</returns>
    Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request);

    /// <summary>
    /// Lấy lịch sử danh sách các bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="date">Ngày tra cứu lịch sử điểm danh</param>
    /// <returns>ApiResponse chứa danh sách bản ghi điểm danh chi tiết trong ngày</returns>
    Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date);

    /// <summary>
    /// Tra cứu tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý tại trạm Kiosk.
    /// </summary>
    /// <param name="storeId">Mã ID cửa hàng</param>
    /// <param name="query">Từ khóa tìm kiếm theo Mã NV hoặc Tên</param>
    /// <returns>ApiResponse chứa danh sách nhân viên thỏa điều kiện</returns>
    Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null);

    /// <summary>
    /// Lấy quân số theo dõi trực tiếp thời gian thực tại cửa hàng kèm Presigned Temporary URL ảnh S3.
    /// </summary>
    Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null);

    /// <summary>
    /// Store Manager phân xử khiếu nại (Duyệt khôi phục giờ công hoặc Bác bỏ).
    /// </summary>
    Task<ApiResponse<bool>> ResolveFraudAsync(ResolveFraudDto request);

    /// <summary>
    /// Lấy lịch làm việc cá nhân theo tuần (Calendar View)
    /// </summary>
    Task<ApiResponse<MyWeeklyScheduleDto>> GetMyWeeklyScheduleAsync(ulong userId, DateOnly weekStart);

    /// <summary>
    /// Lấy lịch sử chấm công cá nhân theo tháng
    /// </summary>
    Task<ApiResponse<MyAttendanceHistoryDto>> GetMyAttendanceHistoryAsync(ulong userId, int month, int year);
}
