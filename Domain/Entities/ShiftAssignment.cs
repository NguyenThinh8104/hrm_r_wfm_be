namespace Domain.Entities;

public class ShiftAssignment
{
    public ulong Id { get; set; }
    public ulong ScheduleId { get; set; }
    public WorkSchedule Schedule { get; set; } = null!;
    public ulong UserId { get; set; }
    public User User { get; set; } = null!;
    public byte AssignedRoleId { get; set; }
    public Role AssignedRole { get; set; } = null!;
    public string AssignmentType { get; set; } = "ASSIGNED";
    public string Status { get; set; } = "CONFIRMED";

    public AttendanceLog? AttendanceLog { get; set; }
}
