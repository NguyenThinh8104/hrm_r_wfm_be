using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Thực thể đơn đề xuất mở rộng / import định biên nhân sự chi nhánh (Headcount Import Request).
/// </summary>
public class HeadcountImportRequest
{
    public ulong Id { get; set; }

    /// <summary>
    /// ID chi nhánh xin mở rộng định biên (Foreign Key -> branches.id).
    /// </summary>
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    /// <summary>
    /// ID Store Manager gửi đề xuất (Foreign Key -> users.id).
    /// </summary>
    public ulong RequestedBy { get; set; }
    public User RequestedByUser { get; set; } = null!;

    /// <summary>
    /// Đường dẫn file Excel (.xlsx) lưu trữ trên hệ thống (S3 / Local Storage).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Tên gốc của tệp tin upload.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái đơn: PENDING, APPROVED, REJECTED, EXHAUSTED, EXPIRED, CLOSED.
    /// </summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Tổng số lượng nhân sự Store Manager đề xuất bổ sung.
    /// </summary>
    public int TotalRequested { get; set; }

    /// <summary>
    /// Số lượng nhân sự được Operations Admin phê duyệt (hỗ trợ duyệt một phần Partial Approval).
    /// </summary>
    public int ApprovedQuantity { get; set; }

    /// <summary>
    /// Số lượng nhân sự đã sử dụng thực tế (đã tạo thành công tài khoản).
    /// </summary>
    public int TotalApproved { get; set; } = 0;

    /// <summary>
    /// Số lượng chỉ tiêu còn lại khả dụng (trừ lùi khi tạo NV: AdditionalQuantity = ApprovedQuantity - TotalApproved).
    /// </summary>
    public int AdditionalQuantity { get; set; }

    /// <summary>
    /// Lý do giải trình từ Store Manager khi nộp file Excel.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú thẩm định, phê duyệt hoặc lý do từ chối từ Operations Admin.
    /// </summary>
    public string? AdminNotes { get; set; }

    /// <summary>
    /// ID Operations Admin thực hiện thẩm định (Foreign Key -> users.id).
    /// </summary>
    public ulong? ReviewedBy { get; set; }
    public User? ReviewedByUser { get; set; }

    /// <summary>
    /// Thời điểm Admin phê duyệt hoặc từ chối đơn.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Thời hạn hiệu lực của đơn. Quá thời gian này đơn tự động hết hạn (EXPIRED).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
