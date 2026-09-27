using Domain.Enums;

namespace Domain.Constants;

/// <summary>
/// Hằng số và cấu hình định biên nhân sự chi nhánh (Branch Headcount Quota Constants).
/// </summary>
public static class HeadcountConstants
{
    // ==========================================
    // 1. Định biên chuẩn theo Phân cấp Chi nhánh (BranchTier Quotas)
    // ==========================================

    /// <summary>
    /// Định biên chuẩn cho Chi nhánh Tier 1 (Đại siêu thị / Flagship Store): Tối đa 30 nhân sự.
    /// </summary>
    public const int Tier1StandardQuota = 30;

    /// <summary>
    /// Định biên chuẩn cho Chi nhánh Tier 2 (Siêu thị tiêu chuẩn - Standard Store): Tối đa 15 nhân sự.
    /// </summary>
    public const int Tier2StandardQuota = 15;

    /// <summary>
    /// Định biên chuẩn cho Chi nhánh Tier 3 (Cửa hàng tiện lợi mini - Express Store): Tối đa 8 nhân sự.
    /// </summary>
    public const int Tier3StandardQuota = 8;

    /// <summary>
    /// Lấy số lượng định biên chuẩn theo phân cấp chi nhánh.
    /// </summary>
    public static int GetStandardQuota(BranchTier tier) => tier switch
    {
        BranchTier.Tier1 => Tier1StandardQuota,
        BranchTier.Tier2 => Tier2StandardQuota,
        BranchTier.Tier3 => Tier3StandardQuota,
        _ => Tier2StandardQuota
    };
}
