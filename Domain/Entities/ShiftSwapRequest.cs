using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ShiftSwapRequest
{
    public int SwapRequestId { get; set; }

    public int AssignmentId { get; set; }

    public int RequesterEmployeeId { get; set; }

    public int TargetEmployeeId { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ShiftAssignment Assignment { get; set; } = null!;

    public virtual Employee RequesterEmployee { get; set; } = null!;

    public virtual Employee? ReviewedByNavigation { get; set; }

    public virtual Employee TargetEmployee { get; set; } = null!;
}

