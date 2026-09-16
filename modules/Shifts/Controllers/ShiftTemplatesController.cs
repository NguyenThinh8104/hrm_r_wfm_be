using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;

namespace Modules.Shifts.Controllers;

/// <summary>
/// Controller chuẩn hóa và quản lý bộ khung ca mẫu toàn hệ thống (UC 1.3 - Operations Admin).
/// </summary>
[ApiController]
[Route("api/v1/shift-templates")]
public class ShiftTemplatesController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftTemplatesController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các khung ca làm việc mẫu (hỗ trợ lọc theo trạng thái status: ACTIVE / INACTIVE).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<List<ShiftTemplateDto>>>> GetAllShiftTemplates([FromQuery] string? status = null)
    {
        var result = await _shiftService.GetAllShiftTemplatesAsync(status);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy thông tin chi tiết một khung ca mẫu theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<ShiftTemplateDto>>> GetShiftTemplateById(uint id)
    {
        var result = await _shiftService.GetShiftTemplateByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một khung ca làm việc chuẩn vào hệ thống.
    /// Nghiệp vụ: Tự động validate start_time != end_time, nếu start_time > end_time thì bắt buộc is_overnight = true, tính giờ công chuẩn.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<ShiftTemplateDto>>> CreateShiftTemplate([FromBody] CreateShiftTemplateDto dto)
    {
        var result = await _shiftService.CreateShiftTemplateAsync(dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin khung ca mẫu chuẩn.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<ShiftTemplateDto>>> UpdateShiftTemplate(uint id, [FromBody] UpdateShiftTemplateDto dto)
    {
        var result = await _shiftService.UpdateShiftTemplateAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật trạng thái khung ca mẫu (ACTIVE / INACTIVE).
    /// Nghiệp vụ: Khi vô hiệu hóa (INACTIVE), không xóa cứng bản ghi để bảo toàn dữ liệu lịch sử xếp ca.
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<ShiftTemplateDto>>> UpdateShiftTemplateStatus(uint id, [FromBody] UpdateShiftTemplateStatusDto dto)
    {
        var result = await _shiftService.UpdateShiftTemplateStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
