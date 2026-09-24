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

    /// <summary>
    /// Trạng thái chi tiết lúc Check-in: PENDING (1), ON_TIME (2), LATE (3), EARLY (4).
    /// </summary>
    public CheckInStatus CheckInStatus { get; set; } = CheckInStatus.PENDING;

    /// <summary>
    /// Trạng thái chi tiết lúc Check-out: PENDING (1), ON_TIME (2), EARLY_LEAVE (3), LATE_LEAVE (4).
    /// </summary>
    public CheckOutStatus? CheckOutStatus { get; set; }

    /// <summary>
    /// Số phút đi muộn (nếu CheckInStatus == LATE).
    /// </summary>
    public int? LateMinutes { get; set; }

    /// <summary>
    /// Số phút về sớm (nếu CheckOutStatus == EARLY_LEAVE).
    /// </summary>
    public int? EarlyLeaveMinutes { get; set; }
    
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
