using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

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
    public string? CheckInPhotoKey { get; set; }
    public string? CheckOutPhotoKey { get; set; }

    /// <summary>
    /// Trạng thái hợp nhất: PENDING (1), PRESENT (2), LATE (3), COMPLETED (4), COMPLETED_LATE (5).
    /// </summary>
    public AttendanceLogStatus Status { get; set; } = AttendanceLogStatus.PENDING;
    
    [NotMapped]
    public double? ActualWorkMinutes => CheckOutTime.HasValue ? (CheckOutTime.Value - CheckInTime).TotalMinutes : null;

    [NotMapped]
    public bool IsLate => Status == AttendanceLogStatus.LATE || Status == AttendanceLogStatus.COMPLETED_LATE;
    public bool IsFraudFlagged { get; set; } = false;
    public ulong? FraudFlaggedBy { get; set; }
    public User? FraudFlaggedByUser { get; set; }
    public string? FraudReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OvertimeRequest> OvertimeRequests { get; set; } = new List<OvertimeRequest>();
}
