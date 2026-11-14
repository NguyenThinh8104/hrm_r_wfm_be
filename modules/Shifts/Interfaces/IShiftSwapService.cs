using Modules.Shifts.DTOs;
using Shared.Common;

namespace Modules.Shifts.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý quy trình đổi / chuyển ca trực và xác thực đồng nghiệp 2 bước (UC 2.2).
/// </summary>
public interface IShiftSwapService
{
    /// <summary>
    /// Gửi yêu cầu đổi ca trực hoặc chuyển ca giữa các nhân viên.
    /// </summary>
    Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request);

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi ca trực (Bước 2).
    /// </summary>
    Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request);

    /// <summary>
    /// Đồng nghiệp (TargetUser) xác nhận hoặc từ chối đơn đổi/chuyển ca (Bước 1 của luồng 2 bước).
    /// Chỉ áp dụng cho đơn có Status = "PENDING_PEER".
    /// </summary>
    Task<ApiResponse<bool>> RespondToSwapRequestAsync(int peerEmployeeId, PeerReviewSwapRequestDto request);

    /// <summary>
    /// Nhân viên tạo đơn (RequesterUser) hủy đơn đổi/chuyển/nghỉ ca đang chờ xử lý.
    /// Chỉ áp dụng cho đơn có Status = "PENDING" hoặc "PENDING_PEER".
    /// </summary>
    Task<ApiResponse<bool>> CancelSwapRequestAsync(int requesterEmployeeId, int swapRequestId);

    /// <summary>
    /// Lấy danh sách danh mục các yêu cầu đổi ca trong cửa hàng.
    /// </summary>
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId);

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển ca của chính nhân viên (đã gửi hoặc được nhờ).
    /// </summary>
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetMySwapRequestsAsync(int employeeId);

    /// <summary>
    /// Lấy danh sách đồng nghiệp cùng chi nhánh đủ điều kiện để đổi/chuyển ca.
    /// </summary>
    Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId, DateOnly? workDate = null);

    /// <summary>
    /// Lấy danh sách các ca làm việc của một đồng nghiệp trong tương lai để chọn đổi.
    /// </summary>
    Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId, int? requestingAssignmentId = null);
}
