using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int EmployeeId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
