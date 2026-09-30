using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Nhật ký ghi nhận lịch sử Khóa / Mở khóa của chi nhánh cửa hàng.
/// </summary>
public class BranchLockLog
{
    public ulong Id { get; set; }

    /// <summary>
    /// ID chi nhánh bị tác động.
    /// </summary>
    public ulong BranchId { get; set; }

    /// <summary>
    /// Đối tượng chi nhánh liên kết.
    /// </summary>
    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = null!;

    /// <summary>
    /// Hành động thực hiện: "Lock" hoặc "Unlock".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Lý do thực hiện khóa hoặc mở khóa.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Mã hoặc tên người thực hiện hành động.
    /// </summary>
    public string? PerformedBy { get; set; }

    /// <summary>
    /// Thời điểm thực hiện hành động.
    /// </summary>
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Số lượng nhân sự bị ảnh hưởng tại thời điểm khóa.
    /// </summary>
    public int AffectedEmployeeCount { get; set; } = 0;

    /// <summary>
    /// Chế độ xử lý nhân sự khi khóa: "KeepAndBlock" | "TransferTemporarily" | null (khi unlock).
    /// </summary>
    public string? StaffHandlingMode { get; set; }

    /// <summary>
    /// Chế độ xử lý ca làm việc tương lai: "Cancel" | "Transfer" | "Suspend" | null.
    /// </summary>
    public string? FutureShiftHandling { get; set; }

    /// <summary>
    /// ID chi nhánh đích nhận nhân sự nếu chọn TransferTemporarily.
    /// </summary>
    public ulong? TransferredToBranchId { get; set; }

    /// <summary>
    /// Đối tượng chi nhánh đích liên kết.
    /// </summary>
    [ForeignKey("TransferredToBranchId")]
    public Branch? TransferredToBranch { get; set; }
}
