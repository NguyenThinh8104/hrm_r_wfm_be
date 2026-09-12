using System;

namespace Domain.Entities;

public partial class KioskActivationCode
{
    public int ActivationCodeId { get; set; }

    public int StoreId { get; set; }

    public string KioskName { get; set; } = null!;

    public string Code { get; set; } = null!;

    public int GeneratedBy { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int? CreatedKioskId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Store Store { get; set; } = null!;

    public virtual User GeneratedByNavigation { get; set; } = null!;

    public virtual KioskDevice? CreatedKiosk { get; set; }
}

