using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

public class KioskDevice
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    [Column("Name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("KioskCode")]
    public string KioskCode { get; set; } = string.Empty;
    
    [Column("IpAddress")]
    public string? IpAddress { get; set; }

    [Column("DeviceToken")]
    public string DeviceToken { get; set; } = string.Empty;

    public string Status { get; set; } = "ACTIVE"; // "ACTIVE", "BLOCKED", "INACTIVE"
    public DateTime? LastPingAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
