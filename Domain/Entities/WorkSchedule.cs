namespace Domain.Entities;

public class WorkSchedule
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public uint ShiftTemplateId { get; set; }
    public ShiftTemplate ShiftTemplate { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public byte RequiredCashier { get; set; } = 1;
    public byte RequiredSales { get; set; } = 1;
    public byte RequiredSecurity { get; set; } = 1;
    public string Status { get; set; } = "DRAFT";
    public ulong CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
    public ShiftHandover? ShiftHandover { get; set; }
}
