namespace Domain.Entities;

public class MonthlyTimesheet
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public byte PeriodMonth { get; set; }
    public ushort PeriodYear { get; set; }
    public string Status { get; set; } = "OPEN";
    public ulong? LockedBy { get; set; }
    public User? LockedByUser { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? ExportedAt { get; set; }
}
