namespace Domain.Entities;

public class KioskDevice
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    public string DeviceName { get; set; } = string.Empty;
    public string Name { get => DeviceName; set => DeviceName = value; }
    public string KioskCode { get; set; } = string.Empty;
    
    public string? IpWhitelist { get; set; }
    public string? AllowedIp { get => IpWhitelist; set => IpWhitelist = value; }
    public string? IpAddress { get => IpWhitelist; set => IpWhitelist = value; }

    public string KioskToken { get; set; } = string.Empty;
    public string DeviceToken { get => KioskToken; set => KioskToken = value; }

    public string? UserAgentPattern { get; set; }
    public string? AllowedBrowser { get => UserAgentPattern; set => UserAgentPattern = value; }
    public string? LastBrowserUserAgent { get => UserAgentPattern; set => UserAgentPattern = value; }

    public string Status { get; set; } = "ACTIVE"; // "ACTIVE", "BLOCKED", "INACTIVE"
    public DateTime? LastPingAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
