using System.ComponentModel.DataAnnotations;
using Domain.Enums;

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
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int GeofenceRadiusMeters { get; set; } = 50;
    /// <summary>
    /// Phân cấp chi nhánh (1 = Tier1: Cấp 1 - Lớn, 2 = Tier2: Cấp 2 - Tiêu chuẩn, 3 = Tier3: Cấp 3 - Nhỏ).
    /// </summary>
    public BranchTier BranchTier { get; set; } = BranchTier.Tier2;

    /// <summary>
    /// Tên chuỗi hiển thị tương ứng của phân cấp (ví dụ: "Cấp 1 - Lớn", "Cấp 2 - Tiêu chuẩn", "Cấp 3 - Nhỏ").
    /// </summary>
    public string BranchTierName => BranchTier switch
    {
        BranchTier.Tier1 => "Cấp 1 - Lớn",
        BranchTier.Tier2 => "Cấp 2 - Tiêu chuẩn",
        BranchTier.Tier3 => "Cấp 3 - Nhỏ",
        _ => "Không xác định"
    };
    public string? KioskAllowedIp { get; set; }
    public string? KioskAllowedBrowser { get; set; }
    public string Status { get; set; } = "ACTIVE"; // "ACTIVE" / "INACTIVE"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int TotalKiosks { get; set; }
    public int ActiveKiosks { get; set; }
    public List<KioskDto> Kiosks { get; set; } = new();

    public int StaffCount { get; set; } = 0;
    public int? TierId { get; set; }
    public TierDto? Tier { get; set; }

    // Backward compatibility aliases
    public int StoreId => (int)Id;
    public string StoreCode => Code;
    public string BranchCode => Code;
    public string StoreName => Name;
    public string BranchName => Name;

    /// <summary>
    /// Alias phân cấp số nguyên (1, 2, 3) tương thích ngược với Frontend.
    /// </summary>
    public int BranchTierNumber => (int)BranchTier;

    public int KioskCount => TotalKiosks;
    public string? AllowedIp => KioskAllowedIp;
    public string? AllowedBrowser => KioskAllowedBrowser;
    public int RadiusMeters => GeofenceRadiusMeters;
    public int Radius => GeofenceRadiusMeters;
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
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? GeofenceRadiusMeters { get; set; }
    /// <summary>
    /// Phân cấp chi nhánh (Bắt buộc): 1 = Tier 1 (Cấp 1 - Lớn), 2 = Tier 2 (Cấp 2 - Tiêu chuẩn), 3 = Tier 3 (Cấp 3 - Nhỏ).
    /// </summary>
    [Required(ErrorMessage = "Phân cấp chi nhánh (BranchTier) là bắt buộc.")]
    [EnumDataType(typeof(BranchTier), ErrorMessage = "Phân cấp chi nhánh không hợp lệ. Chỉ chấp nhận 1 (Tier1), 2 (Tier2), 3 (Tier3).")]
    public BranchTier BranchTier { get; set; } = BranchTier.Tier2;
    public string? KioskAllowedIp { get; set; }
    public string? KioskAllowedBrowser { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public int? StaffCount { get; set; }
    public int? TierId { get; set; }

    // Aliases
    public string? BranchCode { get => Code; set => Code = value ?? string.Empty; }
    public string? StoreCode { get => Code; set => Code = value ?? string.Empty; }
    public string? StoreName { get => Name; set => Name = value ?? string.Empty; }
    public string? BranchName { get => Name; set => Name = value ?? string.Empty; }
    /// <summary>
    /// Alias nhận diện giá trị phân cấp nếu client gửi qua trường "Tier".
    /// </summary>
    public BranchTier? Tier { get => BranchTier; set { if (value.HasValue) BranchTier = value.Value; } }

    public string? AllowedIp { get => KioskAllowedIp; set => KioskAllowedIp = value; }
    public string? AllowedBrowser { get => KioskAllowedBrowser; set => KioskAllowedBrowser = value; }
    public int? RadiusMeters { get => GeofenceRadiusMeters; set => GeofenceRadiusMeters = value; }
    public int? Radius { get => GeofenceRadiusMeters; set => GeofenceRadiusMeters = value; }
}

/// <summary>
/// DTO cập nhật thông tin chi nhánh.
/// </summary>
public class UpdateBranchDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? GeofenceRadiusMeters { get; set; }
    public int? StaffCount { get; set; }
    public int? TierId { get; set; }
    /// <summary>
    /// Phân cấp chi nhánh mới (Tùy chọn): 1 = Tier 1 (Lớn), 2 = Tier 2 (Tiêu chuẩn), 3 = Tier 3 (Nhỏ).
    /// </summary>
    [EnumDataType(typeof(BranchTier), ErrorMessage = "Phân cấp chi nhánh không hợp lệ. Chỉ chấp nhận 1 (Tier1), 2 (Tier2), 3 (Tier3).")]
    public BranchTier? BranchTier { get; set; }
    public string? KioskAllowedIp { get; set; }
    public string? KioskAllowedBrowser { get; set; }

    // Aliases
    public string? StoreName { get => Name; set => Name = value ?? string.Empty; }
    public string? BranchName { get => Name; set => Name = value ?? string.Empty; }

    /// <summary>
    /// Alias nhận diện giá trị phân cấp nếu client gửi qua trường "Tier".
    /// </summary>
    public BranchTier? Tier { get => BranchTier; set => BranchTier = value; }

    public string? AllowedIp { get => KioskAllowedIp; set => KioskAllowedIp = value; }
    public string? AllowedBrowser { get => KioskAllowedBrowser; set => KioskAllowedBrowser = value; }
    public int? RadiusMeters { get => GeofenceRadiusMeters; set => GeofenceRadiusMeters = value; }
    public int? Radius { get => GeofenceRadiusMeters; set => GeofenceRadiusMeters = value; }
}

/// <summary>
/// DTO thống kê số lượng chi nhánh theo từng cấp quy mô (Branch Tier Summary).
/// Định dạng JSON: { "tier1Count": int, "tier2Count": int, "tier3Count": int, "totalCount": int }.
/// </summary>
public class BranchTierSummaryDto
{
    /// <summary>
    /// Số lượng chi nhánh Cấp 1 - Quy mô lớn (Tier 1).
    /// </summary>
    public int Tier1Count { get; set; }

    /// <summary>
    /// Số lượng chi nhánh Cấp 2 - Quy mô tiêu chuẩn (Tier 2).
    /// </summary>
    public int Tier2Count { get; set; }

    /// <summary>
    /// Số lượng chi nhánh Cấp 3 - Quy mô nhỏ (Tier 3).
    /// </summary>
    public int Tier3Count { get; set; }

    /// <summary>
    /// Tổng số chi nhánh đang có trong toàn hệ thống.
    /// </summary>
    public int TotalCount => Tier1Count + Tier2Count + Tier3Count;
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
    public DateTime? UpdatedAt { get; set; }

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
