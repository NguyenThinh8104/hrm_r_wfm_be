using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

public class ShiftTemplate
{
    public uint Id { get; set; }
    public string Code { get; set; } = string.Empty;
    
    [NotMapped]
    public string TemplateCode { get => Code; set => Code = value; }
    
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public uint BreakMinutes { get; set; } = 0;
    
    [NotMapped]
    public uint BreakDurationMinutes { get => BreakMinutes; set => BreakMinutes = value; }
    
    public bool IsOvernight { get; set; } = false;
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" or "INACTIVE"
    
    [NotMapped]
    public bool IsActive { get => Status == "ACTIVE"; set => Status = value ? "ACTIVE" : "INACTIVE"; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}
