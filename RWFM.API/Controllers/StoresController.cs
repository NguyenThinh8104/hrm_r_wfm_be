using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RWFM.Application.Common;
using RWFM.Infrastructure.Data;

namespace RWFM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly RWFMDbContext _context;

    public StoresController(RWFMDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetAllStores()
    {
        var stores = await _context.Stores
            .Where(s => s.IsActive)
            .Select(s => new
            {
                s.StoreId,
                s.StoreCode,
                s.StoreName,
                s.Address,
                s.Phone
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(stores, "Lấy danh sách cửa hàng thành công."));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> GetStoreById(int id)
    {
        var store = await _context.Stores
            .FirstOrDefaultAsync(s => s.StoreId == id);

        if (store == null) return NotFound(ApiResponse.Fail("Không tìm thấy cửa hàng."));

        return Ok(ApiResponse<object>.Ok(new
        {
            store.StoreId,
            store.StoreCode,
            store.StoreName,
            store.Address,
            store.Phone
        }));
    }
}
