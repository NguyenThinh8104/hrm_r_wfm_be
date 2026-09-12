namespace Modules.Handovers.DTOs;

public class CashierHandoverSubmitDto
{
    public int HandoverId { get; set; }
    public decimal ActualCash { get; set; }
    public string? DifferenceNote { get; set; }
}

public class SecurityHandoverSubmitDto
{
    public int HandoverId { get; set; }
    public int ParkingTickets { get; set; }
    public int ParkingCards { get; set; }
    public bool WarehouseLocked { get; set; }
    public bool RollerDoorLocked { get; set; }
    public string? SecurityNote { get; set; }
}

public class LeaderSignHandoverDto
{
    public int HandoverId { get; set; }
    public bool IsApproved { get; set; }
    public string? ManagerNote { get; set; }
}

public class ShiftHandoverSessionDto
{
    public int HandoverId { get; set; }
    public int AssignmentId { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public DateOnly ShiftDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OpenedByName { get; set; } = string.Empty;
    public string? ClosedByName { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ManagerNote { get; set; }

    public CashHandoverItemDto? CashierHandover { get; set; }
    public SecurityHandoverItemDto? SecurityHandover { get; set; }
}

public class CashHandoverItemDto
{
    public int CashHandoverId { get; set; }
    public int CashierEmployeeId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public string? DifferenceNote { get; set; }
}

public class SecurityHandoverItemDto
{
    public int SecurityHandoverId { get; set; }
    public int SecurityEmployeeId { get; set; }
    public string SecurityName { get; set; } = string.Empty;
    public int ParkingTickets { get; set; }
    public int ParkingCards { get; set; }
    public bool WarehouseLocked { get; set; }
    public bool RollerDoorLocked { get; set; }
    public string? SecurityNote { get; set; }
}

