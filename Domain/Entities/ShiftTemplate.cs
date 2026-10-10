using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

public class ShiftTemplate
{
    public uint Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string Scope { get; set; } = "GLOBAL"; // "GLOBAL" | "BRANCH"
    public ulong? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? ShiftType { get; set; } // "SANG" | "CHIEU" | "DEM" | "KHAC"

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsOvernight { get; set; } = false;
    public uint BreakDurationMinutes { get; set; } = 0;
    
    [NotMapped]
    public uint BreakDuration
    {
        get => BreakDurationMinutes;
        set => BreakDurationMinutes = value;
    }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Status
    {
        get => IsActive ? "ACTIVE" : "INACTIVE";
        set => IsActive = (value == "ACTIVE" || value == "1" || value == "true");
    }

    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}
