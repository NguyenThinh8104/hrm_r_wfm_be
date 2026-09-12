namespace Domain.Entities;

public class SystemAuditLog
{
    public ulong Id { get; set; }
    public ulong ActorId { get; set; }
    public User Actor { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public ulong TargetId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
