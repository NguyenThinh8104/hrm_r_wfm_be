using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Modules.Attendance.Services;
using Shared.Common;

namespace Modules.Attendance.Controllers;

/// <summary>
/// API Controller quản lý bảng điểm danh tháng, xuất Excel và đối soát ảnh dành cho Store Manager.
/// </summary>
[ApiController]
[Route("api/v1/store-manager/attendance")]
[Authorize(Roles = "STORE_MANAGER,StoreManager,ADMIN,Admin,OPERATIONS_ADMIN,OperationsAdmin,BUSINESS_OWNER,BusinessOwner")]
public class StoreManagerAttendanceController : ControllerBase
{
    private readonly IStoreManagerMonthlyAttendanceService _monthlyAttendanceService;
    private readonly AttendanceExportService _exportService;

    public StoreManagerAttendanceController(
        IStoreManagerMonthlyAttendanceService monthlyAttendanceService,
        AttendanceExportService exportService)
    {
        _monthlyAttendanceService = monthlyAttendanceService;
        _exportService = exportService;
    }

    /// <summary>
    /// [Store Manager] Lấy bảng điểm danh trong tháng của chi nhánh kèm số liệu thống kê.
    /// </summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<ApiResponse<StoreManagerMonthlyAttendanceResponseDto>>> GetMonthlyAttendance(
        [FromQuery] int month,
        [FromQuery] int year,
        [FromQuery] ulong? branchId,
        [FromQuery] ulong? employeeId)
    {
        var effectiveBranchId = ResolveBranchId(branchId);
        if (effectiveBranchId == 0)
        {
            return BadRequest(ApiResponse<StoreManagerMonthlyAttendanceResponseDto>.Fail("Không xác định được chi nhánh quản lý."));
        }

        var result = await _monthlyAttendanceService.GetMonthlyAttendanceAsync(effectiveBranchId, month, year, employeeId);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Yêu cầu cấp liên kết xem ảnh đối soát khuôn mặt (Presigned URL tạm thời 3 phút).
    /// </summary>
    [HttpGet("photo-url/{attendanceLogId}")]
    public async Task<ActionResult<ApiResponse<AttendancePhotoUrlResponseDto>>> GetAttendancePhotoUrl(
        ulong attendanceLogId,
        [FromQuery] string type = "checkin",
        [FromQuery] ulong? branchId = null)
    {
        var effectiveBranchId = ResolveBranchId(branchId);
        if (effectiveBranchId == 0)
        {
            return BadRequest(ApiResponse<AttendancePhotoUrlResponseDto>.Fail("Không xác định được chi nhánh quản lý."));
        }

        var result = await _monthlyAttendanceService.GetAttendancePhotoUrlAsync(effectiveBranchId, attendanceLogId, type);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// [Store Manager] Xuất file Excel báo cáo chấm công tháng (ClosedXML - 2 Sheets).
    /// </summary>
    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportMonthlyAttendanceExcel(
        [FromQuery] int month,
        [FromQuery] int year,
        [FromQuery] ulong? branchId = null)
    {
        var effectiveBranchId = ResolveBranchId(branchId);
        if (effectiveBranchId == 0)
        {
            return BadRequest("Không xác định được chi nhánh quản lý.");
        }

        var result = await _monthlyAttendanceService.GetMonthlyAttendanceAsync(effectiveBranchId, month, year);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(result.Message);
        }

        var managerName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Store Manager";
        var branchName = result.Data.BranchName;

        var fileBytes = _exportService.ExportMonthlyAttendanceToExcel(result.Data, branchName, managerName);
        var fileName = $"BangChamCong_{branchName.Replace(" ", "_")}_T{month:D2}_{year}.xlsx";

        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    private ulong ResolveBranchId(ulong? requestedBranchId)
    {
        var storeIdClaim = User.FindFirst("StoreId")?.Value 
                        ?? User.FindFirst("storeId")?.Value 
                        ?? User.FindFirst("BranchId")?.Value;

        if (ulong.TryParse(storeIdClaim, out var branchIdFromToken) && branchIdFromToken > 0)
        {
            // Nếu là Store Manager, luôn ưu tiên branch gán cố định của họ để bảo mật đa người dùng
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (userRole.Contains("MANAGER", StringComparison.OrdinalIgnoreCase))
            {
                return branchIdFromToken;
            }
        }

        if (requestedBranchId.HasValue && requestedBranchId.Value > 0)
        {
            return requestedBranchId.Value;
        }

        return ulong.TryParse(storeIdClaim, out var defaultId) ? defaultId : 1;
    }
}
