namespace Modules.Stores.DTOs;

/// <summary>
/// DTO yêu cầu tạo mới chi nhánh cửa hàng (Operations Admin).
/// </summary>
public class CreateStoreDto
{
    /// <summary>
    /// Mã chi nhánh viết hoa duy nhất (Ví dụ: CH03, STORE_CG).
    /// </summary>
    public string BranchCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên chi nhánh (Ví dụ: Cửa hàng Tiện lợi Chi nhánh Quận 1).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ chi nhánh cửa hàng.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ IP tĩnh hoặc dải IP mạng cho phép của Kiosk quầy (Tùy chọn, ví dụ: "192.168.1.100" hoặc "127.0.0.1,::1").
    /// </summary>
    public string? KioskAllowedIp { get; set; }

    /// <summary>
    /// Tên trình duyệt hoặc User-Agent hợp lệ của Kiosk quầy (Tùy chọn, ví dụ: "Chrome", "Edge", "KioskBrowser").
    /// </summary>
    public string? KioskAllowedBrowser { get; set; }
}

/// <summary>
/// DTO cập nhật thông tin chi nhánh cửa hàng (Operations Admin).
/// </summary>
public class UpdateStoreDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? KioskAllowedIp { get; set; }
    public string? KioskAllowedBrowser { get; set; }
    public string? Status { get; set; } // "ACTIVE" hoặc "LOCKED"
}

/// <summary>
/// DTO thay đổi trạng thái hoạt động (Khóa / Mở khóa) của chi nhánh.
/// </summary>
public class UpdateStoreStatusDto
{
    /// <summary>
    /// Trạng thái mới: "ACTIVE" hoặc "LOCKED".
    /// </summary>
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Lý do thay đổi trạng thái (Ghi nhận Audit Log).
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// DTO chi tiết chi nhánh cửa hàng kèm cấu hình mạng Kiosk và danh sách thiết bị.
/// </summary>
public class StoreDetailDto
{
    public int StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string? KioskAllowedIp { get; set; }
    public string? KioskAllowedBrowser { get; set; }
    public int TotalKiosks { get; set; }
    public int ActiveKiosks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<KioskDetailDto> Kiosks { get; set; } = new();
}

/// <summary>
/// DTO cập nhật cấu hình mạng / trình duyệt trạm Kiosk tại quầy (Operations Admin / Store Manager).
/// </summary>
public class UpdateKioskConfigDto
{
    /// <summary>
    /// Tên hiển thị của trạm Kiosk.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Địa chỉ IP tĩnh hoặc dải IP được phép truy cập (Tùy chọn).
    /// </summary>
    public string? AllowedIp { get; set; }

    /// <summary>
    /// Tên trình duyệt hoặc chuỗi User-Agent được phép chạy ứng dụng Kiosk (Tùy chọn).
    /// </summary>
    public string? AllowedBrowser { get; set; }

    /// <summary>
    /// Trạng thái hoạt động: "ACTIVE" hoặc "LOCKED".
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// DTO thay đổi trạng thái (Khóa / Mở khóa) trạm Kiosk quầy.
/// </summary>
public class UpdateKioskStatusDto
{
    /// <summary>
    /// Trạng thái mới: "ACTIVE" hoặc "LOCKED".
    /// </summary>
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Lý do khóa khẩn cấp hoặc mở khóa trạm Kiosk.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// DTO chi tiết thiết bị Kiosk quầy, thông tin mạng và trạng thái kết nối.
/// </summary>
public class KioskDetailDto
{
    public int KioskId { get; set; }
    public int StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string KioskCode { get; set; } = string.Empty;
    public string KioskName { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string? AllowedIp { get; set; }
    public string? AllowedBrowser { get; set; }
    public string? IpAddress { get; set; }
    public string? LastBrowserUserAgent { get; set; }
    public DateTime? LastPingAt { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
