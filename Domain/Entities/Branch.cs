using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace Domain.Entities;

public class Branch
{
    public ulong Id { get; set; }

    [Column("BranchCode")]
    public string BranchCode { get; set; } = string.Empty;

    [NotMapped]
    public string Code { get => BranchCode; set => BranchCode = value; }

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" or "INACTIVE"
    
    [NotMapped]
    public string? Phone { get; set; }

    [NotMapped]
    public string? KioskAllowedIp { get; set; }

    [NotMapped]
    public string? KioskAllowedBrowser { get; set; }

    /// <summary>
    /// Phân cấp quy mô chi nhánh (Tier 1: Lớn, Tier 2: Tiêu chuẩn, Tier 3: Nhỏ). Mặc định là Tier 2 (Tiêu chuẩn).
    /// </summary>
    public Domain.Enums.BranchTier BranchTier { get; set; } = Domain.Enums.BranchTier.Tier2;

    /// <summary>
    /// Khóa ngoại liên kết tới bảng branch_tiers.
    /// </summary>
    public int? TierId { get; set; }

    /// <summary>
    /// Đối tượng Tier liên kết.
    /// </summary>
    [ForeignKey("TierId")]
    public BranchTierEntity? Tier { get; set; }

    [Column("Location", TypeName = "POINT")]
    public Point? Location { get; set; }

    [NotMapped]
    public double? Latitude
    {
        get => Location?.Y;
        set
        {
            if (value.HasValue)
            {
                double lng = Location?.X ?? 105.7833;
                Location = new Point(lng, value.Value) { SRID = 4326 };
            }
        }
    }

    [NotMapped]
    public double? Longitude
    {
        get => Location?.X;
        set
        {
            if (value.HasValue)
            {
                double lat = Location?.Y ?? 21.0333;
                Location = new Point(value.Value, lat) { SRID = 4326 };
            }
        }
    }

    /// <summary>
    /// Số lượng nhân sự định biên tùy chỉnh (Custom Quota). 
    /// Nếu > 0 sẽ ưu tiên dùng StaffCount làm Effective Quota; nếu <= 0 sẽ dùng định biên chuẩn theo BranchTier.
    /// </summary>
    public int StaffCount { get; set; } = 0;

    public int GeofenceRadiusMeters { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm chi nhánh bị khóa.
    /// </summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>
    /// Định danh / Tên người thực hiện khóa chi nhánh.
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// Lý do thực hiện khóa chi nhánh.
    /// </summary>
    public string? LockReason { get; set; }

    /// <summary>
    /// Thời điểm chi nhánh được mở khóa trở lại.
    /// </summary>
    public DateTime? UnlockedAt { get; set; }

    /// <summary>
    /// Định danh / Tên người thực hiện mở khóa chi nhánh.
    /// </summary>
    public string? UnlockedBy { get; set; }

    /// <summary>
    /// Token kiểm soát xung đột dữ liệu (Concurrency Token chống Race Condition).
    /// </summary>
    [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Chế độ áp dụng khung ca của chi nhánh: 'GLOBAL' (dùng ca chung) hoặc 'CUSTOM' (dùng ca riêng). Mặc định là 'GLOBAL'.
    /// </summary>
    public string ShiftMode { get; set; } = "GLOBAL"; // "GLOBAL" | "CUSTOM"

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<KioskDevice> Kiosks { get; set; } = new List<KioskDevice>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<BranchLockLog> LockLogs { get; set; } = new List<BranchLockLog>();
    public ICollection<ShiftTemplate> ShiftTemplates { get; set; } = new List<ShiftTemplate>();
}
