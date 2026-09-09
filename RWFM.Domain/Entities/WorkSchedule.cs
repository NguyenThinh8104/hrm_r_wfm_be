using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class WorkSchedule
{
    public int ScheduleId { get; set; }

    public int StoreId { get; set; }

    public DateOnly WeekStartDate { get; set; }

    public DateOnly WeekEndDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public int? PublishedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee? PublishedByNavigation { get; set; }

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual Store Store { get; set; } = null!;
}
