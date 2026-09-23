using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface nghiệp vụ Quản lý Định biên Nhân sự chi nhánh (Stores Module).
/// </summary>
public interface IStoreHeadcountService
{
    /// <summary>
    /// Lấy thông tin định biên chi nhánh (Quy mô Tier, Quota, Quân số hiện tại, Số vị trí trống).
    /// </summary>
    Task<ApiResponse<BranchHeadcountStatusDto>> GetBranchHeadcountStatusAsync(ulong branchId);

    /// <summary>
    /// Store Manager upload file Excel (.xlsx) nộp đơn đề xuất mở rộng định biên.
    /// </summary>
    Task<ApiResponse<HeadcountImportRequestDto>> SubmitImportRequestAsync(
        ulong managerId, 
        ulong? managerBranchId, 
        UploadHeadcountRequestDto dto);

    /// <summary>
    /// Operations Admin thẩm định đơn: Phê duyệt (hỗ trợ duyệt một phần Partial Approval) hoặc Từ chối (Reject).
    /// </summary>
    Task<ApiResponse<HeadcountImportRequestDto>> ReviewRequestAsync(
        ulong requestId, 
        ReviewHeadcountRequestDto dto, 
        ulong adminId);

    /// <summary>
    /// Operations Admin đóng đơn hoặc đánh dấu hết hạn thủ công đối với các đơn cũ còn dư suất.
    /// </summary>
    Task<ApiResponse<bool>> CloseOrExpireRequestAsync(
        ulong requestId, 
        ulong adminId, 
        string reason);

    /// <summary>
    /// Lấy danh sách các đơn mở rộng định biên còn khả dụng (Status == APPROVED hoặc PENDING, AdditionalQuantity > 0, chưa hết hạn) của chi nhánh.
    /// Phục vụ dropdown cho Operations Admin khi tạo nhân sự vượt định biên.
    /// </summary>
    Task<ApiResponse<List<HeadcountImportRequestDto>>> GetAvailableRequestsForBranchAsync(ulong branchId);

    /// <summary>
    /// Lấy danh sách toàn bộ các đơn đề xuất mở rộng định biên (hỗ trợ lọc theo trạng thái và chi nhánh).
    /// </summary>
    Task<ApiResponse<List<HeadcountImportRequestDto>>> GetAllRequestsAsync(string? status, ulong? branchId);

    /// <summary>
    /// Xem chi tiết một đơn đề xuất mở rộng định biên theo ID.
    /// </summary>
    Task<ApiResponse<HeadcountImportRequestDto>> GetRequestByIdAsync(ulong id);
}
