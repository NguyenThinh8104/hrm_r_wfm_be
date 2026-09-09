using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class AttendanceRecord
{
    public int AttendanceId { get; set; }

    public int AssignmentId { get; set; }

    public int EmployeeId { get; set; }

    public int StoreId { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public string? CheckInMethod { get; set; }

    public string? CheckOutMethod { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ShiftAssignment Assignment { get; set; } = null!;

    public virtual ICollection<AttendanceException> AttendanceExceptions { get; set; } = new List<AttendanceException>();

    public virtual Employee Employee { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;
}
