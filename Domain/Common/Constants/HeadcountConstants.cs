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

    // ==========================================
    // 2. Quy định thời giờ làm việc (Bộ Luật Lao Động)
    // ==========================================

    /// <summary>
    /// Điều 110 BLLĐ: Khoảng nghỉ tối thiểu giữa 2 ca liên tiếp = 12 giờ liên tục.
    /// Áp dụng kiểm tra khi đổi ca / chuyển ca (shift swap/transfer).
    /// </summary>
    public const int MinRestBetweenShiftsHours = 12;

    /// <summary>
    /// Điều 105 &amp; 107 BLLĐ: Tổng số giờ làm việc tối đa trong một ngày = 12 giờ
    /// (bao gồm giờ thường và giờ làm thêm OT, làm thêm ≤ 50% giờ chuẩn 8h).
    /// </summary>
    public const int MaxWorkingHoursPerDay = 12;

    /// <summary>
    /// Điều 111 BLLĐ: Mỗi tuần người lao động phải được nghỉ ít nhất 24 giờ liên tục.
    /// </summary>
    public const int MinWeeklyRestHours = 24;

    /// <summary>
    /// Chu kỳ kiểm tra nghỉ tuần = 7 ngày liên tiếp tính từ ngày ca mới.
    /// </summary>
    public const int WeeklyRestWindowDays = 7;
}
