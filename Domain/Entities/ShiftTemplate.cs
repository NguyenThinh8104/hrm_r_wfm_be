namespace Domain.Entities;

public class ShiftTemplate
{
    public uint Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; } = false;
    public uint BreakDurationMinutes { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}
