namespace Domain.Entities;

public class ShiftHandover
{
    public ulong Id { get; set; }
    public ulong ScheduleId { get; set; }
    public WorkSchedule Schedule { get; set; } = null!;
    public ulong ShiftLeaderId { get; set; }
    public User ShiftLeader { get; set; } = null!;
    public string HandoverStatus { get; set; } = "IN_PROGRESS";
    public DateTime? SignedAt { get; set; }
    public string? GeneralNotes { get; set; }

    public ICollection<CashHandover> CashHandovers { get; set; } = new List<CashHandover>();
    public ICollection<SecurityHandover> SecurityHandovers { get; set; } = new List<SecurityHandover>();
}
