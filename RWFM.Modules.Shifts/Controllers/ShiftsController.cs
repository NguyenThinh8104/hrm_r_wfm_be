using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWFM.Modules.Shifts.DTOs;
using RWFM.Modules.Shifts.Interfaces;
using RWFM.Shared.Common;

namespace RWFM.Modules.Shifts.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ShiftDto>>>> GetShifts()
    {
        var result = await _shiftService.GetAllShiftsAsync();
        return Ok(result);
    }

    [HttpGet("schedule")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ShiftAssignmentDto>>>> GetSchedule(
        [FromQuery] int storeId,
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (!DateOnly.TryParse(startDate, out var sDate) || !DateOnly.TryParse(endDate, out var eDate))
        {
            return BadRequest(ApiResponse<List<ShiftAssignmentDto>>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var result = await _shiftService.GetScheduleAsync(storeId, sDate, eDate);
        return Ok(result);
    }

    [HttpPost("assign")]
    [Authorize(Roles = "StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<ShiftAssignmentDto>>> AssignShift([FromBody] CreateShiftAssignmentDto request)
    {
        var result = await _shiftService.AssignShiftAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("publish")]
    [Authorize(Roles = "StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> PublishSchedule(
        [FromQuery] int storeId,
        [FromQuery] string weekStartDate)
    {
        if (!DateOnly.TryParse(weekStartDate, out var sDate))
        {
            return BadRequest(ApiResponse<bool>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        int.TryParse(empIdClaim, out var empId);

        var result = await _shiftService.PublishScheduleAsync(storeId, sDate, empId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("my-shifts")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<ShiftAssignmentDto>>>> GetMyShifts(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<List<ShiftAssignmentDto>>.Fail("Không xác định được danh tính nhân viên."));
        }

        if (!DateOnly.TryParse(startDate, out var sDate) || !DateOnly.TryParse(endDate, out var eDate))
        {
            return BadRequest(ApiResponse<List<ShiftAssignmentDto>>.Fail("Định dạng ngày không hợp lệ (YYYY-MM-DD)."));
        }

        var result = await _shiftService.GetEmployeeShiftsAsync(empId, sDate, eDate);
        return Ok(result);
    }

    [HttpPost("swap-request")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ShiftSwapRequestDto>>> CreateSwapRequest([FromBody] CreateSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var empId))
        {
            return Unauthorized(ApiResponse<ShiftSwapRequestDto>.Fail("Không xác định được danh tính nhân viên."));
        }

        var result = await _shiftService.RequestShiftSwapAsync(empId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("swap-review")]
    [Authorize(Roles = "StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> ReviewSwapRequest([FromBody] ReviewSwapRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        int.TryParse(empIdClaim, out var managerEmpId);

        var result = await _shiftService.ReviewShiftSwapAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("swap-requests/{storeId}")]
    [Authorize(Roles = "StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<List<ShiftSwapRequestDto>>>> GetSwapRequests(int storeId)
    {
        var result = await _shiftService.GetSwapRequestsByStoreAsync(storeId);
        return Ok(result);
    }
}
