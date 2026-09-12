using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public int? UserId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly HireDate { get; set; }

    public int PositionId { get; set; }

    public int PrimaryStoreId { get; set; }

    public string? PinHash { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AttendanceException> AttendanceExceptionReportedByNavigations { get; set; } = new List<AttendanceException>();

    public virtual ICollection<AttendanceException> AttendanceExceptionResolvedByNavigations { get; set; } = new List<AttendanceException>();

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<CashHandover> CashHandovers { get; set; } = new List<CashHandover>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual Position Position { get; set; } = null!;

    public virtual Store PrimaryStore { get; set; } = null!;

    public virtual ICollection<SecurityHandover> SecurityHandovers { get; set; } = new List<SecurityHandover>();

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual ICollection<ShiftHandoverSession> ShiftHandoverSessionClosedByNavigations { get; set; } = new List<ShiftHandoverSession>();

    public virtual ICollection<ShiftHandoverSession> ShiftHandoverSessionOpenedByNavigations { get; set; } = new List<ShiftHandoverSession>();

    public virtual ICollection<ShiftSwapRequest> ShiftSwapRequestRequesterEmployees { get; set; } = new List<ShiftSwapRequest>();

    public virtual ICollection<ShiftSwapRequest> ShiftSwapRequestReviewedByNavigations { get; set; } = new List<ShiftSwapRequest>();

    public virtual ICollection<ShiftSwapRequest> ShiftSwapRequestTargetEmployees { get; set; } = new List<ShiftSwapRequest>();

    public virtual ICollection<TemporaryDispatch> TemporaryDispatchApprovedByNavigations { get; set; } = new List<TemporaryDispatch>();

    public virtual ICollection<TemporaryDispatch> TemporaryDispatchEmployees { get; set; } = new List<TemporaryDispatch>();

    public virtual ICollection<TemporaryDispatch> TemporaryDispatchRequestedByNavigations { get; set; } = new List<TemporaryDispatch>();

    public virtual User? User { get; set; }

    public virtual ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
}

