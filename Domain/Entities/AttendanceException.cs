using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class AttendanceException
{
    public int ExceptionId { get; set; }

    public int AttendanceId { get; set; }

    public int ReportedBy { get; set; }

    public string ExceptionType { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public int? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AttendanceRecord Attendance { get; set; } = null!;

    public virtual Employee ReportedByNavigation { get; set; } = null!;

    public virtual Employee? ResolvedByNavigation { get; set; }
}

