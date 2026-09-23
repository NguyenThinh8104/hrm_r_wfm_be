using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

[ApiController]
[Route("api/v1/tiers")]
[Route("api/tiers")]
public class TiersController : ControllerBase
{
    private readonly ITierService _tierService;

    public TiersController(ITierService tierService)
    {
        _tierService = tierService;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các Tier chi nhánh.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<TierDto>>>> GetAllTiers()
    {
        var result = await _tierService.GetAllTiersAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một Tier theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TierDto>>> GetTierById(int id)
    {
        var result = await _tierService.GetTierByIdAsync(id);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới một Tier.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TierDto>>> CreateTier([FromBody] CreateTierDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TierDto>.Fail("Dữ liệu đầu vào không hợp lệ"));

        var result = await _tierService.CreateTierAsync(dto);
        if (!result.Success)
            return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// Cập nhật thông tin Tier.
    /// </summary>
    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TierDto>>> UpdateTier(int id, [FromBody] UpdateTierDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TierDto>.Fail("Dữ liệu đầu vào không hợp lệ"));

        var result = await _tierService.UpdateTierAsync(id, dto);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Xóa một Tier (nếu chưa có chi nhánh nào liên kết).
    /// </summary>
    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTier(int id)
    {
        var result = await _tierService.DeleteTierAsync(id);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
