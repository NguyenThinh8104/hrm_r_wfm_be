namespace Modules.Stores.DTOs;

/// <summary>
/// DTO phản hồi trạng thái định biên nhân sự của chi nhánh theo mô hình Effective Quota.
/// </summary>
public class BranchHeadcountStatusDto
{
    public ulong BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string BranchTierName { get; set; } = string.Empty;
    public int BranchTierValue { get; set; }
    
    /// <summary>
    /// Phân cấp chi nhánh (Tier 1, Tier 2, Tier 3).
    /// </summary>
    public int BranchTier => BranchTierValue;

    /// <summary>
    /// Định biên chuẩn theo phân cấp quy mô chi nhánh (Tier Quota: Tier 1 = 30, Tier 2 = 15, Tier 3 = 8).
    /// </summary>
    public int StandardQuota { get; set; }

    /// <summary>
    /// Số lượng nhân sự định biên tùy chỉnh (Đã bãi bỏ, giữ 0 để tương thích backwards).
    /// </summary>
    public int StaffCount { get; set; } = 0;

    /// <summary>
    /// Định biên hiệu dụng: Luôn tuân thủ tuyệt đối định biên chuẩn theo phân cấp Tier (Tier 1 = 30, Tier 2 = 15, Tier 3 = 8).
    /// </summary>
    public int EffectiveQuota { get; set; }

    /// <summary>
    /// Alias tương thích ngược: Quota
    /// </summary>
    public int Quota => EffectiveQuota > 0 ? EffectiveQuota : StandardQuota;

    /// <summary>
    /// Số lượng nhân sự đang hoạt động thực tế (Status == 'ACTIVE').
    /// </summary>
    public int CurrentHeadcount { get; set; }

    /// <summary>
    /// Số lượng nhân sự đã nghỉ việc (Status == 'INACTIVE').
    /// </summary>
    public int InactiveCount { get; set; }

    /// <summary>
    /// Số lượng vị trí còn trống trong phạm vi định biên Tier.
    /// </summary>
    public int AvailableQuotaSlots { get; set; }

    /// <summary>
    /// Đánh dấu chi nhánh đã đạt hoặc vượt định biên chuẩn Tier.
    /// </summary>
    public bool IsQuotaReached { get; set; }

    /// <summary>
    /// Alias tương thích với Frontend: isStandardQuotaReached
    /// </summary>
    public bool IsStandardQuotaReached => IsQuotaReached;

    /// <summary>
    /// Có thể tạo nhân sự trực tiếp nếu quân số hiện tại nhỏ hơn định biên.
    /// </summary>
    public bool CanCreateDirectly { get; set; }

    /// <summary>
    /// Khả năng nâng cấp Tier khi chi nhánh đã kịch biên (Tier 3 -> Tier 2, Tier 2 -> Tier 1. Tier 1 là cao nhất).
    /// </summary>
    public bool CanUpgradeTier => BranchTierValue > 1;

    /// <summary>
    /// Phân cấp Tier tiếp theo khi nâng cấp (Tier 3 nâng lên 2, Tier 2 nâng lên 1).
    /// </summary>
    public int? NextTier => BranchTierValue > 1 ? BranchTierValue - 1 : null;

    /// <summary>
    /// Định biên chuẩn sau khi nâng lên Tier tiếp theo (Tier 3 -> 15, Tier 2 -> 30).
    /// </summary>
    public int? NextTierQuota => BranchTierValue == 3 ? 15 : (BranchTierValue == 2 ? 30 : null);

    /// <summary>
    /// Số lượng đơn đề xuất đang chờ duyệt (mặc định = 0 để tương thích FE cũ).
    /// </summary>
    public int PendingRequestsCount => 0;

    /// <summary>
    /// Số lượng suất mở rộng khả dụng từ đơn (mặc định = 0 để tương thích FE cũ).
    /// </summary>
    public int AvailableOverrideSlots => 0;

    /// <summary>
    /// Alias tương thích với Frontend: additionalApprovedQuota
    /// </summary>
    public int AdditionalApprovedQuota => 0;

    /// <summary>
    /// Tổng số lượng vị trí nhân sự còn khả dụng của chi nhánh.
    /// </summary>
    public int TotalAvailableSlots => AvailableQuotaSlots;
}
