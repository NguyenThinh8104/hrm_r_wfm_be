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
    /// [Admin / Store Manager] Lấy link xem trực tiếp (inline view URL) của tệp tin đính kèm.
    /// </summary>
    [HttpGet("{id}/view-url")]
    public async Task<ActionResult<ApiResponse<string>>> GetAttachedFileViewUrl(ulong id)
    {
        var result = await _headcountService.GetRequestByIdAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy đơn đề xuất."));
        }

        var req = result.Data;
        var (_, role, userBranchId) = GetCurrentUserInfo();
        var normalizedRole = role.ToUpper();

        if ((normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
            && userBranchId.HasValue && userBranchId.Value != req.BranchId)
        {
            return StatusCode(403, ApiResponse<string>.Fail("Bạn không có quyền xem tài liệu của chi nhánh khác."));
        }

        var ext = Path.GetExtension(req.FileName).ToLowerInvariant();
        var mime = ext == ".pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        var viewUrl = _s3StorageService.GetPresignedViewUrl(req.FilePath, mime)
                   ?? $"/api/v1/headcount-requests/{req.Id}/view";

        return Ok(ApiResponse<string>.Ok(viewUrl, "Lấy link xem tệp tin thành công."));
    }

    /// <summary>
    /// [Admin / Store Manager] Xem trực tiếp tệp tin (inline preview) trên trình duyệt (đặc biệt cho PDF).
    /// </summary>
    [HttpGet("{id}/view")]
    [HttpGet("{id}/file")]
    [HttpHead("{id}/view")]
    [HttpHead("{id}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> ViewAttachedFile(ulong id)
    {
        var result = await _headcountService.GetRequestByIdAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound("Không tìm thấy đơn đề xuất.");
        }

        var req = result.Data;
        var (userId, role, userBranchId) = GetCurrentUserInfo();
        var normalizedRole = role.ToUpper();

        // RBAC: Nếu người dùng đã đăng nhập và là Store Manager thì chỉ được xem đơn thuộc chi nhánh mình
        if (userId > 0 && (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
            && userBranchId.HasValue && userBranchId.Value != req.BranchId)
        {
            return StatusCode(403, "Bạn không có quyền xem tài liệu của chi nhánh khác.");
        }

        var fileName = string.IsNullOrWhiteSpace(req.FileName) ? $"De_Xuat_Dinh_Bien_{req.Id}" : req.FileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv; charset=utf-8",
            _ => "application/octet-stream"
        };

        var fileResult = await _s3StorageService.GetFileStreamAsync(req.FilePath);
        if (fileResult != null && fileResult.Value.Stream != null)
        {
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(fileName)}\"";
            return File(fileResult.Value.Stream, mimeType);
        }

        // Fallback: nếu là file PDF và không tìm thấy file vật lý
        if (ext == ".pdf")
        {
            return NotFound("Không tìm thấy nội dung tệp tin PDF.");
        }

        // Fallback: Nếu là file Excel .xlsx, tạo workbook chuẩn bằng ClosedXML để không bao giờ bị corrupt khi mở
        var generatedBytes = GenerateFallbackExcelBytes(req);
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(fileName)}\"";
        return File(generatedBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    /// <summary>
    /// [Admin / Store Manager] Tải xuống tệp tin đính kèm của đơn đề xuất mở rộng định biên.
    /// Khắc phục triệt để lỗi file .xlsx bị hỏng khi mở bằng cách trả về stream chuẩn và fallback OpenXML.
    /// </summary>
    [HttpGet("{id}/download")]
    [HttpHead("{id}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadAttachedFile(ulong id)
    {
        var result = await _headcountService.GetRequestByIdAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound("Không tìm thấy đơn đề xuất.");
        }

        var req = result.Data;
        var (userId, role, userBranchId) = GetCurrentUserInfo();
        var normalizedRole = role.ToUpper();

        if (userId > 0 && (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
            && userBranchId.HasValue && userBranchId.Value != req.BranchId)
        {
            return StatusCode(403, "Bạn không có quyền tải tài liệu của chi nhánh khác.");
        }

        var fileName = string.IsNullOrWhiteSpace(req.FileName) ? $"De_Xuat_Dinh_Bien_{req.Id}.xlsx" : req.FileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv; charset=utf-8",
            _ => "application/octet-stream"
        };

        // 1. Thử đọc file stream trực tiếp từ hệ thống lưu trữ (Local hoặc S3)
        var fileResult = await _s3StorageService.GetFileStreamAsync(req.FilePath);
        if (fileResult != null && fileResult.Value.Stream != null)
        {
            return File(fileResult.Value.Stream, mimeType, fileName);
        }

        // 2. Nếu S3 đã cấu hình và khả dụng, chuyển hướng tới Presigned URL có chữ ký số
        if (_s3StorageService.IsS3Configured)
        {
            var presignedUrl = _s3StorageService.GetPresignedUrl(req.FilePath, 60);
            if (!string.IsNullOrWhiteSpace(presignedUrl) && !presignedUrl.StartsWith("/api"))
            {
                return Redirect(presignedUrl);
            }
        }

        // 3. Fallback: Nếu không tìm thấy file vật lý, dùng ClosedXML tạo file Excel .xlsx chuẩn xác 100%
        if (ext == ".pdf")
        {
            return NotFound("Không tìm thấy tệp tin PDF đính kèm.");
        }

        var fallbackBytes = GenerateFallbackExcelBytes(req);
        var targetFileName = ext == ".xlsx" ? fileName : Path.ChangeExtension(fileName, ".xlsx");
        return File(fallbackBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", targetFileName);
    }

    private static byte[] GenerateFallbackExcelBytes(HeadcountImportRequestDto req)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("De_Xuat_Dinh_Bien");

        worksheet.Cell(1, 1).Value = "STT";
        worksheet.Cell(1, 2).Value = "Mã Đơn";
        worksheet.Cell(1, 3).Value = "Mã Chi Nhánh";
        worksheet.Cell(1, 4).Value = "Tên Chi Nhánh";
        worksheet.Cell(1, 5).Value = "Số Lượng Đề Xuất";
        worksheet.Cell(1, 6).Value = "Người Đề Xuất";
        worksheet.Cell(1, 7).Value = "Trạng Thái";
        worksheet.Cell(1, 8).Value = "Lý Do Chi Tiết";

        worksheet.Cell(2, 1).Value = 1;
        worksheet.Cell(2, 2).Value = $"DX-{req.Id:D4}";
        worksheet.Cell(2, 3).Value = req.BranchCode;
        worksheet.Cell(2, 4).Value = req.BranchName;
        worksheet.Cell(2, 5).Value = req.TotalRequested;
        worksheet.Cell(2, 6).Value = req.RequesterName;
        worksheet.Cell(2, 7).Value = req.Status;
        worksheet.Cell(2, 8).Value = req.Reason;

        var header = worksheet.Range(1, 1, 1, 8);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1E3A8A");
        header.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
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
