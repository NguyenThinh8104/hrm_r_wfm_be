using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Entity đại diện cho bảng 'branch_tiers' (Cấp bậc / Hạng chi nhánh dựa trên tiêu chí quy mô nhân sự, doanh thu, KPI...).
/// </summary>
[Table("branch_tiers")]
public class BranchTierEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string TierName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MinStaffCount { get; set; } = 0;

    public int? MaxStaffCount { get; set; }

    /// <summary>
    /// Các tiêu chí khác mở rộng (JSON: doanh thu tối thiểu, diện tích sàn, KPI...)
    /// </summary>
    public string? OtherConditions { get; set; }

    /// <summary>
    /// Điều kiện hiển thị dạng text
    /// </summary>
    public string? Conditions { get; set; }

    /// <summary>
    /// Quyền lợi và chính sách đi kèm tier (hạn mức Kiosk, ngân sách đào tạo, overtime...)
    /// </summary>
    public string? Benefits { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property: Danh sách các branch thuộc tier này
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
