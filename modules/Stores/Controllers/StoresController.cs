using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// API Controller quản lý danh mục chi nhánh cửa hàng (UC 1.2).
/// Actor: Operations Admin (Quản trị vận hành chuỗi).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly IStoreService _storeService;

    public StoresController(IStoreService storeService)
    {
        _storeService = storeService;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống.
    /// Cho phép lọc theo trạng thái: ACTIVE, LOCKED hoặc lấy tất cả.
    /// </summary>
    /// <param name="status">Trạng thái lọc ("ACTIVE", "LOCKED" hoặc để trống)</param>
    /// <returns>Danh sách chi nhánh kèm thống kê trạm Kiosk</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<StoreDetailDto>>>> GetAllStores([FromQuery] string? status = null)
    {
        var result = await _storeService.GetAllStoresAsync(status);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một cửa hàng theo ID kèm danh sách Kiosk và cấu hình mạng/trình duyệt.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cửa hàng</param>
    /// <returns>Thông tin chi tiết cửa hàng</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<StoreDetailDto>>> GetStoreById(int id)
    {
        var result = await _storeService.GetStoreByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới chi nhánh cửa hàng vào chuỗi siêu thị tiện lợi.
    /// </summary>
    /// <param name="dto">DTO thông tin chi nhánh và cấu hình mạng Kiosk</param>
    /// <returns>Chi nhánh mới được tạo</returns>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<StoreDetailDto>>> CreateStore([FromBody] CreateStoreDto dto)
    {
        var result = await _storeService.CreateStoreAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin chi nhánh cửa hàng (Tên, địa chỉ, cấu hình mạng Kiosk).
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cần cập nhật</param>
    /// <param name="dto">DTO thông tin cập nhật</param>
    /// <returns>Thông tin chi nhánh sau khi cập nhật</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<StoreDetailDto>>> UpdateStore(int id, [FromBody] UpdateStoreDto dto)
    {
        var result = await _storeService.UpdateStoreAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Khóa hoặc Mở khóa chi nhánh cửa hàng.
    /// Khi chi nhánh bị khóa (LOCKED), các trạm Kiosk thuộc chi nhánh này sẽ tự động bị từ chối hoạt động.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh</param>
    /// <param name="dto">DTO chứa trạng thái mới ("ACTIVE" hoặc "LOCKED") và lý do</param>
    /// <returns>Thông tin chi nhánh sau khi đổi trạng thái</returns>
    [HttpPut("{id}/status")]
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER")]
    public async Task<ActionResult<ApiResponse<StoreDetailDto>>> UpdateStoreStatus(int id, [FromBody] UpdateStoreStatusDto dto)
    {
        var result = await _storeService.UpdateStoreStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
