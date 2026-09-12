namespace Modules.Stores.DTOs;

public class CreateKioskCodeRequestDto
{
    public int StoreId { get; set; }
    public string KioskName { get; set; } = string.Empty;
}

public class KioskCodeResponseDto
{
    public int ActivationCodeId { get; set; }
    public int StoreId { get; set; }
    public string KioskName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}

public class ActivateKioskRequestDto
{
    public string Code { get; set; } = string.Empty;
}

public class KioskActivationResponseDto
{
    public int KioskId { get; set; }
    public int StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string KioskCode { get; set; } = string.Empty;
    public string KioskName { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime ActivatedAt { get; set; }
}

public class VerifyKioskTokenRequestDto
{
    public string DeviceToken { get; set; } = string.Empty;
}

