namespace Domain.Entities;

public class SecurityHandover
{
    public ulong Id { get; set; }
    public ulong ShiftHandoverId { get; set; }
    public ShiftHandover ShiftHandover { get; set; } = null!;
    public ulong SecurityGuardId { get; set; }
    public User SecurityGuard { get; set; } = null!;
    public ushort OvernightVehicleCount { get; set; } = 0;
    public bool IsWarehouseLocked { get; set; } = true;
    public bool IsShutterClosed { get; set; } = true;
    public string? SecurityNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
