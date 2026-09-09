using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class ShiftHandoverSession
{
    public int HandoverId { get; set; }

    public int AssignmentId { get; set; }

    public int StoreId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public string Status { get; set; } = null!;

    public int OpenedBy { get; set; }

    public int? ClosedBy { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? ManagerNote { get; set; }

    public virtual ShiftAssignment Assignment { get; set; } = null!;

    public virtual ICollection<CashHandover> CashHandovers { get; set; } = new List<CashHandover>();

    public virtual Employee? ClosedByNavigation { get; set; }

    public virtual Employee OpenedByNavigation { get; set; } = null!;

    public virtual ICollection<SecurityHandover> SecurityHandovers { get; set; } = new List<SecurityHandover>();

    public virtual Store Store { get; set; } = null!;
}
