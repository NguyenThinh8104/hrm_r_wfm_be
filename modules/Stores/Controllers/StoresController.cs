using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Data;

namespace Modules.Stores.Controllers;

/// <summary>
/// API Controller quản lý danh mục chi nhánh cửa hàng.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly AppDbContext _context;

    public StoresController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh cửa hàng đang hoạt động trong hệ thống.
    /// </summary>
    /// <returns>Danh sách cửa hàng kèm mã, tên, địa chỉ và số điện thoại</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetAllStores()
    {
        var stores = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .Select(b => new
            {
                StoreId = (int)b.Id,
                StoreCode = b.BranchCode,
                StoreName = b.Name,
                Address = b.Address
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(stores, "Lấy danh sách cửa hàng thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một cửa hàng theo ID.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cửa hàng</param>
    /// <returns>Thông tin chi tiết cửa hàng</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetStoreById(int id)
    {
        var store = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == (ulong)id);

        if (store == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));

        return Ok(ApiResponse<object>.Ok(new
        {
            StoreId = (int)store.Id,
            StoreCode = store.BranchCode,
            StoreName = store.Name,
            Address = store.Address
        }));
    }
}
