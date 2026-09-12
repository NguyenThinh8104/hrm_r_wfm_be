namespace Domain.Entities;

public class ShiftSwapRequest
{
    public ulong Id { get; set; }
    public ulong RequestingAssignmentId { get; set; }
    public ShiftAssignment RequestingAssignment { get; set; } = null!;
    public ulong TargetAssignmentId { get; set; }
    public ShiftAssignment TargetAssignment { get; set; } = null!;
    public string? Reason { get; set; }
    public string Status { get; set; } = "PENDING";
    public ulong? ReviewedBy { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
