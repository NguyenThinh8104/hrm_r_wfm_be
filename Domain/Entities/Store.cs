using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Store
{
    public int StoreId { get; set; }

    public string StoreCode { get; set; } = null!;

    public string StoreName { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<KioskActivationCode> KioskActivationCodes { get; set; } = new List<KioskActivationCode>();

    public virtual ICollection<KioskDevice> KioskDevices { get; set; } = new List<KioskDevice>();

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<ShiftHandoverSession> ShiftHandoverSessions { get; set; } = new List<ShiftHandoverSession>();

    public virtual ICollection<TemporaryDispatch> TemporaryDispatchFromStores { get; set; } = new List<TemporaryDispatch>();

    public virtual ICollection<TemporaryDispatch> TemporaryDispatchToStores { get; set; } = new List<TemporaryDispatch>();

    public virtual ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}

