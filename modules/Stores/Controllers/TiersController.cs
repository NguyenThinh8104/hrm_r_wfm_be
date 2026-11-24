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

    // =========================================================================================
    // LUỒNG 1: QUẢN LÝ PHÂN CẤP CHI NHÁNH (TIER MANAGEMENT)
    // Controller: TiersController
    // Route API: POST /api/tiers hoặc /api/v1/tiers
    // Mục đích: Tiếp nhận DTO CreateTierDto từ Frontend, validate ModelState và chuyển tiếp sang TierService
    // Kết quả: Trả về ApiResponse<TierDto> với mã HTTP 201 Created nếu thành công, 400 Bad Request nếu lỗi
    // =========================================================================================

    /// <summary>
    /// [POST /api/tiers] - Tạo mới một phân cấp chi nhánh (Tier).
    /// BƯỚC 1 CỦA LUỒNG: Nhận request JSON từ client, kiểm tra DataAnnotations trên CreateTierDto.
    /// BƯỚC 2: Gọi ITierService.CreateTierAsync để thực thi logic nghiệp vụ và lưu Database bảng branch_tiers.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TierDto>>> CreateTier([FromBody] CreateTierDto dto)
    {
        // 1. Kiểm tra tính hợp lệ cơ bản của dữ liệu (DataAnnotations: [Required], [Range]...)
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<TierDto>.Fail("Dữ liệu đầu vào không hợp lệ"));

        // 2. Chuyển tiếp sang TierService để kiểm tra trùng tên và kiểm tra quy tắc Min/Max staff
        var result = await _tierService.CreateTierAsync(dto);
        if (!result.Success)
            return BadRequest(result);

        // 3. Trả về kết quả thành công với HTTP 201 Created kèm dữ liệu Tier vừa tạo
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
