using Shared.Common;

namespace Shared.Interfaces;

/// <summary>
/// Interface dịch vụ quản trị định biên nhân sự chi nhánh (Headcount & Override Service).
/// Đặt tại tầng Shared để modules/Auth có thể gọi kiểm tra Quota từ modules/Stores mà không vi phạm nguyên tắc đóng gói module.
/// </summary>
public interface IBranchHeadcountService
{
    /// <summary>
    /// Thẩm định định biên chi nhánh và trừ lùi chỉ tiêu từ đơn mở rộng nếu chi nhánh đã đạt giới hạn Quota.
    /// </summary>
    /// <param name="branchId">ID chi nhánh chỉ định tạo nhân sự.</param>
    /// <param name="importRequestId">ID đơn xin mở rộng định biên (bắt buộc nếu vượt Quota).</param>
    /// <param name="expansionReason">Lý do mở rộng định biên (bắt buộc nếu vượt Quota).</param>
    /// <param name="actorId">ID người thực hiện thao tác (Operations Admin).</param>
    /// <returns>Đối tượng HeadcountValidationResult chứa trạng thái thành công/thất bại và cờ IsOverride.</returns>
    Task<HeadcountValidationResult> ValidateAndConsumeQuotaAsync(
        ulong branchId, 
        ulong? importRequestId, 
        string? expansionReason, 
        ulong actorId);
}
