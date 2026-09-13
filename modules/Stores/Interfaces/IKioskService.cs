using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý kích hoạt, xác thực và cấu hình mạng/trình duyệt trạm Kiosk tại cửa hàng (UC 1.2).
/// </summary>
public interface IKioskService
{
    /// <summary>
    /// Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (15 phút) cho cửa hàng bởi StoreManager hoặc Admin.
    /// </summary>
    Task<ApiResponse<KioskCodeResponseDto>> CreateKioskCodeAsync(int managerUserId, CreateKioskCodeRequestDto request);

    /// <summary>
    /// Kích hoạt thiết bị Kiosk mới sử dụng mã kích hoạt OTP do Cửa hàng trưởng cấp.
    /// </summary>
    Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp, string? userAgent = null);

    /// <summary>
    /// Xác minh DeviceToken của máy Kiosk khi khởi động hoặc Ping duy trì kết nối.
    /// Kiểm tra trạng thái Kiosk, trạng thái chi nhánh, địa chỉ IP và trình duyệt hợp lệ.
    /// </summary>
    Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp, string? userAgent = null);

    /// <summary>
    /// Lấy danh sách toàn bộ trạm Kiosk trong hệ thống chuỗi (Operations Admin).
    /// </summary>
    Task<ApiResponse<List<KioskDetailDto>>> GetAllKiosksAsync();

    /// <summary>
    /// Lấy thông tin chi tiết một trạm Kiosk theo ID.
    /// </summary>
    Task<ApiResponse<KioskDetailDto>> GetKioskByIdAsync(int kioskId);

    /// <summary>
    /// Lấy danh sách các trạm Kiosk đã được kích hoạt thuộc một chi nhánh cửa hàng.
    /// </summary>
    Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId);

    /// <summary>
    /// Thiết lập thông tin mạng (AllowedIp) và trình duyệt (AllowedBrowser) cho trạm Kiosk quầy.
    /// </summary>
    Task<ApiResponse<KioskDetailDto>> UpdateKioskConfigAsync(int kioskId, UpdateKioskConfigDto dto);

    /// <summary>
    /// Khóa khẩn cấp hoặc Mở khóa trạm Kiosk quầy (Operations Admin / Store Manager).
    /// </summary>
    Task<ApiResponse<KioskDetailDto>> UpdateKioskStatusAsync(int kioskId, UpdateKioskStatusDto dto);
}
