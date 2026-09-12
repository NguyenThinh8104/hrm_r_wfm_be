namespace Domain.Entities;

public class AttendanceLog
{
    public ulong Id { get; set; }
    public ulong AssignmentId { get; set; }
    public ShiftAssignment Assignment { get; set; } = null!;
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public ulong? KioskId { get; set; }
    public KioskDevice? Kiosk { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal? OpeningFloatCash { get; set; }
    public bool IsFraudFlagged { get; set; } = false;
    public ulong? FraudFlaggedBy { get; set; }
    public User? FraudFlaggedByUser { get; set; }
    public string? FraudReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();
}
