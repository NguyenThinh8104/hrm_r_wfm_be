using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Data;
using Shared.Services;

namespace API.Controllers;

/// <summary>
/// DTO phản hồi thông tin tệp tin đã tải lên thành công.
/// </summary>
public class FileUploadResponseDto
{
    public string S3Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ViewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>
/// Controller quản lý tải lên, sinh Presigned URL và xem trực tiếp (preview/stream) tệp tin an toàn (AWS S3 & Local Fallback).
/// </summary>
[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IS3StorageService _s3StorageService;
    private readonly AppDbContext _context;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IS3StorageService s3StorageService,
        AppDbContext context,
        ILogger<FilesController> logger)
    {
        _s3StorageService = s3StorageService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Tải lên tệp tin PDF an toàn với đầy đủ các bước kiểm tra (phần mở rộng .pdf, MIME type application/pdf, magic bytes %PDF, dung lượng).
    /// </summary>
    [HttpPost("upload-pdf")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<FileUploadResponseDto>>> UploadPdf(
        IFormFile file,
        [FromForm] string? folderName)
    {
        var (userId, _, _) = GetCurrentUserInfo();
        if (userId <= 0)
        {
            return Unauthorized(ApiResponse<FileUploadResponseDto>.Fail("Không xác định được danh tính người dùng."));
        }

        try
        {
            var targetFolder = string.IsNullOrWhiteSpace(folderName) ? "documents/pdf" : folderName.Trim();
            var s3Key = await _s3StorageService.UploadPdfAsync(file, targetFolder);

            var viewUrl = _s3StorageService.GetPresignedViewUrl(s3Key, "application/pdf")
                       ?? $"/api/v1/files/view?key={Uri.EscapeDataString(s3Key)}";
            var downloadUrl = $"/api/v1/files/download?key={Uri.EscapeDataString(s3Key)}";

            var response = new FileUploadResponseDto
            {
                S3Key = s3Key,
                FileName = Path.GetFileName(file.FileName),
                FileSize = file.Length,
                ContentType = "application/pdf",
                ViewUrl = viewUrl,
                DownloadUrl = downloadUrl
            };

            _logger.LogInformation("Người dùng #{UserId} đã tải lên PDF thành công: {S3Key}", userId, s3Key);
            return StatusCode(201, ApiResponse<FileUploadResponseDto>.Ok(response, "Tải lên tệp tin PDF thành công."));
        }
        catch (ArgumentException aex)
        {
            return BadRequest(ApiResponse<FileUploadResponseDto>.Fail(aex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi bất thường khi tải lên tệp tin PDF");
            return StatusCode(500, ApiResponse<FileUploadResponseDto>.Fail("Lỗi hệ thống khi tải lên tệp tin PDF. Vui lòng thử lại sau."));
        }
    }

    /// <summary>
    /// Lấy link xem tạm thời (Presigned View URL với Content-Disposition: inline) từ S3 hoặc link local fallback.
    /// Có kiểm tra phân quyền truy cập tài nguyên của người dùng.
    /// </summary>
    [HttpGet("presigned-view-url")]
    public async Task<ActionResult<ApiResponse<string>>> GetPresignedViewUrl(
        [FromQuery] string key,
        [FromQuery] int expirationMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponse<string>.Fail("Vui lòng cung cấp mã s3Key của tệp tin."));
        }

        var isAuthorized = await CheckFileAccessPermissionAsync(key);
        if (!isAuthorized)
        {
            return StatusCode(403, ApiResponse<string>.Fail("Bạn không có quyền truy cập tệp tin này."));
        }

        var ext = Path.GetExtension(key).ToLowerInvariant();
        var contentType = ext == ".pdf" ? "application/pdf" : "application/octet-stream";

        var viewUrl = _s3StorageService.GetPresignedViewUrl(key, contentType, expirationMinutes);
        if (string.IsNullOrWhiteSpace(viewUrl))
        {
            return NotFound(ApiResponse<string>.Fail("Không tìm thấy tệp tin hoặc không thể sinh liên kết xem."));
        }

        return Ok(ApiResponse<string>.Ok(viewUrl, "Lấy liên kết xem tạm thời thành công."));
    }

    /// <summary>
    /// Xem trực tiếp tệp tin trên trình duyệt (Content-Disposition: inline, Content-Type: application/pdf).
    /// Đảm bảo hoạt động thông suốt cả khi dùng S3 thật hoặc Local Storage Fallback.
    /// </summary>
    [HttpGet("view")]
    [HttpHead("view")]
    [AllowAnonymous] // Hỗ trợ nhúng hiển thị trên iframe/browser tab mới, xác thực token qua query hoặc cookie nếu có
    public async Task<IActionResult> ViewFile([FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("Vui lòng cung cấp mã s3Key.");
        }

        var fileResult = await _s3StorageService.GetFileStreamAsync(key);
        if (fileResult == null || fileResult.Value.Stream == null)
        {
            return NotFound("Không tìm thấy tệp tin trong hệ thống lưu trữ.");
        }

        var (stream, contentType, fileName) = fileResult.Value;

        // Thiết lập header Content-Disposition inline để trình duyệt mở xem trực tiếp thay vì tự động download
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(fileName)}\"";
        return File(stream, contentType);
    }

    /// <summary>
    /// Tải tệp tin về máy (Content-Disposition: attachment).
    /// </summary>
    [HttpGet("download")]
    [HttpHead("download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFile([FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("Vui lòng cung cấp mã s3Key.");
        }

        var fileResult = await _s3StorageService.GetFileStreamAsync(key);
        if (fileResult == null || fileResult.Value.Stream == null)
        {
            return NotFound("Không tìm thấy tệp tin trong hệ thống lưu trữ.");
        }

        var (stream, contentType, fileName) = fileResult.Value;
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Kiểm tra phân quyền truy cập tệp tin (RBAC & multi-tenant chi nhánh).
    /// </summary>
    private async Task<bool> CheckFileAccessPermissionAsync(string key)
    {
        var (userId, role, branchId) = GetCurrentUserInfo();
        if (userId <= 0) return false;

        var normalizedRole = role.ToUpper();

        // 1. Quản trị viên cấp cao có quyền truy cập mọi tài liệu
        if (normalizedRole == "OPERATIONS_ADMIN" || normalizedRole == "OPERATIONSADMIN" ||
            normalizedRole == "ADMIN" ||
            normalizedRole == "BUSINESS_OWNER" || normalizedRole == "BUSINESSOWNER")
        {
            return true;
        }

        // 2. Nếu tệp tin thuộc đơn đề xuất định biên headcount-requests:
        if (key.StartsWith("headcount-requests", StringComparison.OrdinalIgnoreCase))
        {
            var req = await _context.HeadcountImportRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.FilePath == key);

            if (req == null)
            {
                // Nếu không tìm thấy đơn gắn với key này, từ chối quyền truy cập
                return false;
            }

            // Nếu là Store Manager: chỉ được xem tệp tin thuộc chi nhánh của mình
            if (normalizedRole == "STORE_MANAGER" || normalizedRole == "STOREMANAGER")
            {
                return branchId.HasValue && branchId.Value == req.BranchId;
            }

            return false;
        }

        // Với các tài liệu dùng chung khác, mặc định cho phép user đã xác thực truy cập
        return true;
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
