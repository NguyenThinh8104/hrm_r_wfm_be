using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ShiftAssignment
{
    public int AssignmentId { get; set; }

    public int ScheduleId { get; set; }

    public int EmployeeId { get; set; }

    public int ShiftId { get; set; }

    public DateOnly WorkDate { get; set; }

    public int StoreId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual Employee Employee { get; set; } = null!;

    public virtual WorkSchedule Schedule { get; set; } = null!;

    public virtual Shift Shift { get; set; } = null!;

    public virtual ICollection<ShiftHandoverSession> ShiftHandoverSessions { get; set; } = new List<ShiftHandoverSession>();

    public virtual ICollection<ShiftSwapRequest> ShiftSwapRequests { get; set; } = new List<ShiftSwapRequest>();

    public virtual Store Store { get; set; } = null!;
}

