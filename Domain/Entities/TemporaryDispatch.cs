namespace Domain.Entities;

public class TemporaryDispatch
{
    public ulong Id { get; set; }
    public ulong SourceBranchId { get; set; }
    public Branch SourceBranch { get; set; } = null!;
    public ulong TargetBranchId { get; set; }
    public Branch TargetBranch { get; set; } = null!;
    public ulong UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ulong RequestedBy { get; set; }
    public User RequestedByUser { get; set; } = null!;
    public ulong? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
