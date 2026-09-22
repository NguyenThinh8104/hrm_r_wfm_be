using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;

namespace Modules.Dispatch.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "STORE_MANAGER,SHIFT_LEADER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,ShiftLeader,OperationsAdmin,BusinessOwner")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchService _dispatchService;

    public DispatchController(IDispatchService dispatchService)
    {
        _dispatchService = dispatchService;
    }

    /// <summary>
    /// [Store Manager A] Tạo phiếu đề nghị chi viện nhân sự liên chi nhánh (UC 4.1).
    /// </summary>
    [HttpPost("request")]
    public async Task<ActionResult<ApiResponse<DispatchRecordDto>>> CreateDispatchRequest([FromBody] CreateDispatchRequestDto request)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.CreateDispatchRequestAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager B] Phê duyệt hoặc từ chối đơn đề nghị chi viện và chỉ định nhân sự (UC 4.2).
    /// </summary>
    [HttpPost("review")]
    public async Task<ActionResult<ApiResponse<bool>>> ReviewDispatchRequest([FromBody] ReviewDispatchRequestDto request)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.ReviewDispatchRequestAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Lấy danh sách các lệnh điều động liên quan đến cửa hàng.
    /// </summary>
    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<ApiResponse<List<DispatchRecordDto>>>> GetStoreDispatches(int storeId)
    {
        var result = await _dispatchService.GetDispatchesByStoreAsync(storeId);
        return Ok(result);
    }

    /// <summary>
    /// [Quản lý & Admin] Lấy toàn bộ danh sách lệnh điều động toàn hệ thống có hỗ trợ lọc.
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<DispatchRecordDto>>>> GetAllDispatches(
        [FromQuery] string? status,
        [FromQuery] int? storeId)
    {
        var result = await _dispatchService.GetAllDispatchesAsync(status, storeId);
        return Ok(result);
    }

    /// <summary>
    /// [Business Owner & Ops Admin] Giám sát ma trận điều chuyển và tổng số giờ công chi viện toàn mạng lưới .
    /// </summary>
    [HttpGet("network-metrics")]
    [Authorize(Roles = "BUSINESS_OWNER,OPERATIONS_ADMIN,BusinessOwner,OperationsAdmin")]
    public async Task<ActionResult<ApiResponse<DispatchNetworkMetricsDto>>> GetNetworkMetrics(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate)
    {
        DateOnly? parsedFromDate = !string.IsNullOrEmpty(fromDate) && DateOnly.TryParse(fromDate, out var fDate) ? fDate : null;
        DateOnly? parsedToDate = !string.IsNullOrEmpty(toDate) && DateOnly.TryParse(toDate, out var tDate) ? tDate : null;

        var result = await _dispatchService.GetNetworkMetricsAsync(parsedFromDate, parsedToDate);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager A] Chỉnh sửa đơn đề nghị chi viện đang chờ duyệt (PENDING) - Cơ chế cập nhật trực tiếp tại chỗ (Cách 1).
    /// </summary>
    [HttpPut("request/{id}")]
    public async Task<ActionResult<ApiResponse<DispatchRecordDto>>> UpdateDispatchRequest(int id, [FromBody] UpdateDispatchRequestDto request)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.UpdateDispatchRequestAsync(managerEmpId, id, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager A] Hủy và xóa đơn đề nghị chi viện khi đang chờ duyệt (PENDING).
    /// </summary>
    [HttpDelete("request/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDispatchRequest(int id)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.DeleteDispatchRequestAsync(managerEmpId, id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private int GetCurrentUserId()
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
                      ?? User.FindFirst("UserId")?.Value 
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(empIdClaim, out var id) ? id : 0;
    }
}

