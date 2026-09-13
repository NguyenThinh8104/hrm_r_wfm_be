namespace Modules.Stores.DTOs;

// ==========================================
// 1. Branch DTOs (UC 1.2)
// ==========================================

/// <summary>
/// DTO trả về thông tin chi nhánh cửa hàng kèm danh sách Kiosk.
/// </summary>
public class BranchDto
{
    public ulong Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalKiosks { get; set; }
    public int ActiveKiosks { get; set; }
    public List<KioskDto> Kiosks { get; set; } = new();

    // Backward compatibility aliases
    public int StoreId => (int)Id;
    public string StoreCode => Code;
    public string BranchCode => Code;
    public string StoreName => Name;
    public string BranchName => Name;
    public int KioskCount => TotalKiosks;
}

/// <summary>
/// DTO yêu cầu tạo mới chi nhánh cửa hàng.
/// </summary>
public class CreateBranchDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Status { get; set; } = "ACTIVE";

    // Aliases
    public string? BranchCode { get => Code; set => Code = value ?? string.Empty; }
    public string? StoreCode { get => Code; set => Code = value ?? string.Empty; }
    public string? StoreName { get => Name; set => Name = value ?? string.Empty; }
    public string? BranchName { get => Name; set => Name = value ?? string.Empty; }
}

/// <summary>
/// DTO cập nhật thông tin chi nhánh.
/// </summary>
public class UpdateBranchDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }

    // Aliases
    public string? StoreName { get => Name; set => Name = value ?? string.Empty; }
    public string? BranchName { get => Name; set => Name = value ?? string.Empty; }
}

/// <summary>
/// DTO cập nhật trạng thái chi nhánh (ACTIVE / INACTIVE).
/// </summary>
public class UpdateBranchStatusDto
{
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
    public string? Reason { get; set; }
}

// ==========================================
// 2. Kiosk DTOs (UC 1.2)
// ==========================================

/// <summary>
/// DTO trả về thông tin chi tiết thiết bị Kiosk quầy.
/// </summary>
public class KioskDto
{
    public ulong Id { get; set; }
    public ulong BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string KioskCode { get; set; } = string.Empty;
    public string? IpWhitelist { get; set; }
    public string KioskToken { get; set; } = string.Empty;
    public string? UserAgentPattern { get; set; }
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "BLOCKED" / "INACTIVE"
    public DateTime? LastPingAt { get; set; }
    public bool IsOnline => LastPingAt.HasValue && LastPingAt.Value >= DateTime.UtcNow.AddMinutes(-5);
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Backward compatibility aliases
    public int KioskId => (int)Id;
    public int StoreId => (int)BranchId;
    public string StoreCode => BranchCode;
    public string StoreName => BranchName;
    public string Name => DeviceName;
    public string KioskName => DeviceName;
    public string DeviceToken => KioskToken;
    public string? AllowedIp => IpWhitelist;
    public string? AllowedBrowser => UserAgentPattern;
    public string? IpAddress => IpWhitelist;
    public string? LastBrowserUserAgent => UserAgentPattern;
}

/// <summary>
/// DTO tạo mới Kiosk cho một chi nhánh.
/// </summary>
public class CreateKioskDto
{
    public string DeviceName { get; set; } = string.Empty;
    public string? IpWhitelist { get; set; }
    public string? UserAgentPattern { get; set; }
    public string? KioskToken { get; set; }

    // Aliases
    public string? Name { get => DeviceName; set => DeviceName = value ?? string.Empty; }
    public string? KioskName { get => DeviceName; set => DeviceName = value ?? string.Empty; }
    public string? AllowedIp { get => IpWhitelist; set => IpWhitelist = value; }
    public string? AllowedBrowser { get => UserAgentPattern; set => UserAgentPattern = value; }
}

/// <summary>
/// DTO cập nhật thông tin / cấu hình Kiosk quầy.
/// </summary>
public class UpdateKioskDto
{
    public string DeviceName { get; set; } = string.Empty;
    public string? IpWhitelist { get; set; }
    public string? UserAgentPattern { get; set; }

    // Aliases
    public string? Name { get => DeviceName; set => DeviceName = value ?? string.Empty; }
    public string? KioskName { get => DeviceName; set => DeviceName = value ?? string.Empty; }
    public string? AllowedIp { get => IpWhitelist; set => IpWhitelist = value; }
    public string? AllowedBrowser { get => UserAgentPattern; set => UserAgentPattern = value; }
}

/// <summary>
/// DTO cập nhật trạng thái Kiosk quầy (ACTIVE / BLOCKED / INACTIVE).
/// </summary>
public class UpdateKioskStatusDto
{
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "BLOCKED" / "INACTIVE"
    public string? Reason { get; set; }
}

// ==========================================
// 3. Backward Compatibility DTO Classes
// ==========================================
public class StoreDetailDto : BranchDto { }
public class CreateStoreDto : CreateBranchDto { }
public class UpdateStoreDto : UpdateBranchDto { }
public class UpdateStoreStatusDto : UpdateBranchStatusDto { }
public class KioskDetailDto : KioskDto { }
public class UpdateKioskConfigDto : UpdateKioskDto { }
