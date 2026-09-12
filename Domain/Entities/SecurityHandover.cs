using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class SecurityHandover
{
    public int SecurityHandoverId { get; set; }

    public int HandoverId { get; set; }

    public int SecurityEmployeeId { get; set; }

    public int ParkingTickets { get; set; }

    public int ParkingCards { get; set; }

    public bool WarehouseLocked { get; set; }

    public bool RollerDoorLocked { get; set; }

    public string? SecurityNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ShiftHandoverSession Handover { get; set; } = null!;

    public virtual Employee SecurityEmployee { get; set; } = null!;
}

