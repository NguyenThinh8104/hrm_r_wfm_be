using System.ComponentModel.DataAnnotations.Schema;

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
    public string? KioskAllowedIp { get; set; }

    [NotMapped]
    public string? Phone { get; set; }

    [NotMapped]
    public string? KioskAllowedBrowser { get; set; }

    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" or "INACTIVE"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<KioskDevice> Kiosks { get; set; } = new List<KioskDevice>();
    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}
