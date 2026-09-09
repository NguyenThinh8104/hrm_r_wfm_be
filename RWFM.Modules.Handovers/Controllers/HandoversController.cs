using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RWFM.Modules.Handovers.DTOs;
using RWFM.Modules.Handovers.Interfaces;
using RWFM.Shared.Common;

namespace RWFM.Modules.Handovers.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HandoversController : ControllerBase
{
    private readonly IHandoverService _handoverService;

    public HandoversController(IHandoverService handoverService)
    {
        _handoverService = handoverService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<ShiftHandoverSessionDto>>> GetCurrentHandover(
        [FromQuery] int storeId,
        [FromQuery] int assignmentId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        int.TryParse(empIdClaim, out var empId);

        var result = await _handoverService.GetOrCreateSessionAsync(storeId, assignmentId, targetDate, empId);
        return Ok(result);
    }

    [HttpPost("cashier-submit")]
    [Authorize(Roles = "Employee,ShiftLeader,StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<ShiftHandoverSessionDto>>> SubmitCashierHandover([FromBody] CashierHandoverSubmitDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var cashierEmpId))
        {
            return Unauthorized(ApiResponse<ShiftHandoverSessionDto>.Fail("Không xác định được danh tính thu ngân."));
        }

        var result = await _handoverService.SubmitCashierHandoverAsync(cashierEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("security-submit")]
    [Authorize(Roles = "Employee,ShiftLeader,StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<ShiftHandoverSessionDto>>> SubmitSecurityHandover([FromBody] SecurityHandoverSubmitDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var securityEmpId))
        {
            return Unauthorized(ApiResponse<ShiftHandoverSessionDto>.Fail("Không xác định được danh tính bảo vệ."));
        }

        var result = await _handoverService.SubmitSecurityHandoverAsync(securityEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("leader-sign")]
    [Authorize(Roles = "ShiftLeader,StoreManager,ChainAdmin")]
    public async Task<ActionResult<ApiResponse<ShiftHandoverSessionDto>>> LeaderSignHandover([FromBody] LeaderSignHandoverDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var leaderEmpId))
        {
            return Unauthorized(ApiResponse<ShiftHandoverSessionDto>.Fail("Không xác định được danh tính Trưởng ca."));
        }

        var result = await _handoverService.LeaderSignHandoverAsync(leaderEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<ApiResponse<List<ShiftHandoverSessionDto>>>> GetStoreHandovers(
        int storeId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var result = await _handoverService.GetSessionsByStoreAsync(storeId, targetDate);
        return Ok(result);
    }
}
