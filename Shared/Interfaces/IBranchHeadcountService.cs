using Shared.Common;

namespace Shared.Interfaces;

/// <summary>
/// Interface dịch vụ quản trị định biên nhân sự chi nhánh (Headcount & Effective Quota Service).
/// Đặt tại tầng Shared để modules/Auth có thể gọi kiểm tra Quota từ modules/Stores mà không vi phạm nguyên tắc đóng gói module.
/// </summary>
public interface IBranchHeadcountService
{
    /// <summary>
    /// Thẩm định định biên nhân sự của chi nhánh dựa trên phân cấp chuẩn Tier (Tier 1 = 30, Tier 2 = 15, Tier 3 = 8).
    /// </summary>
    /// <param name="branchId">ID chi nhánh chỉ định tạo nhân sự.</param>
    /// <param name="additionalBatchCount">Số lượng nhân sự đang tạm tính trong batch import hiện tại (phục vụ bulk import Excel).</param>
    /// <returns>Đối tượng HeadcountValidationResult chứa trạng thái thành công/thất bại và EffectiveQuota.</returns>
    Task<HeadcountValidationResult> ValidateHeadcountAsync(ulong branchId, int additionalBatchCount = 0);
}
