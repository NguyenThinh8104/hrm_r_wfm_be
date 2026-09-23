using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Modules.Auth.DTOs;

/// <summary>
/// DTO tải lên tệp tin Excel để import nhân sự hàng loạt.
/// </summary>
public class BulkImportEmployeeRequestDto
{
    [Required(ErrorMessage = "Vui lòng đính kèm tệp tin danh sách nhân sự (.xlsx / .xls / .csv).")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Mã chi nhánh mặc định áp dụng cho các dòng không chỉ định rõ chi nhánh trong file.
    /// </summary>
    public ulong? DefaultBranchId { get; set; }

    /// <summary>
    /// Mã đơn mở rộng định biên (nếu đợt import này bổ sung nhân sự vượt định biên chuẩn).
    /// </summary>
    public ulong? ImportRequestId { get; set; }

    /// <summary>
    /// Lý do giải trình mở rộng định biên (bắt buộc khi import vượt định biên chuẩn).
    /// </summary>
    public string? ExpansionReason { get; set; }
}

/// <summary>
/// DTO mô tả thông tin 1 dòng lỗi trong quá trình import.
/// </summary>
public class ImportRowErrorDto
{
    public int RowNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? FullName { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// DTO tổng kết báo cáo kết quả import nhân sự hàng loạt.
/// </summary>
public class BulkImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ImportRowErrorDto> Errors { get; set; } = new();
    public List<EmployeeDetailDto> SuccessEmployees { get; set; } = new();
}

/// <summary>
/// DTO lưu trữ dữ liệu parse từ một dòng Excel.
/// </summary>
public class ParsedEmployeeRow
{
    public int RowNumber { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string BranchIdentifier { get; set; } = string.Empty;
    public string Password { get; set; } = "Password@123";
}
