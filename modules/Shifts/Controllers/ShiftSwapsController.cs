using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;

namespace Modules.Shifts.Controllers;

/// <summary>
/// Controller xử lý quy trình đổi ca trực, chuyển ca trực và xác thực đồng nghiệp 2 bước (UC 2.2).
/// Route chính: api/shift-swaps
/// Route alias tương thích: api/shifts
/// </summary>
[ApiController]
[Route("api/shift-swaps")]
[Route("api/shifts")]
[Authorize]
public class ShiftSwapsController : ControllerBase
{
    private readonly IShiftSwapService _swapService;

    public ShiftSwapsController(IShiftSwapService swapService)
    {
        _swapService = swapService;
    }

    /// <summary>
    /// Tạo yêu cầu đổi ca làm việc, chuyển ca (nhờ làm thay) hoặc xin nghỉ ca.
    /// POST /api/shift-swaps/swap-request hoặc POST /api/shifts/swap-request
    /// </summary>
    [HttpPost("swap-request")]
    public async Task<ActionResult<ApiResponse<ShiftSwapRequestDto>>> CreateSwapRequest([FromBody] CreateSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<ShiftSwapRequestDto>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _swapService.RequestShiftSwapAsync(empId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Quản lý duyệt hoặc từ chối yêu cầu đổi ca trực (Bước 2).
    /// POST /api/shift-swaps/swap-review hoặc POST /api/shifts/swap-review
    /// </summary>
    [HttpPost("swap-review")]
    [Authorize(Roles = "STORE_MANAGER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> ReviewSwapRequest([FromBody] ReviewSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        int.TryParse(empIdClaim, out var managerEmpId);

        var result = await _swapService.ReviewShiftSwapAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Đồng nghiệp (TargetUser) xác nhận hoặc từ chối đơn đổi/chuyển ca (Bước 1 luồng 2 bước).
    /// POST /api/shift-swaps/swap-peer-review hoặc POST /api/shifts/swap-peer-review
    /// </summary>
    [HttpPost("swap-peer-review")]
    public async Task<ActionResult<ApiResponse<bool>>> PeerReviewSwapRequest([FromBody] PeerReviewSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _swapService.RespondToSwapRequestAsync(empId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Nhân viên tạo đơn hủy đơn đổi/chuyển/nghỉ ca đang chờ xử lý.
    /// POST /api/shift-swaps/swap-cancel/{swapRequestId} hoặc POST /api/shifts/swap-cancel/{swapRequestId}
    /// </summary>
    [HttpPost("swap-cancel/{swapRequestId}")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelSwapRequest(int swapRequestId)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _swapService.CancelSwapRequestAsync(empId, swapRequestId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi ca trực của cửa hàng.
    /// GET /api/shift-swaps/swap-requests/{storeId} hoặc GET /api/shifts/swap-requests/{storeId}
    /// </summary>
    [HttpGet("swap-requests/{storeId}")]
    [Authorize(Roles = "STORE_MANAGER,OPERATIONS_ADMIN,BUSINESS_OWNER,StoreManager,OperationsAdmin,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<ShiftSwapRequestDto>>>> GetSwapRequests(int storeId)
    {
        var role = (User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty).Trim().ToUpperInvariant();
        bool isGlobal = role == "OPERATIONS_ADMIN" || role == "BUSINESS_OWNER" || role == "ADMIN" ||
                        role == "OPERATIONSADMIN" || role == "BUSINESSOWNER";

        int effectiveBranchId = storeId;
        if (!isGlobal)
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            if (int.TryParse(storeIdClaim, out var userBranchId) && userBranchId > 0)
            {
                effectiveBranchId = userBranchId;
            }
        }

        var result = await _swapService.GetSwapRequestsByStoreAsync(effectiveBranchId);
        return Ok(result);
    }

    /// <summary>
    /// Nhân viên lấy danh sách các yêu cầu đổi/chuyển ca của chính mình (đã gửi hoặc được nhờ).
    /// GET /api/shift-swaps/my-swap-requests hoặc GET /api/shifts/my-swap-requests
    /// </summary>
    [HttpGet("my-swap-requests")]
    public async Task<ActionResult<ApiResponse<List<ShiftSwapRequestDto>>>> GetMySwapRequests()
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<List<ShiftSwapRequestDto>>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _swapService.GetMySwapRequestsAsync(empId);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách đồng nghiệp đang thực sự làm việc tại chi nhánh vào ngày cụ thể để nhân viên chọn khi đổi/chuyển ca.
    /// GET /api/shift-swaps/colleagues/{branchId} hoặc GET /api/shifts/colleagues/{branchId}
    /// </summary>
    [HttpGet("colleagues/{branchId}")]
    public async Task<ActionResult<ApiResponse<List<ColleagueDto>>>> GetColleagues(int branchId, [FromQuery] string? workDate = null)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        int.TryParse(empIdClaim, out var empId);

        DateOnly? parsedWorkDate = null;
        if (!string.IsNullOrEmpty(workDate) && DateOnly.TryParse(workDate, out var wd))
        {
            parsedWorkDate = wd;
        }

        var result = await _swapService.GetColleaguesForSwapAsync(empId, branchId, parsedWorkDate);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách ca làm việc của một đồng nghiệp trong tương lai để chọn đổi.
    /// GET /api/shift-swaps/colleague-shifts/{colleagueEmployeeId} hoặc GET /api/shifts/colleague-shifts/{colleagueEmployeeId}
    /// </summary>
    [HttpGet("colleague-shifts/{colleagueEmployeeId}")]
    public async Task<ActionResult<ApiResponse<List<ColleagueShiftDto>>>> GetColleagueShifts(int colleagueEmployeeId, [FromQuery] int? requestingAssignmentId = null)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value 
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        int.TryParse(empIdClaim, out var empId);

        var result = await _swapService.GetColleagueShiftsAsync(empId, colleagueEmployeeId, requestingAssignmentId);
        return Ok(result);
    }
}
