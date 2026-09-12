using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Shift
{
    public int ShiftId { get; set; }

    public string ShiftCode { get; set; } = null!;

    public string ShiftName { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsOvernight { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();
}

