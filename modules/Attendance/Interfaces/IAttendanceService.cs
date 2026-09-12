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
    /// Bước 2 quy trình Kiosk: Thực hiện điểm danh đầu ca (Check-in) bằng mã PIN nhân viên.
    /// </summary>
    /// <param name="request">DTO chứa EmployeeId, StoreId, PinCode, KioskId và tiền lẻ bàn giao (nếu là Thu ngân)</param>
    /// <returns>ApiResponse chứa bản ghi điểm danh đầu ca (Check-in time, status Present/Late)</returns>
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckInAsync(KioskPinCheckInDto request);

    /// <summary>
    /// Bước 2 quy trình Kiosk: Thực hiện điểm danh kết thúc ca (Check-out) bằng mã PIN nhân viên.
    /// </summary>
    /// <param name="request">DTO chứa EmployeeId, StoreId, PinCode và KioskId</param>
    /// <returns>ApiResponse chứa bản ghi điểm danh cập nhật thời gian ra ca</returns>
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request);

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
}
