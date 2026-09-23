using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;

using Shared.Services;

namespace Modules.Stores.Controllers;

/// <summary>
/// Controller quản lý Đề xuất Mở rộng Định biên Nhân sự chi nhánh (Headcount Import Requests - UC 1.4 & UC 1.5).
/// </summary>
[ApiController]
[Route("api/v1/headcount-requests")]
[Authorize]
public class HeadcountRequestsController : ControllerBase
{
    private readonly IStoreHeadcountService _headcountService;
    private readonly IS3StorageService _s3StorageService;

    public HeadcountRequestsController(
        IStoreHeadcountService headcountService,
        IS3StorageService s3StorageService)
    {
        _headcountService = headcountService;
        _s3StorageService = s3StorageService;
    }

    /// <summary>
    /// [Store Manager] Upload file Excel (.xlsx) nộp đơn xin mở rộng / tăng định biên nhân sự chi nhánh.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "STORE_MANAGER,StoreManager,OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<HeadcountImportRequestDto>>> UploadRequest([FromForm] UploadHeadcountRequestDto dto)
    {
        var (userId, role, branchId) = GetCurrentUserInfo();
        if (userId <= 0)
        {
            return Unauthorized(ApiResponse<HeadcountImportRequestDto>.Fail("Không xác định được danh tính người dùng."));
        }

        // Chỉ áp đặt ràng buộc chi nhánh nếu tài khoản là Store Manager
        var isStoreManager = role.Equals("STORE_MANAGER", StringComparison.OrdinalIgnoreCase) 
                          || role.Equals("StoreManager", StringComparison.OrdinalIgnoreCase);
        var effectiveBranchId = isStoreManager ? branchId : null;

        var result = await _headcountService.SubmitImportRequestAsync(userId, effectiveBranchId, dto);
        if (!result.Success) return BadRequest(result);
        return StatusCode(201, result);
    }

    /// <summary>
    /// [Operations Admin] Thẩm định đơn đề xuất: Phê duyệt (hỗ trợ duyệt một phần) hoặc Từ chối đơn.
    /// </summary>
    [HttpPost("{id}/review")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,BUSINESS_OWNER,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<HeadcountImportRequestDto>>> ReviewRequest(ulong id, [FromBody] ReviewHeadcountRequestDto dto)
    {
        var (adminId, _, _) = GetCurrentUserInfo();
        if (adminId <= 0)
        {
            return Unauthorized(ApiResponse<HeadcountImportRequestDto>.Fail("Không xác định được danh tính Quản trị viên."));
        }

        var result = await _headcountService.ReviewRequestAsync(id, dto, adminId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Operations Admin] Đóng hoặc đánh dấu hết hạn thủ công đối với đơn mở rộng cũ còn dư suất.
    /// </summary>
    [HttpPost("{id}/close")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<bool>>> CloseRequest(ulong id, [FromBody] CloseHeadcountRequestDto dto)
    {
        var (adminId, _, _) = GetCurrentUserInfo();
        if (adminId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính Quản trị viên."));
        }

        var result = await _headcountService.CloseOrExpireRequestAsync(id, adminId, dto.Reason);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Store Manager] Lấy danh sách toàn bộ các đơn đề xuất mở rộng định biên.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,STORE_MANAGER,StoreManager,BUSINESS_OWNER,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<HeadcountImportRequestDto>>>> GetAllRequests(
        [FromQuery] string? status,
        [FromQuery] ulong? branchId)
    {
        var (_, role, userBranchId) = GetCurrentUserInfo();
        var normalizedRole = role.ToUpper();

        // Nếu là Store Manager thì chỉ xem đơn của chi nhánh mình
        if ((normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER") && userBranchId.HasValue)
        {
            branchId = userBranchId.Value;
        }

        var result = await _headcountService.GetAllRequestsAsync(status, branchId);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một đơn đề xuất mở rộng định biên theo ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,STORE_MANAGER,StoreManager,BUSINESS_OWNER,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<HeadcountImportRequestDto>>> GetRequestById(ulong id)
    {
        var result = await _headcountService.GetRequestByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Store Manager] Tải xuống hoặc mở file đính kèm của đơn đề xuất mở rộng định biên.
    /// </summary>
    [HttpGet("{id}/download")]
    [HttpGet("{id}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadAttachedFile(ulong id)
    {
        var result = await _headcountService.GetRequestByIdAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound("Không tìm thấy đơn đề xuất.");
        }

        var req = result.Data;
        var fileName = string.IsNullOrWhiteSpace(req.FileName) ? $"De_Xuat_Dinh_Bien_{req.Id}.xlsx" : req.FileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv; charset=utf-8",
            _ => "application/octet-stream"
        };

        // 1. Tìm trên local storage trong uploads/
        if (!string.IsNullOrWhiteSpace(req.FilePath))
        {
            var localRelPath = req.FilePath.Replace('/', Path.DirectorySeparatorChar);
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "uploads", localRelPath),
                Path.Combine(Directory.GetCurrentDirectory(), "API", "uploads", localRelPath),
                Path.Combine(AppContext.BaseDirectory, "uploads", localRelPath),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "uploads", localRelPath)
            };

            foreach (var p in possiblePaths)
            {
                if (System.IO.File.Exists(p))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(p);
                    return File(fileBytes, mimeType, fileName);
                }
            }
        }

        // 2. Nếu S3 đã cấu hình, sinh Presigned URL và chuyển hướng tải
        var presignedUrl = _s3StorageService.GetPresignedUrl(req.FilePath, 60);
        if (!string.IsNullOrWhiteSpace(presignedUrl) && !presignedUrl.Contains("temp_token"))
        {
            return Redirect(presignedUrl);
        }

        // 3. Fallback: Nếu không tìm thấy file vật lý, tự động tạo nội dung file mẫu chuẩn trả về
        var csvHeader = "STT,Mã Vị Trí,Chức Danh Đề Xuất,Số Lượng,Hình Thức Hợp Đồng,Ca Làm Việc Dự Kiến,Lý Do Chi Tiết\n";
        var csvRow = $"1,DX-01,Nhân Sự Mở Rộng,{req.TotalRequested},FULL_TIME,Toàn thời gian,\"{req.Reason}\"\n";
        var generatedBytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csvHeader + csvRow)).ToArray();
        return File(generatedBytes, "text/csv; charset=utf-8", Path.ChangeExtension(fileName, ".csv"));
    }

    /// <summary>
    /// [Operations Admin] Lấy danh sách các đơn mở rộng khả dụng (chưa hết hạn, còn chỉ tiêu) của một chi nhánh.
    /// Phục vụ dropdown cho Admin chọn khi tạo nhân sự vượt định biên chuẩn.
    /// </summary>
    [HttpGet("branch/{branchId}/available")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<HeadcountImportRequestDto>>>> GetAvailableRequests(ulong branchId)
    {
        var result = await _headcountService.GetAvailableRequestsForBranchAsync(branchId);
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu thông tin định biên chi nhánh: Quy mô Tier, Quota, Quân số hiện tại, Vị trí trống, Suất mở rộng khả dụng.
    /// Route 1: /api/v1/headcount-requests/branch/{branchId}/status
    /// Route 2: /api/v1/branches/{branchId}/headcount-status
    /// </summary>
    [HttpGet("branch/{branchId}/status")]
    [HttpGet("/api/v1/branches/{branchId}/headcount-status")]
    [Authorize(Roles = "OPERATIONS_ADMIN,OperationsAdmin,STORE_MANAGER,StoreManager,BUSINESS_OWNER,BusinessOwner,Admin,ADMIN")]
    public async Task<ActionResult<ApiResponse<BranchHeadcountStatusDto>>> GetBranchHeadcountStatus(ulong branchId)
    {
        var result = await _headcountService.GetBranchHeadcountStatusAsync(branchId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    private (ulong userId, string role, ulong? branchId) GetCurrentUserInfo()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("UserId")?.Value
                 ?? User.FindFirst("EmployeeId")?.Value;
        ulong.TryParse(idStr, out var userId);

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var branchIdStr = User.FindFirst("StoreId")?.Value
                       ?? User.FindFirst("HomeBranchId")?.Value
                       ?? User.FindFirst("BranchId")?.Value;
        ulong? branchId = ulong.TryParse(branchIdStr, out var bId) ? bId : null;

        return (userId, role, branchId);
    }
}
