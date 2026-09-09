using System;
using System.Collections.Generic;

namespace RWFM.Domain.Entities;

public partial class Position
{
    public int PositionId { get; set; }

    public string PositionCode { get; set; } = null!;

    public string PositionName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
