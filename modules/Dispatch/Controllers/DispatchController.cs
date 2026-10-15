using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;

namespace Modules.Dispatch.Controllers;

/// <summary>
/// API Controller quản lý điều động nhân sự tạm thời liên chi nhánh (Cross-Branch Temporary Dispatch).
/// Hỗ trợ cả 2 route chuẩn:
/// - Route chính RESTful: api/dispatches (chuẩn tài nguyên số nhiều)
/// - Route alias: api/dispatch (tương thích ngược 100% cho client cũ)
/// </summary>
[ApiController]
[Route("api/dispatches")]
[Route("api/dispatch")]
[Authorize(Roles = "STORE_MANAGER,SHIFT_LEADER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,ShiftLeader,OperationsAdmin,BusinessOwner")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchWorkflowService _workflowService;
    private readonly IDispatchQuotaService _quotaService;
    private readonly IDispatchMetricsService _metricsService;

    public DispatchController(
        IDispatchWorkflowService workflowService,
        IDispatchQuotaService quotaService,
        IDispatchMetricsService metricsService)
    {
        _workflowService = workflowService;
        _quotaService = quotaService;
        _metricsService = metricsService;
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

        var result = await _workflowService.CreateDispatchRequestAsync(managerEmpId, request);
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

        var result = await _workflowService.ReviewDispatchRequestAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Lấy danh sách các lệnh điều động liên quan đến cửa hàng (cả 2 chiều mượn và cho mượn).
    /// </summary>
    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<ApiResponse<List<DispatchRecordDto>>>> GetStoreDispatches(int storeId)
    {
        var result = await _workflowService.GetDispatchesByStoreAsync(storeId);
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
        var result = await _workflowService.GetAllDispatchesAsync(status, storeId);
        return Ok(result);
    }

    /// <summary>
    /// [Business Owner & Ops Admin] Giám sát ma trận điều chuyển và tổng số giờ công chi viện toàn mạng lưới.
    /// </summary>
    [HttpGet("network-metrics")]
    [Authorize(Roles = "BUSINESS_OWNER,OPERATIONS_ADMIN,BusinessOwner,OperationsAdmin")]
    public async Task<ActionResult<ApiResponse<DispatchNetworkMetricsDto>>> GetNetworkMetrics(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate)
    {
        DateOnly? parsedFromDate = !string.IsNullOrEmpty(fromDate) && DateOnly.TryParse(fromDate, out var fDate) ? fDate : null;
        DateOnly? parsedToDate = !string.IsNullOrEmpty(toDate) && DateOnly.TryParse(toDate, out var tDate) ? tDate : null;

        var result = await _metricsService.GetNetworkMetricsAsync(parsedFromDate, parsedToDate);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager A] Chỉnh sửa đơn đề nghị chi viện đang chờ duyệt (PENDING) hoặc đã thu hồi (RECALLED).
    /// </summary>
    [HttpPut("request/{id}")]
    public async Task<ActionResult<ApiResponse<DispatchRecordDto>>> UpdateDispatchRequest(int id, [FromBody] UpdateDispatchRequestDto request)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _workflowService.UpdateDispatchRequestAsync(managerEmpId, id, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager A] Thu hồi đơn đề nghị chi viện đang chờ duyệt để chỉnh sửa (RECALLED).
    /// </summary>
    [HttpPost("recall/{id}")]
    public async Task<ActionResult<ApiResponse<DispatchRecordDto>>> RecallDispatchRequest(int id, [FromBody] RecallDispatchRequestDto? request)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _workflowService.RecallDispatchRequestAsync(managerEmpId, id, request?.Reason);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager A] Hủy và xóa đơn đề nghị chi viện khi đang chờ duyệt (PENDING) hoặc đã thu hồi (RECALLED).
    /// </summary>
    [HttpDelete("request/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDispatchRequest(int id)
    {
        var managerEmpId = GetCurrentUserId();
        if (managerEmpId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _workflowService.DeleteDispatchRequestAsync(managerEmpId, id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager & Ops Admin] Lấy danh sách nhân sự của chi nhánh phục vụ điều động chi viện (UC 4.1, UC 4.2).
    /// Đã xóa bỏ route đè chéo Kiosk và bảo vệ bảo mật danh sách nhân sự qua quyền Quản lý/Admin.
    /// </summary>
    [HttpGet("branch-employees/{branchId:long}")]
    public async Task<ActionResult<ApiResponse<List<DispatchEmployeeOptionDto>>>> GetBranchEmployeesForDispatch(
        [FromRoute] ulong branchId)
    {
        if (branchId == 0)
        {
            return BadRequest(ApiResponse<List<DispatchEmployeeOptionDto>>.Fail("Vui lòng cung cấp mã chi nhánh (branchId)."));
        }

        var result = await _quotaService.GetBranchEmployeesForDispatchAsync(branchId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Store Manager & Ops Admin] Tra cứu định biên an toàn và số lượng nhân sự khả dụng cho điều động của chi nhánh hỗ trợ.
    /// </summary>
    [HttpGet("branch-quota/{branchId:long}")]
    public async Task<ActionResult<ApiResponse<BranchDispatchQuotaStatusDto>>> GetBranchDispatchQuota(
        [FromRoute] ulong branchId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] ulong? excludeDispatchId)
    {
        DateOnly? parsedStartDate = !string.IsNullOrEmpty(startDate) && DateOnly.TryParse(startDate, out var sDate) ? sDate : null;
        DateOnly? parsedEndDate = !string.IsNullOrEmpty(endDate) && DateOnly.TryParse(endDate, out var eDate) ? eDate : null;

        var result = await _quotaService.GetBranchDispatchQuotaAsync(branchId, parsedStartDate, parsedEndDate, excludeDispatchId);
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
