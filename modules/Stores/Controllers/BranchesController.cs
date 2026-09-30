using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

namespace Modules.Stores.Controllers;

/// <summary>
/// Controller quản lý danh mục chi nhánh cửa hàng & tọa độ GPS Geofence (UC 1.2 - Operations Admin).
/// </summary>
[ApiController]
[Route("api/v1/branches")]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;
    private readonly IBranchLockService _branchLockService;

    public BranchesController(IBranchService branchService, IBranchLockService branchLockService)
    {
        _branchService = branchService;
        _branchLockService = branchLockService;
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các chi nhánh cửa hàng trong hệ thống (hỗ trợ lọc status, tier & search).
    /// </summary>
    /// <param name="status">Lọc theo trạng thái hoạt động (ACTIVE / INACTIVE).</param>
    /// <param name="search">Tìm kiếm theo mã chi nhánh, tên hoặc địa chỉ.</param>
    /// <param name="tier">Lọc theo phân cấp chi nhánh (1 = Tier 1: Lớn, 2 = Tier 2: Tiêu chuẩn, 3 = Tier 3: Nhỏ).</param>
    [HttpGet]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAllBranches(
        [FromQuery] string? status = null, 
        [FromQuery] string? search = null,
        [FromQuery] BranchTier? tier = null)
    {
        var result = await _branchService.GetAllBranchesAsync(status, search, tier);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thống kê số lượng chi nhánh theo từng phân cấp quy mô (tier1Count, tier2Count, tier3Count).
    /// Hỗ trợ cả 2 route: /api/v1/branches/tier-summary và /api/v1/branches/summary.
    /// </summary>
    [HttpGet("tier-summary")]
    [HttpGet("summary")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<BranchTierSummaryDto>>> GetBranchTierSummary()
    {
        var result = await _branchService.GetBranchTierSummaryAsync();
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy thông tin chi tiết của một chi nhánh theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> GetBranchById(ulong id)
    {
        var result = await _branchService.GetBranchByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu thông tin định biên chi nhánh (Quy mô Tier, Định biên chuẩn, StaffCount, Quân số, Vị trí trống).
    /// Hỗ trợ cả 2 route: /api/v1/branches/{id}/headcount-status và route alias /api/v1/headcount-requests/branch/{id}/status.
    /// </summary>
    [HttpGet("{id}/headcount-status")]
    [HttpGet("/api/v1/headcount-requests/branch/{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<BranchHeadcountStatusDto>>> GetBranchHeadcountStatus(
        ulong id,
        [FromServices] IStoreHeadcountService headcountService)
    {
        var result = await headcountService.GetBranchHeadcountStatusAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một chi nhánh cửa hàng vào hệ thống (yêu cầu phân cấp BranchTier).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> CreateBranch([FromBody] CreateBranchDto dto)
    {
        // Kiểm tra validation thuộc tính (bao gồm bắt buộc có BranchTier và thuộc khoảng 1..3)
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<BranchDto>.Fail("Dữ liệu phân cấp hoặc thông tin chi nhánh không hợp lệ."));
        }
        var result = await _branchService.CreateBranchAsync(dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật thông tin chi nhánh cửa hàng (Tên, địa chỉ, phân cấp BranchTier).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranch(ulong id, [FromBody] UpdateBranchDto dto)
    {
        // Kiểm tra validation dữ liệu cập nhật
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<BranchDto>.Fail("Dữ liệu phân cấp hoặc thông tin chi nhánh không hợp lệ."));
        }
        var result = await _branchService.UpdateBranchAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Khóa hoặc Mở khóa chi nhánh cửa hàng (ACTIVE / INACTIVE).
    /// Nghiệp vụ: Khi khóa Branch (INACTIVE), toàn bộ Kiosk thuộc chi nhánh tự động chuyển sang BLOCKED.
    /// </summary>
    [HttpPatch("{id}/status")]
    [HttpPut("{id}/status")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranchStatus(ulong id, [FromBody] UpdateBranchStatusDto dto)
    {
        var result = await _branchService.UpdateBranchStatusAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Kiểm tra các điều kiện chặn trước khi khóa chi nhánh (Không thay đổi dữ liệu).
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cần kiểm tra.</param>
    /// <returns>Đối tượng BranchLockCheckResponseDto gồm canLock, blockers và affectedEmployeeCount.</returns>
    [HttpGet("{id}/lock-check")]
    [Authorize(Policy = "branch.lock")]
    public async Task<ActionResult<BranchLockCheckResponseDto>> CheckBranchLock(ulong id, CancellationToken cancellationToken)
    {
        var checkResult = await _branchLockService.CheckLockConditionsAsync(id, cancellationToken);
        return Ok(checkResult);
    }

    /// <summary>
    /// [Operations Admin] Thực hiện khóa chi nhánh (kiểm tra blocker trong transaction, xử lý nhân sự và ca làm việc).
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cần khóa.</param>
    /// <param name="request">DTO chứa lý do (10-500 ký tự), mã xác nhận, chế độ xử lý nhân sự và ca tương lai.</param>
    [HttpPost("{id}/lock")]
    [Authorize(Policy = "branch.lock")]
    public async Task<IActionResult> LockBranch(
        ulong id, 
        [FromBody] LockBranchRequestDto request, 
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ProblemDetails
            {
                Title = "Dữ liệu yêu cầu không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = string.Join("; ", errors),
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            });
        }

        var currentUserName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value 
            ?? User.FindFirst("EmployeeCode")?.Value 
            ?? User.Identity?.Name 
            ?? "Admin";

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("EmployeeId")?.Value;
        ulong.TryParse(userIdStr, out var currentUserId);

        var result = await _branchLockService.LockBranchAsync(
            id, 
            request, 
            currentUserName, 
            currentUserId > 0 ? currentUserId : null, 
            cancellationToken);

        if (!result.Success)
        {
            if (result.ProblemDetails != null)
            {
                return StatusCode(result.StatusCode, result.ProblemDetails);
            }
            return StatusCode(result.StatusCode, new ProblemDetails
            {
                Status = result.StatusCode,
                Title = "Lỗi xử lý khóa chi nhánh",
                Detail = result.Message
            });
        }

        return Ok(ApiResponse<BranchDto>.Ok(result.Data!, result.Message));
    }

    /// <summary>
    /// [Operations Admin] Thực hiện mở khóa chi nhánh và khôi phục hoạt động cho trạm Kiosk và nhân sự.
    /// </summary>
    /// <param name="id">Mã ID chi nhánh cần mở khóa.</param>
    /// <param name="request">DTO chứa lý do mở khóa (tùy chọn).</param>
    [HttpPost("{id}/unlock")]
    [Authorize(Policy = "branch.lock")]
    public async Task<IActionResult> UnlockBranch(
        ulong id, 
        [FromBody] UnlockBranchRequestDto? request, 
        CancellationToken cancellationToken)
    {
        var currentUserName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value 
            ?? User.FindFirst("EmployeeCode")?.Value 
            ?? User.Identity?.Name 
            ?? "Admin";

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("EmployeeId")?.Value;
        ulong.TryParse(userIdStr, out var currentUserId);

        var result = await _branchLockService.UnlockBranchAsync(
            id, 
            request ?? new UnlockBranchRequestDto(), 
            currentUserName, 
            currentUserId > 0 ? currentUserId : null, 
            cancellationToken);

        if (!result.Success)
        {
            if (result.ProblemDetails != null)
            {
                return StatusCode(result.StatusCode, result.ProblemDetails);
            }
            return StatusCode(result.StatusCode, new ProblemDetails
            {
                Status = result.StatusCode,
                Title = "Lỗi xử lý mở khóa chi nhánh",
                Detail = result.Message
            });
        }

        return Ok(ApiResponse<BranchDto>.Ok(result.Data!, result.Message));
    }

    /// <summary>
    /// [Operations Admin/Admin] Nâng phân cấp Tier của chi nhánh khi đạt kịch trần định biên nhân sự (Tier 3 -> Tier 2 -> Tier 1).
    /// </summary>
    [HttpPost("{id}/upgrade-tier")]
    [HttpPatch("{id}/upgrade-tier")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpgradeBranchTier(ulong id)
    {
        var result = await _branchService.UpgradeBranchTierAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Xóa chi nhánh cửa hàng khỏi hệ thống.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteBranch(ulong id)
    {
        var result = await _branchService.DeleteBranchAsync(id);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách toàn bộ các trạm Kiosk thuộc một chi nhánh.
    /// </summary>
    [HttpGet("{id}/kiosks")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<List<KioskDto>>>> GetBranchKiosks(ulong id)
    {
        var result = await _branchService.GetBranchKiosksAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Thêm mới một trạm Kiosk tại chi nhánh cửa hàng (Tự sinh kiosk_token và kiosk_code).
    /// </summary>
    [HttpPost("{id}/kiosks")]
    [Authorize(Roles = "OperationsAdmin,OPERATIONS_ADMIN,BusinessOwner,BUSINESS_OWNER,Admin,ADMIN,StoreManager,STORE_MANAGER,ShiftLeader,SHIFT_LEADER")]
    public async Task<ActionResult<ApiResponse<KioskDto>>> CreateBranchKiosk(ulong id, [FromBody] CreateKioskDto dto)
    {
        var result = await _branchService.CreateBranchKioskAsync(id, dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Cập nhật số lượng nhân sự của chi nhánh và tự động xác định lại tier tương ứng.
    /// </summary>
    [HttpPut("{id}/staff-count")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BranchDto>>> UpdateBranchStaffCount(ulong id, [FromBody] UpdateBranchStaffCountDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<BranchDto>.Fail("Dữ liệu không hợp lệ."));

        var result = await _branchService.UpdateBranchStaffCountAsync(id, dto.StaffCount);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
