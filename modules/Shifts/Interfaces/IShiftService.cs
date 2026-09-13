using Modules.Shifts.DTOs;
using Shared.Common;

namespace Modules.Shifts.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý ca làm việc, định mức nhu cầu nhân sự và phân bổ lịch trực (UC 2.1).
/// </summary>
public interface IShiftService
{
    // ==========================================
    // 1. Quản lý Mẫu Ca Chuẩn (Operations Admin)
    // ==========================================

    /// <summary>
    /// Tạo mẫu ca chuẩn mới cho toàn hệ thống chuỗi cửa hàng.
    /// </summary>
    Task<ApiResponse<ShiftDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto);

    /// <summary>
    /// Cập nhật thông tin mẫu ca làm việc chuẩn.
    /// </summary>
    Task<ApiResponse<ShiftDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto);

    /// <summary>
    /// Vô hiệu hóa (Soft delete) mẫu ca làm việc chuẩn.
    /// </summary>
    Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id);

    /// <summary>
    /// Lấy danh sách tất cả các ca làm việc mẫu (cho phép lọc bao gồm cả ca ngưng hoạt động).
    /// </summary>
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(bool includeInactive = false);

    // =========================================================
    // 2. Khởi Tạo Khung Lịch & Định Mức Nhu Cầu (Store Manager & Admin)
    // =========================================================

    /// <summary>
    /// Khởi tạo khung mẫu lịch làm việc thô cho tất cả các ngày trong tháng của chi nhánh.
    /// </summary>
    Task<ApiResponse<List<WorkScheduleDto>>> GenerateMonthlyScheduleAsync(GenerateMonthlyScheduleDto dto, ulong createdByUserId);

    /// <summary>
    /// Điều chỉnh định mức nhu cầu số lượng nhân sự (Thu ngân, Bán hàng, Bảo vệ) cho một ca trực.
    /// </summary>
    Task<ApiResponse<WorkScheduleDto>> UpdateScheduleRequirementAsync(UpdateScheduleRequirementDto dto);

    /// <summary>
    /// Lấy danh sách khung lịch và định mức nhu cầu nhân sự của chi nhánh trong tháng.
    /// </summary>
    Task<ApiResponse<List<WorkScheduleDto>>> GetMonthlySchedulesAsync(ulong branchId, int year, int month);

    // =========================================================
    // 3. Phân Bổ Nhân Sự & Công Bố Lịch (Store Manager)
    // =========================================================

    /// <summary>
    /// Phân bổ hàng loạt nhân viên Full-time/Part-time vào ca trực với kiểm tra chống trùng ca tự động.
    /// </summary>
    Task<ApiResponse<List<ShiftAssignmentDto>>> BatchAssignShiftsAsync(BatchAssignShiftDto dto);

    /// <summary>
    /// Lấy dữ liệu ma trận phân bổ ca làm việc tháng (Nhân viên x Ngày) phục vụ màn hình Store Manager.
    /// </summary>
    Task<ApiResponse<MonthlyScheduleMatrixDto>> GetMonthlyRosterMatrixAsync(ulong branchId, int year, int month);

    /// <summary>
    /// Duyệt và công bố toàn bộ lịch ca làm việc trong tháng cho nhân viên cửa hàng.
    /// </summary>
    Task<ApiResponse<bool>> PublishMonthlyScheduleAsync(ulong branchId, int year, int month, ulong publishedByUserId);

    // ==========================================
    // 4. Các Phương Thức Tương Thích Hiện Có
    // ==========================================
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync();
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate);
    Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request);
    Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId);
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate);
    Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request);
    Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request);
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId);
}
