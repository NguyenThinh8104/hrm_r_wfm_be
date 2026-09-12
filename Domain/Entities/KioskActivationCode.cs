namespace Domain.Entities;

public class KioskActivationCode
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string KioskName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ulong GeneratedBy { get; set; }
    public User GeneratedByUser { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public ulong? CreatedKioskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
