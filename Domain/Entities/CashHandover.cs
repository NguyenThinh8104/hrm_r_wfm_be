namespace Domain.Entities;

public class CashHandover
{
    public ulong Id { get; set; }
    public ulong ShiftHandoverId { get; set; }
    public ShiftHandover ShiftHandover { get; set; } = null!;
    public ulong CashierId { get; set; }
    public User Cashier { get; set; } = null!;
    public decimal OpeningCash { get; set; }
    public decimal SystemExpectedCash { get; set; }
    public decimal ClosingActualCash { get; set; }
    public decimal DifferenceAmount => ClosingActualCash - SystemExpectedCash;
    public string? DiscrepancyReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
