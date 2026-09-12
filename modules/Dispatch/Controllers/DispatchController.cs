using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;

namespace Modules.Dispatch.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "StoreManager,OperationsAdmin,BusinessOwner")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchService _dispatchService;

    public DispatchController(IDispatchService dispatchService)
    {
        _dispatchService = dispatchService;
    }

    [HttpPost("request")]
    public async Task<ActionResult<ApiResponse<DispatchRecordDto>>> CreateDispatchRequest([FromBody] CreateDispatchRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var managerEmpId))
        {
            return Unauthorized(ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.CreateDispatchRequestAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("review")]
    public async Task<ActionResult<ApiResponse<bool>>> ReviewDispatchRequest([FromBody] ReviewDispatchRequestDto request)
    {
        var empIdClaim = User.FindFirst("EmployeeId")?.Value;
        if (!int.TryParse(empIdClaim, out var managerEmpId))
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính quản lý."));
        }

        var result = await _dispatchService.ReviewDispatchRequestAsync(managerEmpId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("store/{storeId}")]
    public async Task<ActionResult<ApiResponse<List<DispatchRecordDto>>>> GetStoreDispatches(int storeId)
    {
        var result = await _dispatchService.GetDispatchesByStoreAsync(storeId);
        return Ok(result);
    }
}

