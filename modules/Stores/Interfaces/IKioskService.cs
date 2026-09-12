using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý kích hoạt và xác thực thiết bị Kiosk tại cửa hàng.
/// </summary>
public interface IKioskService
{
    /// <summary>
    /// Tạo mã kích hoạt Kiosk OTP ngẫu nhiên (15 phút) cho cửa hàng bởi StoreManager.
    /// </summary>
    /// <param name="managerUserId">Mã ID của người dùng Quản lý tạo mã</param>
    /// <param name="request">DTO chứa thông tin StoreId và tên Kiosk hiển thị</param>
    /// <returns>ApiResponse chứa thông tin mã kích hoạt OTP và thời gian hết hạn</returns>
    Task<ApiResponse<KioskCodeResponseDto>> CreateKioskCodeAsync(int managerUserId, CreateKioskCodeRequestDto request);

    /// <summary>
    /// Kích hoạt thiết bị Kiosk mới sử dụng mã kích hoạt OTP do Cửa hàng trưởng cấp.
    /// </summary>
    /// <param name="request">DTO chứa mã kích hoạt Code (ví dụ: POS-1234)</param>
    /// <param name="clientIp">Địa chỉ IP của máy Kiosk gửi yêu cầu kích hoạt</param>
    /// <returns>ApiResponse chứa DeviceToken bí mật và thông tin thiết bị Kiosk sau khi kích hoạt</returns>
    Task<ApiResponse<KioskActivationResponseDto>> ActivateKioskAsync(ActivateKioskRequestDto request, string? clientIp);

    /// <summary>
    /// Xác minh DeviceToken của máy Kiosk khi khởi động hoặc Ping duy trì kết nối.
    /// </summary>
    /// <param name="deviceToken">Mã Token bí mật định danh thiết bị Kiosk</param>
    /// <param name="clientIp">Địa chỉ IP hiện tại của trạm Kiosk</param>
    /// <returns>ApiResponse xác nhận Token hợp lệ kèm thông tin cửa hàng gắn liền với Kiosk</returns>
    Task<ApiResponse<KioskActivationResponseDto>> VerifyKioskTokenAsync(string deviceToken, string? clientIp);

    /// <summary>
    /// Lấy danh sách các trạm Kiosk đã được kích hoạt thuộc một chi nhánh cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <returns>ApiResponse chứa danh sách các thiết bị Kiosk của cửa hàng</returns>
    Task<ApiResponse<List<KioskActivationResponseDto>>> GetStoreKiosksAsync(int storeId);
}
