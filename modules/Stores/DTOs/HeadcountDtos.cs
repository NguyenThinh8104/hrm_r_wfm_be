using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Modules.Stores.DTOs;

/// <summary>
/// DTO phản hồi trạng thái định biên nhân sự của chi nhánh.
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
    /// Định biên chuẩn theo quy mô chi nhánh (Tier Quota).
    /// </summary>
    public int StandardQuota { get; set; }

    /// <summary>
    /// Số lượng nhân sự đang hoạt động thực tế (Status == 'ACTIVE').
    /// </summary>
    public int CurrentHeadcount { get; set; }

    /// <summary>
    /// Số lượng nhân sự đã nghỉ việc (Status == 'INACTIVE').
    /// </summary>
    public int InactiveCount { get; set; }

    /// <summary>
    /// Số lượng vị trí còn trống trong phạm vi định biên chuẩn.
    /// </summary>
    public int AvailableQuotaSlots => Math.Max(0, StandardQuota - CurrentHeadcount);

    /// <summary>
    /// Đánh dấu chi nhánh đã đạt hoặc vượt định biên chuẩn.
    /// </summary>
    public bool IsQuotaReached => CurrentHeadcount >= StandardQuota;

    /// <summary>
    /// Alias tương thích với Frontend: isStandardQuotaReached
    /// </summary>
    public bool IsStandardQuotaReached => CurrentHeadcount >= StandardQuota;

    /// <summary>
    /// Có thể tạo nhân sự trực tiếp không cần đơn ngoại lệ.
    /// </summary>
    public bool CanCreateDirectly => CurrentHeadcount < StandardQuota;

    /// <summary>
    /// Số lượng đơn đề xuất mở rộng định biên đang chờ duyệt (PENDING).
    /// </summary>
    public int PendingRequestsCount { get; set; }

    /// <summary>
    /// Tổng số lượng suất nhân sự bổ sung còn khả dụng từ các đơn mở rộng đã được duyệt.
    /// </summary>
    public int AvailableOverrideSlots { get; set; }

    /// <summary>
    /// Alias tương thích với Frontend: additionalApprovedQuota
    /// </summary>
    public int AdditionalApprovedQuota => AvailableOverrideSlots;

    /// <summary>
    /// Tổng số lượng vị trí nhân sự còn khả dụng của chi nhánh.
    /// </summary>
    public int TotalAvailableSlots => AvailableQuotaSlots + AvailableOverrideSlots;
}

/// <summary>
/// DTO thông tin chi tiết đơn đề xuất mở rộng định biên.
/// </summary>
public class HeadcountImportRequestDto
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public ulong RequestedBy { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmployeeCode { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRequested { get; set; }
    public int ApprovedQuantity { get; set; }
    public int TotalApproved { get; set; }
    public int AdditionalQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public ulong? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO tải lên file Excel xin mở rộng định biên từ Store Manager.
/// </summary>
public class UploadHeadcountRequestDto
{
    [Required(ErrorMessage = "Vui lòng chọn chi nhánh.")]
    public ulong BranchId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số lượng nhân sự đề xuất.")]
    [Range(1, 50, ErrorMessage = "Số lượng nhân sự đề xuất phải từ 1 đến 50 người.")]
    public int TotalRequested { get; set; }

    /// <summary>
    /// Alias tương thích frontend: requestedQuantity
    /// </summary>
    public int? RequestedQuantity
    {
        get => TotalRequested;
        set { if (value.HasValue && value.Value > 0) TotalRequested = value.Value; }
    }

    [Required(ErrorMessage = "Vui lòng nhập lý do đề xuất tăng định biên.")]
    [StringLength(1000, ErrorMessage = "Lý do không được vượt quá 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng đính kèm file Excel (.xlsx / .xls / .csv).")]
    public IFormFile File { get; set; } = null!;
}

/// <summary>
/// DTO thẩm định và phê duyệt đơn mở rộng định biên từ Operations Admin.
/// Hỗ trợ phê duyệt một phần (Partial Approval) hoặc từ chối (Reject).
/// </summary>
public class ReviewHeadcountRequestDto
{
    [Required(ErrorMessage = "Vui lòng xác định hành động (Phê duyệt hoặc Từ chối).")]
    public bool IsApproved { get; set; }

    /// <summary>
    /// Số lượng nhân sự phê duyệt thực tế (hỗ trợ duyệt một phần Partial Approval).
    /// Nếu để trống hoặc bằng 0 khi IsApproved = true, hệ thống sẽ duyệt toàn bộ (ApprovedQuantity = TotalRequested).
    /// </summary>
    [Range(0, 50, ErrorMessage = "Số lượng nhân sự phê duyệt không hợp lệ.")]
    public int? ApprovedQuantity { get; set; }

    /// <summary>
    /// Số ngày hiệu lực của đơn (mặc định 30 ngày nếu không chỉ định).
    /// </summary>
    [Range(1, 180, ErrorMessage = "Thời hạn hiệu lực phải từ 1 đến 180 ngày.")]
    public int? ExpirationDays { get; set; }

    /// <summary>
    /// Ghi chú thẩm định từ Admin hoặc lý do từ chối (bắt buộc khi từ chối).
    /// </summary>
    public string? AdminNotes { get; set; }
}

/// <summary>
/// DTO đóng hoặc hết hạn thủ công đơn mở rộng định biên.
/// </summary>
public class CloseHeadcountRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập lý do đóng đơn.")]
    public string Reason { get; set; } = string.Empty;
}
