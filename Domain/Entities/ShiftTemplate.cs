using Domain.Enums;

namespace Domain.Entities;

public class ShiftTemplate
{
    public uint Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ShiftType ShiftType { get; set; } = ShiftType.Morning;
    public string? Description { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; } = false;
    public uint BreakDurationMinutes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}
