using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class KioskDevice
{
    public int KioskId { get; set; }

    public int StoreId { get; set; }

    public string KioskCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string DeviceToken { get; set; } = null!;

    public string Status { get; set; } = "Active";

    public string? IpAddress { get; set; }

    public DateTime? LastPingAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Store Store { get; set; } = null!;

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

