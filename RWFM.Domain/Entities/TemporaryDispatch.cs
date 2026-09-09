using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class TemporaryDispatch
{
    public int DispatchId { get; set; }

    public int EmployeeId { get; set; }

    public int FromStoreId { get; set; }

    public int ToStoreId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public int RequestedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee? ApprovedByNavigation { get; set; }

    public virtual Employee Employee { get; set; } = null!;

    public virtual Store FromStore { get; set; } = null!;

    public virtual Employee RequestedByNavigation { get; set; } = null!;

    public virtual Store ToStore { get; set; } = null!;
}
