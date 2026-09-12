namespace Domain.Entities;

public class OvertimeRequest
{
    public ulong Id { get; set; }
    public ulong AttendanceLogId { get; set; }
    public AttendanceLog AttendanceLog { get; set; } = null!;
    public string OtType { get; set; } = "POST_SHIFT_EXTENSION";
    public uint RequestedMinutes { get; set; }
    public uint ApprovedMinutes { get; set; } = 0;
    public string Status { get; set; } = "PENDING";
    public ulong? VerifiedByLeader { get; set; }
    public User? VerifiedByLeaderUser { get; set; }
    public ulong? ApprovedByManager { get; set; }
    public User? ApprovedByManagerUser { get; set; }
    public string? ManagerNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
