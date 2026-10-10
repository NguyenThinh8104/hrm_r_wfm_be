using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Modules.Stores.DTOs;

/// <summary>
/// DTO chi tiết từng đối tượng gây cản trở (Blocker Item).
/// </summary>
public class BranchLockBlockerItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// DTO chứa nhóm điều kiện chặn khóa chi nhánh.
/// </summary>
public class BranchLockBlockerDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<BranchLockBlockerItemDto> Items { get; set; } = new();
}

/// <summary>
/// Response DTO cho endpoint GET /api/branches/{id}/lock-check.
/// </summary>
public class BranchLockCheckResponseDto
{
    [JsonPropertyName("canLock")]
    public bool CanLock { get; set; }

    [JsonPropertyName("blockers")]
    public List<BranchLockBlockerDto> Blockers { get; set; } = new();

    [JsonPropertyName("affectedEmployeeCount")]
    public int AffectedEmployeeCount { get; set; }
}

/// <summary>
/// Request Body cho endpoint POST /api/branches/{id}/lock.
/// </summary>
public class LockBranchRequestDto
{
    /// <summary>
    /// Lý do thực hiện khóa chi nhánh (Bắt buộc, từ 10 đến 500 ký tự).
    /// </summary>
    [Required(ErrorMessage = "Lý do khóa chi nhánh là bắt buộc.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Lý do khóa chi nhánh phải có độ dài từ 10 đến 500 ký tự.")]
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Mã chi nhánh xác nhận (Tùy chọn).
    /// </summary>
    [JsonPropertyName("confirmBranchCode")]
    public string? ConfirmBranchCode { get; set; }

    /// <summary>
    /// Chế độ xử lý nhân sự của chi nhánh (Tùy chọn).
    /// </summary>
    [JsonPropertyName("staffHandlingMode")]
    public string? StaffHandlingMode { get; set; }

    /// <summary>
    /// ID chi nhánh đích nhận nhân sự (Tùy chọn).
    /// </summary>
    [JsonPropertyName("transferToBranchId")]
    public ulong? TransferToBranchId { get; set; }

    /// <summary>
    /// Chế độ xử lý ca làm việc tương lai (Tùy chọn).
    /// </summary>
    [JsonPropertyName("futureShiftHandling")]
    public string? FutureShiftHandling { get; set; }
}

/// <summary>
/// Request Body cho endpoint POST /api/branches/{id}/unlock.
/// </summary>
public class UnlockBranchRequestDto
{
    /// <summary>
    /// Lý do mở khóa chi nhánh (Tùy chọn, tối đa 500 ký tự).
    /// </summary>
    [StringLength(500, ErrorMessage = "Lý do mở khóa tối đa 500 ký tự.")]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// ProblemDetails tùy chỉnh khi có xung đột chặn khóa chi nhánh (409 Conflict).
/// </summary>
public class BranchLockConflictProblemDetails : ProblemDetails
{
    [JsonPropertyName("canLock")]
    public bool CanLock => false;

    [JsonPropertyName("blockers")]
    public List<BranchLockBlockerDto> Blockers { get; set; } = new();

    public BranchLockConflictProblemDetails(List<BranchLockBlockerDto> blockers, string detail = "Chi nhánh không thể khóa do còn điều kiện chặn chưa được xử lý.")
    {
        Title = "Chi nhánh không thể khóa";
        Status = 409;
        Detail = detail;
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
        Blockers = blockers;
    }
}

/// <summary>
/// Kết quả trả về từ nghiệp vụ khóa/mở khóa của BranchLockService.
/// </summary>
public class BranchLockOperationResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; } = 200;
    public string Message { get; set; } = string.Empty;
    public BranchDto? Data { get; set; }
    public List<BranchLockBlockerDto> Blockers { get; set; } = new();
    public ProblemDetails? ProblemDetails { get; set; }

    public static BranchLockOperationResult Ok(BranchDto data, string message) => new()
    {
        Success = true,
        StatusCode = 200,
        Message = message,
        Data = data
    };

    public static BranchLockOperationResult BadRequest(string detail, string title = "Dữ liệu yêu cầu không hợp lệ") => new()
    {
        Success = false,
        StatusCode = 400,
        Message = detail,
        ProblemDetails = new ProblemDetails
        {
            Status = 400,
            Title = title,
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        }
    };

    public static BranchLockOperationResult NotFound(string detail = "Không tìm thấy chi nhánh cửa hàng.") => new()
    {
        Success = false,
        StatusCode = 404,
        Message = detail,
        ProblemDetails = new ProblemDetails
        {
            Status = 404,
            Title = "Không tìm thấy chi nhánh",
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        }
    };

    public static BranchLockOperationResult Conflict(List<BranchLockBlockerDto> blockers, string detail = "Chi nhánh không thể khóa do còn điều kiện chặn chưa được xử lý.") => new()
    {
        Success = false,
        StatusCode = 409,
        Message = detail,
        Blockers = blockers,
        ProblemDetails = new BranchLockConflictProblemDetails(blockers, detail)
    };

    public static BranchLockOperationResult ConcurrencyConflict(string detail = "Dữ liệu chi nhánh đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại trang và thử lại.") => new()
    {
        Success = false,
        StatusCode = 409,
        Message = detail,
        ProblemDetails = new ProblemDetails
        {
            Status = 409,
            Title = "Xung đột dữ liệu đồng thời",
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
        }
    };
}
