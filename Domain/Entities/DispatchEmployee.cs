namespace Domain.Entities;

public class DispatchEmployee
{
    public ulong Id { get; set; }
    public ulong DispatchId { get; set; }
    public TemporaryDispatch Dispatch { get; set; } = null!;
    public ulong UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "PENDING";
    public ulong? ApprovedBy { get; set; }
    public User? ApprovedByUser { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
