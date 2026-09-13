using Modules.Stores.DTOs;
using Shared.Common;

namespace Modules.Stores.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý danh mục chi nhánh cửa hàng (UC 1.2).
/// </summary>
public interface IStoreService
{
    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng, hỗ trợ lọc theo trạng thái.
    /// </summary>
    /// <param name="status">Lọc theo trạng thái "ACTIVE", "LOCKED" hoặc null (lấy tất cả)</param>
    /// <returns>Danh sách chi nhánh kèm thống kê Kiosk</returns>
    Task<ApiResponse<List<StoreDetailDto>>> GetAllStoresAsync(string? status = null);

    /// <summary>
    /// Lấy thông tin chi tiết của một cửa hàng theo ID kèm danh sách trạm Kiosk và cấu hình mạng/trình duyệt.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cửa hàng</param>
    /// <returns>Chi tiết cửa hàng</returns>
    Task<ApiResponse<StoreDetailDto>> GetStoreByIdAsync(int id);

    /// <summary>
    /// Thêm mới một chi nhánh cửa hàng vào hệ thống (Operations Admin).
    /// </summary>
    /// <param name="dto">DTO chứa mã chi nhánh, tên, địa chỉ và cấu hình IP/trình duyệt</param>
    /// <returns>Chi nhánh mới được tạo</returns>
    Task<ApiResponse<StoreDetailDto>> CreateStoreAsync(CreateStoreDto dto);

    /// <summary>
    /// Cập nhật thông tin chi nhánh cửa hàng (Operations Admin).
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cần cập nhật</param>
    /// <param name="dto">DTO chứa dữ liệu mới</param>
    /// <returns>Chi nhánh sau khi cập nhật</returns>
    Task<ApiResponse<StoreDetailDto>> UpdateStoreAsync(int id, UpdateStoreDto dto);

    /// <summary>
    /// Khóa hoặc Mở khóa chi nhánh cửa hàng (Operations Admin).
    /// Khi chi nhánh bị khóa, trạm Kiosk tại chi nhánh sẽ tự động bị tạm dừng hoạt động.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh</param>
    /// <param name="dto">DTO chứa trạng thái mới ("ACTIVE" hoặc "LOCKED") và lý do</param>
    /// <returns>Chi nhánh sau khi thay đổi trạng thái</returns>
    Task<ApiResponse<StoreDetailDto>> UpdateStoreStatusAsync(int id, UpdateStoreStatusDto dto);
}
