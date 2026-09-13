using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

public class KioskDevice
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    [Column("Name")]
    public string Name { get; set; } = string.Empty;
    
    [NotMapped]
    public string DeviceName { get => Name; set => Name = value; }
    
    public string KioskCode { get; set; } = string.Empty;
    
    [Column("AllowedIp")]
    public string? AllowedIp { get; set; }
    
    [NotMapped]
    public string? IpWhitelist { get => AllowedIp; set => AllowedIp = value; }
    
    [NotMapped]
    public string? IpAddress { get => AllowedIp; set => AllowedIp = value; }

    [Column("DeviceToken")]
    public string DeviceToken { get; set; } = string.Empty;
    
    [NotMapped]
    public string KioskToken { get => DeviceToken; set => DeviceToken = value; }

    [Column("AllowedBrowser")]
    public string? AllowedBrowser { get; set; }
    
    [NotMapped]
    public string? UserAgentPattern { get => AllowedBrowser; set => AllowedBrowser = value; }
    
    [NotMapped]
    public string? LastBrowserUserAgent { get => AllowedBrowser; set => AllowedBrowser = value; }

    public string Status { get; set; } = "ACTIVE"; // "ACTIVE", "BLOCKED", "INACTIVE"
    public DateTime? LastPingAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
