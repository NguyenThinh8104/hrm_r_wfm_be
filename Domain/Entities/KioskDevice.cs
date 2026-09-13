namespace Domain.Entities;

public class KioskDevice
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string KioskCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string? IpAddress { get; set; }
    public string? AllowedIp { get; set; }
    public string? AllowedBrowser { get; set; }
    public string? LastBrowserUserAgent { get; set; }
    public DateTime? LastPingAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}
