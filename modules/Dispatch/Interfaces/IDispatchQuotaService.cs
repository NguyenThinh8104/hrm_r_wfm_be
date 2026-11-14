using Modules.Dispatch.DTOs;
using Shared.Common;

namespace Modules.Dispatch.Interfaces;

/// <summary>
/// Dịch vụ tính toán hạn mức định biên điều động an toàn (40% quân số) và kiểm tra tư cách nhân sự chi viện.
/// </summary>
public interface IDispatchQuotaService
{
    /// <summary>
    /// Tra cứu định biên an toàn và số lượng nhân sự khả dụng cho điều động của chi nhánh cho mượn (SourceBranch).
    /// Quy tắc: Số nhân sự đang đi chi viện (lệnh APPROVED còn hiệu lực) + số mới không vượt quá 40% tổng quân số.
    /// </summary>
    Task<ApiResponse<BranchDispatchQuotaStatusDto>> GetBranchDispatchQuotaAsync(
        ulong branchId, 
        DateOnly? startDate, 
        DateOnly? endDate, 
        ulong? excludeDispatchId = null);

    /// <summary>
    /// Lấy danh sách nhân sự của chi nhánh phục vụ điều động chi viện liên chi nhánh (UC 4.1).
    /// Tự động loại trừ các chức danh quản lý (Store Manager, Operations Admin, Business Owner).
    /// </summary>
    Task<ApiResponse<List<DispatchEmployeeOptionDto>>> GetBranchEmployeesForDispatchAsync(ulong branchId);
}
