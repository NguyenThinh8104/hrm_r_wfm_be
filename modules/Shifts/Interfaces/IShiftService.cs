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
    // 1. Quản lý Mẫu Ca Chuẩn (UC 1.3 - Operations Admin)
    // ==========================================

    /// <summary>
    /// Tạo mẫu ca làm việc chuẩn mới áp dụng cho hệ thống chuỗi cửa hàng (UC 1.3).
    /// </summary>
    Task<ApiResponse<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto);

    /// <summary>
    /// Cập nhật thông tin chi tiết của một mẫu ca làm việc chuẩn.
    /// </summary>
    Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto);

    /// <summary>
    /// Cập nhật trạng thái mẫu ca làm việc (ACTIVE / INACTIVE) - Không xóa cứng để bảo toàn lịch sử chấm công.
    /// </summary>
    Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateStatusAsync(uint id, UpdateShiftTemplateStatusDto dto);

    /// <summary>
    /// Lấy thông tin chi tiết mẫu ca chuẩn theo ID.
    /// </summary>
    Task<ApiResponse<ShiftTemplateDto>> GetShiftTemplateByIdAsync(uint id);

    /// <summary>
    /// Vô hiệu hóa (Soft delete) mẫu ca làm việc chuẩn.
    /// </summary>
    Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id);

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc mẫu trong hệ thống (hỗ trợ lọc status: ACTIVE / INACTIVE).
    /// </summary>
    Task<ApiResponse<List<ShiftTemplateDto>>> GetAllShiftTemplatesAsync(string? status = null);
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

    // =========================================================
    // 3b. Quản Lý Lịch Tuần & Xung Đột & Công Bố Tuần (UC 2.1 & UC 2.3)
    // =========================================================

    /// <summary>
    /// Khởi tạo khung mẫu ca cho 7 ngày trong tuần theo định mức mặc định (UC 2.1).
    /// </summary>
    Task<ApiResponse<WeeklyScheduleMatrixDto>> GenerateWeeklyScheduleAsync(GenerateWeeklyScheduleDto dto, ulong createdByUserId);

    /// <summary>
    /// Lấy ma trận lịch phân bổ ca tuần (7 ngày) kèm chỉ tiêu định mức và danh sách nhân viên (UC 2.1 & UC 2.3).
    /// </summary>
    Task<ApiResponse<WeeklyScheduleMatrixDto>> GetWeeklyScheduleMatrixAsync(ulong branchId, DateOnly weekStartDate);

    /// <summary>
    /// Phân bổ nhanh danh sách nhân viên Full-time vào ca trực trong tuần (UC 2.1).
    /// Tự động chặn trùng giờ/trùng ngày ở bất kỳ chi nhánh nào.
    /// </summary>
    Task<ApiResponse<List<ShiftAssignmentDto>>> AssignFullTimeBatchAsync(AssignFullTimeBatchDto dto);

    /// <summary>
    /// Rà soát xung đột và kiểm tra tình trạng đủ/thiếu định mức trước khi công bố lịch tuần (UC 2.3).
    /// </summary>
    Task<ApiResponse<ScheduleConflictCheckResultDto>> CheckWeeklyConflictsAsync(ulong branchId, DateOnly weekStartDate);

    /// <summary>
    /// Store Manager duyệt và công bố phát hành lịch tuần (UC 2.3).
    /// Chuyển trạng thái sang PUBLISHED và CONFIRMED.
    /// </summary>
    Task<ApiResponse<bool>> PublishWeeklyScheduleAsync(ulong branchId, DateOnly weekStartDate, ulong publishedByUserId);

    /// <summary>
    /// Xóa/Hủy 1 phân công ca làm việc của nhân viên.
    /// </summary>
    Task<ApiResponse<bool>> DeleteShiftAssignmentAsync(ulong assignmentId);

    /// <summary>
    /// Tự động giải và xếp lịch ca tuần tối ưu bằng Google OR-Tools Constraint Programming Solver (UC 2.1).
    /// </summary>
    Task<ApiResponse<AutoScheduleResultDto>> AutoScheduleWeeklyAsync(AutoScheduleWeeklyDto dto, ulong userId);



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

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển ca của chính nhân viên (đã gửi hoặc được nhờ).
    /// </summary>
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetMySwapRequestsAsync(int employeeId);

    /// <summary>
    /// Lấy danh sách đồng nghiệp cùng chi nhánh đủ điều kiện để đổi/chuyển ca.
    /// </summary>
    Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId);

    /// <summary>
    /// Lấy danh sách các ca làm việc của một đồng nghiệp trong tương lai để chọn đổi (loại bỏ các ca mà nhân viên hiện tại đã có lịch).
    /// </summary>
    Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId);
}
