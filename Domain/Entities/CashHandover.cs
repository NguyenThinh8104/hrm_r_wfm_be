using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class CashHandover
{
    public int CashHandoverId { get; set; }

    public int HandoverId { get; set; }

    public int CashierEmployeeId { get; set; }

    public decimal OpeningFloat { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? DifferenceAmount { get; set; }

    public string? DifferenceNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee CashierEmployee { get; set; } = null!;

    public virtual ShiftHandoverSession Handover { get; set; } = null!;
}

