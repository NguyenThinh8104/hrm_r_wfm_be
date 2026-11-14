using Modules.Dispatch.DTOs;
using Shared.Common;

namespace Modules.Dispatch.Interfaces;

/// <summary>
/// Dịch vụ quản lý vòng đời và quy trình xét duyệt đề nghị chi viện nhân sự liên chi nhánh.
/// </summary>
public interface IDispatchWorkflowService
{
    /// <summary>
    /// [Store Manager A] Tạo phiếu đề nghị chi viện nhân sự liên chi nhánh (UC 4.1).
    /// </summary>
    Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request);

    /// <summary>
    /// [Store Manager B] Phê duyệt hoặc từ chối đơn đề nghị chi viện và chỉ định nhân sự (UC 4.2).
    /// </summary>
    Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request);

    /// <summary>
    /// [Store Manager] Lấy danh sách các lệnh điều động liên quan đến cửa hàng (2 chiều cho mượn và nhận).
    /// </summary>
    Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId);

    /// <summary>
    /// [Quản lý & Admin] Lấy toàn bộ danh sách lệnh điều động toàn hệ thống có hỗ trợ lọc.
    /// </summary>
    Task<ApiResponse<List<DispatchRecordDto>>> GetAllDispatchesAsync(string? status, int? storeId);

    /// <summary>
    /// [Store Manager A] Chỉnh sửa đơn đề nghị chi viện đang chờ duyệt (PENDING) hoặc đã thu hồi (RECALLED).
    /// </summary>
    Task<ApiResponse<DispatchRecordDto>> UpdateDispatchRequestAsync(int managerId, int dispatchId, UpdateDispatchRequestDto request);

    /// <summary>
    /// [Store Manager A] Thu hồi đơn đề nghị chi viện đang chờ duyệt để chỉnh sửa (RECALLED).
    /// </summary>
    Task<ApiResponse<DispatchRecordDto>> RecallDispatchRequestAsync(int managerId, int dispatchId, string? reason = null);

    /// <summary>
    /// [Store Manager A] Hủy và xóa đơn đề nghị chi viện khi đang chờ duyệt (PENDING) hoặc đã thu hồi (RECALLED).
    /// </summary>
    Task<ApiResponse<bool>> DeleteDispatchRequestAsync(int managerId, int dispatchId);
}
