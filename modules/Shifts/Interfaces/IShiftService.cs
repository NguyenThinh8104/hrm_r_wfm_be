using Modules.Shifts.DTOs;
using Shared.Common;

namespace Modules.Shifts.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý ca làm việc, định mức nhu cầu nhân sự và phân bổ lịch trực (UC 2.1).
/// Bao gồm quy trình tạo mẫu ca chuẩn của Operations Admin, cấu hình định mức nhu cầu nhân sự 
/// và phân bổ hàng loạt nhân viên Full-time/Part-time của Store Manager.
/// </summary>
public interface IShiftService
{
    // ==========================================
    // 1. Quản lý Mẫu Ca Chuẩn (Operations Admin)
    // ==========================================

    /// <summary>
    /// Tạo mẫu ca làm việc chuẩn mới áp dụng cho hệ thống chuỗi cửa hàng.
    /// Logic đặc biệt: Kiểm tra trùng duy nhất mã ca TemplateCode (viết hoa), hỗ trợ ca qua đêm (IsOvernight = true) và thời gian nghỉ break.
    /// </summary>
    /// <param name="dto">DTO chứa dữ liệu đầu vào bao gồm TemplateCode, Name, StartTime, EndTime, IsOvernight, BreakDurationMinutes</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca vừa tạo thành công (ShiftDto) hoặc thông báo lỗi nếu trùng mã</returns>
    Task<ApiResponse<ShiftDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto);

    /// <summary>
    /// Cập nhật thông tin chi tiết của một mẫu ca làm việc chuẩn.
    /// Logic đặc biệt: Cho phép cập nhật giờ bắt đầu, giờ kết thúc, cờ qua đêm, số phút nghỉ giữa ca và bật/tắt trạng thái hoạt động IsActive.
    /// </summary>
    /// <param name="id">Mã ID định danh mẫu ca chuẩn (ShiftTemplate.Id)</param>
    /// <param name="dto">DTO chứa thông tin mới cần cập nhật</param>
    /// <returns>ApiResponse chứa thông tin mẫu ca sau khi cập nhật (ShiftDto) hoặc báo lỗi nếu không tìm thấy ID</returns>
    Task<ApiResponse<ShiftDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto);

    /// <summary>
    /// Vô hiệu hóa (Soft delete) mẫu ca làm việc chuẩn.
    /// Logic đặc biệt: Đổi cờ IsActive = false thay vì xóa cứng dữ liệu để bảo toàn lịch sử chấm công và ca trực đã xếp.
    /// </summary>
    /// <param name="id">Mã ID định danh mẫu ca chuẩn cần vô hiệu hóa</param>
    /// <returns>ApiResponse trả về cờ boolean xác nhận thao tác vô hiệu hóa thành công</returns>
    Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id);

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc mẫu trong hệ thống.
    /// Logic đặc biệt: Mặc định chỉ lấy các ca active. Nếu includeInactive = true sẽ lấy tất cả ca phục vụ màn hình quản trị Admin.
    /// </summary>
    /// <param name="includeInactive">Cờ tùy chọn: True lấy cả ca đã vô hiệu hóa, False chỉ lấy ca đang hoạt động</param>
    /// <returns>ApiResponse chứa danh sách DTO thông tin mẫu ca chuẩn (List&lt;ShiftDto&gt;)</returns>
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(bool includeInactive = false);

    // =========================================================
    // 2. Khởi Tạo Khung Lịch & Định Mức Nhu Cầu (Store Manager & Admin)
    // =========================================================

    /// <summary>
    /// Tự động sinh khung mẫu lịch làm việc thô cho tất cả các ngày trong tháng tại chi nhánh chỉ định.
    /// Logic đặc biệt: Duyệt từng ngày từ 1 đến ngày cuối tháng (28-31 ngày), ghép với danh sách mẫu ca được chọn (hoặc tất cả mẫu ca active).
    /// Bỏ qua các ca ngày đã tồn tại (nếu chạy lại), thiết lập số lượng định mức nhu cầu nhân sự mặc định (DefaultRequiredCashier, DefaultRequiredSales, DefaultRequiredSecurity) ở trạng thái DRAFT.
    /// </summary>
    /// <param name="dto">DTO cấu hình bao gồm BranchId, Year, Month, danh sách TemplateIds và chỉ tiêu số lượng nhân sự từng vị trí</param>
    /// <param name="createdByUserId">ID người dùng thực hiện khởi tạo khung lịch (dùng để ghi vết Audit Log)</param>
    /// <returns>ApiResponse chứa danh sách khung lịch thô của tất cả các ca trong tháng (List&lt;WorkScheduleDto&gt;)</returns>
    Task<ApiResponse<List<WorkScheduleDto>>> GenerateMonthlyScheduleAsync(GenerateMonthlyScheduleDto dto, ulong createdByUserId);

    /// <summary>
    /// Điều chỉnh định mức nhu cầu số lượng nhân sự theo từng vị trí (Thu ngân, Bán hàng, Bảo vệ) cho 1 ca trực.
    /// Logic đặc biệt: Cập nhật định mức RequiredCashier, RequiredSales, RequiredSecurity và tự động đếm số lượng nhân sự đã phân bổ thực tế.
    /// </summary>
    /// <param name="dto">DTO chứa ScheduleId và định mức số lượng nhân sự mới từng vị trí</param>
    /// <returns>ApiResponse chứa thông tin khung ca làm việc đã cập nhật chỉ tiêu (WorkScheduleDto)</returns>
    Task<ApiResponse<WorkScheduleDto>> UpdateScheduleRequirementAsync(UpdateScheduleRequirementDto dto);

    /// <summary>
    /// Truy vấn danh sách khung lịch làm việc kèm chỉ tiêu định mức nhu cầu nhân sự của chi nhánh trong tháng.
    /// Logic đặc biệt: Lọc theo BranchId, Year, Month, sắp xếp tăng dần theo WorkDate và StartTime của ca.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm làm việc (Ví dụ: 2026)</param>
    /// <param name="month">Tháng làm việc (1 - 12)</param>
    /// <returns>ApiResponse chứa danh sách bản ghi WorkScheduleDto kèm số lượng đã gán thực tế</returns>
    Task<ApiResponse<List<WorkScheduleDto>>> GetMonthlySchedulesAsync(ulong branchId, int year, int month);

    // =========================================================
    // 3. Phân Bổ Nhân Sự & Công Bố Lịch (Store Manager)
    // =========================================================

    /// <summary>
    /// Phân bổ hàng loạt nhân viên Full-time/Part-time vào các ca làm việc trong tháng.
    /// Logic đặc biệt: Tự động kiểm tra xung đột trùng ca (1 nhân viên không thể trực 2 ca cùng 1 ngày ở bất kỳ chi nhánh nào).
    /// Tự động lấy RoleId mặc định của nhân viên để gán vào ca. Nếu ca chưa có bản ghi WorkSchedule sẽ tự tạo tự động. Các mục lỗi sẽ được ghi nhận và trả về danh sách cảnh báo.
    /// </summary>
    /// <param name="dto">DTO chứa BranchId và danh sách các mục phân công (UserId, ShiftTemplateId, WorkDate)</param>
    /// <returns>ApiResponse chứa danh sách các bản ghi gán ca thành công (List&lt;ShiftAssignmentDto&gt;) và thông báo tổng hợp</returns>
    Task<ApiResponse<List<ShiftAssignmentDto>>> BatchAssignShiftsAsync(BatchAssignShiftDto dto);

    /// <summary>
    /// Truy vấn dữ liệu ma trận phân bổ lịch làm việc tháng cho màn hình Store Manager Dashboard.
    /// Logic đặc biệt: Tổng hợp danh sách tất cả nhân viên thuộc chi nhánh (HomeBranchId) và các nhân viên được biệt phái/gán ca tại cửa hàng.
    /// Tạo cấu trúc lưới 2 chiều (Nhân viên x Ngày trong tháng) giúp Quản lý cửa hàng có cái nhìn toàn cảnh về phân bổ nhân sự.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm tra cứu ma trận lịch</param>
    /// <param name="month">Tháng tra cứu ma trận lịch (1 - 12)</param>
    /// <returns>ApiResponse chứa đối tượng MonthlyScheduleMatrixDto gồm dữ liệu khung ca và lưới phân bổ ca từng nhân viên</returns>
    Task<ApiResponse<MonthlyScheduleMatrixDto>> GetMonthlyRosterMatrixAsync(ulong branchId, int year, int month);

    /// <summary>
    /// Duyệt và công bố phát hành toàn bộ lịch làm việc trong tháng cho cửa hàng.
    /// Logic đặc biệt: Chuyển trạng thái tất cả WorkSchedule trong tháng từ DRAFT sang PUBLISHED, đồng thời cập nhật toàn bộ ShiftAssignment sang CONFIRMED để nhân viên nhìn thấy trên Mobile app.
    /// </summary>
    /// <param name="branchId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="year">Năm phát hành lịch</param>
    /// <param name="month">Tháng phát hành lịch</param>
    /// <param name="publishedByUserId">Mã ID của Quản lý cửa hàng duyệt phát hành</param>
    /// <returns>ApiResponse trả về kết quả boolean xác nhận công bố thành công</returns>
    Task<ApiResponse<bool>> PublishMonthlyScheduleAsync(ulong branchId, int year, int month, ulong publishedByUserId);

    // ==========================================
    // 4. Các Phương Thức Tương Thích Hiện Có
    // ==========================================

    /// <summary>
    /// Lấy danh sách mẫu ca làm việc active cho client.
    /// </summary>
    /// <returns>ApiResponse chứa danh sách ShiftDto</returns>
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync();

    /// <summary>
    /// Lấy lịch phân công nhân sự theo khoảng thời gian từ startDate đến endDate.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="startDate">Ngày bắt đầu tra cứu</param>
    /// <param name="endDate">Ngày kết thúc tra cứu</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto</returns>
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Phân công 1 ca lẻ cho nhân viên.
    /// </summary>
    /// <param name="request">DTO chứa thông tin gán ca đơn lẻ</param>
    /// <returns>ApiResponse chứa ShiftAssignmentDto thành công</returns>
    Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request);

    /// <summary>
    /// Phát hành lịch làm việc theo tuần cho cửa hàng.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <param name="weekStartDate">Ngày bắt đầu tuần (Thứ Hai)</param>
    /// <param name="publishedByEmployeeId">ID người duyệt phát hành</param>
    /// <returns>ApiResponse trả về boolean kết quả</returns>
    Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId);

    /// <summary>
    /// Lấy danh sách ca làm việc cá nhân của một nhân viên trong khoảng thời gian.
    /// </summary>
    /// <param name="employeeId">ID nhân viên</param>
    /// <param name="startDate">Ngày bắt đầu</param>
    /// <param name="endDate">Ngày kết thúc</param>
    /// <returns>ApiResponse chứa danh sách ShiftAssignmentDto cá nhân</returns>
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Gửi yêu cầu đổi ca trực giữa 2 nhân viên.
    /// </summary>
    /// <param name="requesterEmployeeId">ID nhân viên xin đổi ca</param>
    /// <param name="request">DTO chứa ca cần đổi và người muốn đổi cùng lý do</param>
    /// <returns>ApiResponse chứa ShiftSwapRequestDto</returns>
    Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request);

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi ca trực.
    /// </summary>
    /// <param name="managerEmployeeId">ID quản lý duyệt</param>
    /// <param name="request">DTO chứa ID yêu cầu đổi ca và kết quả duyệt (Approved/Rejected)</param>
    /// <returns>ApiResponse trả về boolean kết quả</returns>
    Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request);

    /// <summary>
    /// Lấy danh sách danh mục các yêu cầu đổi ca trong cửa hàng.
    /// </summary>
    /// <param name="storeId">ID cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách ShiftSwapRequestDto</returns>
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId);
}
