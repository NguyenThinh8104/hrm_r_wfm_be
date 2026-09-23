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

    public int GeofenceRadiusMeters { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<KioskDevice> Kiosks { get; set; } = new List<KioskDevice>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    public ICollection<HeadcountImportRequest> HeadcountImportRequests { get; set; } = new List<HeadcountImportRequest>();
}
