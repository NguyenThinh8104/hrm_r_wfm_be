using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;

namespace Modules.Shifts.Controllers;

/// <summary>
/// Controller chuẩn hóa và quản lý bộ khung ca mẫu toàn hệ thống & ca riêng chi nhánh (Enterprise Global / Custom Shifts).
/// </summary>
[ApiController]
[Route("api/v1/shift-templates")]
[Route("api/shift-templates")]
public class ShiftTemplatesController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftTemplatesController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    /// <summary>
    /// Thống kê khung ca (Tổng ca chung, số chi nhánh dùng ca chung, dùng ca riêng, số ca đêm qua ngày).
    /// GET /api/shift-templates/stats
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ShiftTemplateStatsDto>> GetShiftTemplateStats()
    {
        var stats = await _shiftService.GetShiftTemplateStatsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Lấy danh sách khung ca làm việc có phân trang và bộ lọc (scope, branchId, type, isActive, q).
    /// GET /api/shift-templates?scope=&branchId=&type=&isActive=&q=&page=&pageSize=
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<PagedResult<ShiftTemplateDto>>> GetAllShiftTemplates(
        [FromQuery] string? scope = null,
        [FromQuery] ulong? branchId = null,
        [FromQuery] string? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null, // backward compatibility
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // Backward compatibility: status='ACTIVE'/'INACTIVE'
        if (isActive == null && !string.IsNullOrWhiteSpace(status))
        {
            isActive = string.Equals(status.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        var result = await _shiftService.GetShiftTemplatesPagedAsync(scope, branchId, type, isActive, q, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một khung ca mẫu theo ID.
    /// GET /api/shift-templates/{id}
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ShiftTemplateDto>> GetShiftTemplateById(uint id)
    {
        var result = await _shiftService.GetShiftTemplateByIdAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound(new { code = "NOT_FOUND", message = result.Message ?? "Không tìm thấy mẫu ca làm việc." });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một khung ca làm việc (Chung GLOBAL hoặc Riêng BRANCH).
    /// POST /api/shift-templates
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult> CreateShiftTemplate([FromBody] CreateShiftTemplateRequest request)
    {
        var result = await _shiftService.CreateShiftTemplateAsync(request, GetCurrentUserId(), GetIpAddress());
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new
            {
                code = result.Code ?? "ERROR",
                message = result.Message,
                details = result.Details
            });
        }
        return StatusCode(201, result.Data);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin khung ca làm việc.
    /// PUT /api/shift-templates/{id}?confirm=false|true
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult> UpdateShiftTemplate(
        uint id,
        [FromBody] UpdateShiftTemplateRequest request,
        [FromQuery] bool confirm = false)
    {
        var result = await _shiftService.UpdateShiftTemplateAsync(id, request, confirm, GetCurrentUserId(), GetIpAddress());
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new
            {
                code = result.Code ?? "ERROR",
                message = result.Message,
                affectedBranchCount = result.AffectedBranchCount,
                details = result.Details
            });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật trạng thái kích hoạt / ngừng áp dụng khung ca.
    /// PATCH /api/shift-templates/{id}/active?confirm=false|true
    /// </summary>
    [HttpPatch("{id}/active")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult> UpdateShiftTemplateActive(
        uint id,
        [FromBody] UpdateShiftTemplateActiveRequest request,
        [FromQuery] bool confirm = false)
    {
        var result = await _shiftService.UpdateShiftTemplateActiveAsync(id, request.IsActive, confirm, GetCurrentUserId(), GetIpAddress());
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new
            {
                code = result.Code ?? "ERROR",
                message = result.Message,
                affectedBranchCount = result.AffectedBranchCount,
                details = result.Details
            });
        }
        return Ok(result.Data);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật trạng thái khung ca (ACTIVE / INACTIVE) - Tương thích ngược với endpoint cũ.
    /// PATCH /api/shift-templates/{id}/status
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult> UpdateShiftTemplateStatus(
        uint id,
        [FromBody] UpdateShiftTemplateStatusDto dto,
        [FromQuery] bool confirm = false)
    {
        bool isActive = string.Equals((dto.Status ?? "").Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase);
        var result = await _shiftService.UpdateShiftTemplateActiveAsync(id, isActive, confirm, GetCurrentUserId(), GetIpAddress());
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new
            {
                code = result.Code ?? "ERROR",
                message = result.Message,
                affectedBranchCount = result.AffectedBranchCount,
                details = result.Details
            });
        }
        return Ok(result.Data);
    }

    private ulong? GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("EmployeeId")?.Value
            ?? User.FindFirst("sub")?.Value;
        return ulong.TryParse(idStr, out var id) ? id : null;
    }

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
