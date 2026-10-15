using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;
using Shared.Security;
using Shared.Services;

namespace Modules.Auth.Services;

public class EmployeeImportService : IEmployeeImportService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IBranchHeadcountService _headcountService;
    private readonly ILogger<EmployeeImportService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmployeeImportService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IBranchHeadcountService headcountService,
        ILogger<EmployeeImportService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _headcountService = headcountService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateEmployeeImportTemplateAsync(int count = 5)
    {
        count = Math.Clamp(count, 1, 500);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Danh_Sach_Nhan_Su");

        var headers = new[]
        {
            "STT",
            "Mã Nhân Viên (*)",
            "Họ Và Tên (*)",
            "Email (*)",
            "Số Điện Thoại (*)",
            "Mã Vai Trò (*)",
            "Hình Thức (FULL_TIME / PART_TIME)",
            "Mã Chi Nhánh (*)",
            "Mật Khẩu Khởi Tạo"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Sinh N mã nhân viên tự động không trùng lặp dựa trên dữ liệu DB hiện có
        var existingCodes = await _context.Users
            .AsNoTracking()
            .Select(u => u.EmployeeCode)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        int maxNumber = 0;
        foreach (var code in existingCodes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            var trimmed = code.Trim().ToUpper();
            string numPart = "";
            if (trimmed.StartsWith("NV-"))
            {
                numPart = trimmed.Substring(3);
            }
            else if (trimmed.StartsWith("NV"))
            {
                numPart = trimmed.Substring(2);
            }

            if (int.TryParse(numPart, out var parsedNum))
            {
                if (parsedNum > maxNumber) maxNumber = parsedNum;
            }
        }

        int currentNum = maxNumber > 0 ? maxNumber : 1000;
        var generatedCodes = new List<string>();
        while (generatedCodes.Count < count)
        {
            currentNum++;
            var candidate = $"NV{currentNum:D4}";
            if (!existingSet.Contains(candidate))
            {
                generatedCodes.Add(candidate);
                existingSet.Add(candidate);
            }
        }

        // Tạo sẵn các hàng tương ứng với số lượng nhân viên Admin đã chọn
        for (int i = 0; i < generatedCodes.Count; i++)
        {
            int rowIdx = i + 2;
            worksheet.Cell(rowIdx, 1).Value = i + 1; // STT
            worksheet.Cell(rowIdx, 2).Value = generatedCodes[i]; // Mã Nhân Viên sinh tự động
            // Cột 3 (Họ Và Tên), 4 (Email), 5 (SĐT), 6 (Mã Vai Trò), 8 (Mã Chi Nhánh) để trống cho Admin điền
            worksheet.Cell(rowIdx, 7).Value = "FULL_TIME";
            worksheet.Cell(rowIdx, 9).Value = "Password@123";

            worksheet.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(rowIdx, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(rowIdx, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        worksheet.Columns().AdjustToContents();

        // Sheet 2: Danh mục & Hướng dẫn
        var guideSheet = workbook.Worksheets.Add("Huong_Dan_Va_Danh_Muc");
        guideSheet.Cell(1, 1).Value = "DANH MỤC VAI TRÒ CỬA HÀNG HỢP LỆ";
        guideSheet.Cell(1, 1).Style.Font.Bold = true;
        guideSheet.Cell(2, 1).Value = "Mã Vai Trò";
        guideSheet.Cell(2, 2).Value = "Tên Chức Danh";
        guideSheet.Range(2, 1, 2, 2).Style.Font.Bold = true;
        guideSheet.Range(2, 1, 2, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var rolesData = new[]
        {
            ("STORE_MANAGER", "Cửa hàng trưởng"),
            ("SHIFT_LEADER", "Trưởng ca vận hành"),
            ("CASHIER", "Nhân viên thu ngân"),
            ("SALES_STAFF", "Nhân viên bán hàng / Quầy kệ"),
            ("SECURITY_GUARD", "Nhân viên an ninh / Bảo vệ")
        };

        for (int r = 0; r < rolesData.Length; r++)
        {
            guideSheet.Cell(3 + r, 1).Value = rolesData[r].Item1;
            guideSheet.Cell(3 + r, 2).Value = rolesData[r].Item2;
        }

        // Lấy danh sách chi nhánh hiện có
        var activeBranches = await _context.Branches
            .Where(b => b.Status == "ACTIVE")
            .OrderBy(b => b.BranchCode)
            .ToListAsync();

        int startBranchRow = 10;
        guideSheet.Cell(startBranchRow, 1).Value = "DANH SÁCH MÃ CHI NHÁNH HOẠT ĐỘNG (DÙNG ĐIỀN CỘT MÃ CHI NHÁNH)";
        guideSheet.Cell(startBranchRow, 1).Style.Font.Bold = true;
        guideSheet.Cell(startBranchRow + 1, 1).Value = "Mã Chi Nhánh (BranchCode)";
        guideSheet.Cell(startBranchRow + 1, 2).Value = "Tên Cửa Hàng";
        guideSheet.Cell(startBranchRow + 1, 3).Value = "Quy Mô (Tier)";
        guideSheet.Range(startBranchRow + 1, 1, startBranchRow + 1, 3).Style.Font.Bold = true;
        guideSheet.Range(startBranchRow + 1, 1, startBranchRow + 1, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        for (int b = 0; b < activeBranches.Count; b++)
        {
            guideSheet.Cell(startBranchRow + 2 + b, 1).Value = activeBranches[b].BranchCode;
            guideSheet.Cell(startBranchRow + 2 + b, 2).Value = activeBranches[b].Name;
            guideSheet.Cell(startBranchRow + 2 + b, 3).Value = activeBranches[b].BranchTier.ToString();
        }

        guideSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_Import_Nhan_Su.xlsx");
    }

    public async Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateImportErrorReportAsync(List<ImportRowErrorDto> errors, string? sourceFileName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Bao_Cao_Loi");

        // Tiêu đề báo cáo
        worksheet.Cell(1, 1).Value = "BÁO CÁO CHI TIẾT LỖI IMPORT NHÂN SỰ";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#DC2626");

        var cleanSource = string.IsNullOrWhiteSpace(sourceFileName) ? "Danh_sach_nhan_su.xlsx" : sourceFileName;
        worksheet.Cell(2, 1).Value = $"Tệp tin gốc: {cleanSource}   |   Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}   |   Tổng số lỗi: {errors?.Count ?? 0} dòng vi phạm";
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontSize = 10;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#4B5563");

        // Headers
        var headers = new[]
        {
            "STT",
            "Dòng Excel",
            "Mã Nhân Viên",
            "Họ Và Tên",
            "Email",
            "Chi Tiết Lý Do Lỗi",
            "Thời Gian Ghi Nhận"
        };

        int headerRow = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(headerRow, i + 1).Value = headers[i];
        }

        var headerRange = worksheet.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DC2626");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        int currentRow = headerRow + 1;
        int stt = 1;
        var currentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        if (errors != null && errors.Count > 0)
        {
            foreach (var err in errors)
            {
                worksheet.Cell(currentRow, 1).Value = stt;
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(currentRow, 2).Value = $"Dòng {err.RowNumber}";
                worksheet.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 2).Style.Font.Bold = true;

                worksheet.Cell(currentRow, 3).Value = !string.IsNullOrWhiteSpace(err.EmployeeCode) ? err.EmployeeCode : "—";
                worksheet.Cell(currentRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(currentRow, 3).Style.Font.Bold = true;

                worksheet.Cell(currentRow, 4).Value = !string.IsNullOrWhiteSpace(err.FullName) ? err.FullName : "—";

                worksheet.Cell(currentRow, 5).Value = !string.IsNullOrWhiteSpace(err.Email) ? err.Email : "—";

                worksheet.Cell(currentRow, 6).Value = err.ErrorMessage;
                worksheet.Cell(currentRow, 6).Style.Font.FontColor = XLColor.FromHtml("#B91C1C");
                worksheet.Cell(currentRow, 6).Style.Alignment.WrapText = true;

                worksheet.Cell(currentRow, 7).Value = currentTime;
                worksheet.Cell(currentRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Tô màu so le zebra striping
                if (stt % 2 == 0)
                {
                    worksheet.Range(currentRow, 1, currentRow, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
                }

                currentRow++;
                stt++;
            }

            var dataRange = worksheet.Range(headerRow + 1, 1, currentRow - 1, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // Độ rộng cột
        worksheet.Column(1).Width = 8;
        worksheet.Column(2).Width = 14;
        worksheet.Column(3).Width = 18;
        worksheet.Column(4).Width = 24;
        worksheet.Column(5).Width = 28;
        worksheet.Column(6).Width = 72;
        worksheet.Column(7).Width = 22;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outFileName = $"Bao_Cao_Loi_Import_{timestamp}.xlsx";

        return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", outFileName);
    }

    public async Task<ApiResponse<BulkImportResultDto>> BulkImportEmployeesAsync(
        BulkImportEmployeeRequestDto dto,
        ulong actorId,
        string actorRole,
        string? ipAddress)
    {
        var normalizedActorRole = actorRole.ToUpper();
        if (normalizedActorRole != "OPERATIONS_ADMIN" && normalizedActorRole != "OPERATIONSADMIN" && normalizedActorRole != "ADMIN")
        {
            return ApiResponse<BulkImportResultDto>.Fail(
                "Chỉ duy nhất Quản trị viên vận hành (OPERATIONS_ADMIN / ADMIN) mới có quyền thực hiện import nhân sự hàng loạt.");
        }

        if (dto.File == null || dto.File.Length == 0)
        {
            return ApiResponse<BulkImportResultDto>.Fail("Vui lòng đính kèm tệp tin danh sách nhân sự (.xlsx / .xls / .csv).");
        }

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls" && ext != ".csv")
        {
            return ApiResponse<BulkImportResultDto>.Fail("Định dạng tệp tin không hợp lệ. Chỉ chấp nhận định dạng .xlsx, .xls hoặc .csv.");
        }

        if (ext == ".xls")
        {
            return ApiResponse<BulkImportResultDto>.Fail("Hệ thống chỉ hỗ trợ định dạng Excel mới (.xlsx) hoặc .csv. Vui lòng mở tệp tin bằng Microsoft Excel và lưu lại thành (.xlsx).");
        }

        // 1. Phân tích các dòng từ file
        var parsedRows = new List<ParsedEmployeeRow>();
        try
        {
            if (ext == ".csv")
            {
                using var reader = new StreamReader(dto.File.OpenReadStream());
                int lineIndex = 0;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineIndex++;
                    if (lineIndex == 1) continue; // Bỏ qua tiêu đề
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Hỗ trợ cả dấu phẩy (,) và dấu chấm phẩy (;)
                    var delimiter = line.Contains(';') ? ';' : ',';
                    var parts = line.Split(delimiter);
                    if (parts.Length < 6) continue;

                    parsedRows.Add(new ParsedEmployeeRow
                    {
                        RowNumber = lineIndex,
                        EmployeeCode = parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty,
                        FullName = parts.Length > 2 ? parts[2].Trim().Trim('"') : string.Empty,
                        Email = parts.Length > 3 ? parts[3].Trim().Trim('"') : string.Empty,
                        Phone = parts.Length > 4 ? parts[4].Trim().Trim('"') : string.Empty,
                        RoleCode = parts.Length > 5 ? parts[5].Trim().Trim('"') : string.Empty,
                        EmploymentType = parts.Length > 6 && !string.IsNullOrWhiteSpace(parts[6]) ? parts[6].Trim().Trim('"') : "FULL_TIME",
                        BranchIdentifier = parts.Length > 7 ? parts[7].Trim().Trim('"') : string.Empty,
                        Password = parts.Length > 8 && !string.IsNullOrWhiteSpace(parts[8]) ? parts[8].Trim().Trim('"') : "Password@123"
                    });
                }
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await dto.File.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var workbook = new XLWorkbook(memoryStream);
                var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Danh_Sach_Nhan_Su", StringComparison.OrdinalIgnoreCase))
                             ?? workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    return ApiResponse<BulkImportResultDto>.Fail("Tệp tin Excel không chứa trang tính (worksheet) nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1); // Bỏ qua tiêu đề
                foreach (var r in rows)
                {
                    int rowNum = r.RowNumber();
                    var code = GetCellString(r.Cell(2));
                    var name = GetCellString(r.Cell(3));
                    var email = GetCellString(r.Cell(4));
                    var phone = GetCellString(r.Cell(5));
                    var role = GetCellString(r.Cell(6));
                    var type = GetCellString(r.Cell(7));
                    var branch = GetCellString(r.Cell(8));
                    var pass = GetCellString(r.Cell(9));

                    // Nếu cả hàng đều trống thì bỏ qua
                    if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name) &&
                        string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
                    {
                        continue;
                    }

                    parsedRows.Add(new ParsedEmployeeRow
                    {
                        RowNumber = rowNum,
                        EmployeeCode = code,
                        FullName = name,
                        Email = email,
                        Phone = phone,
                        RoleCode = role,
                        EmploymentType = string.IsNullOrWhiteSpace(type) ? "FULL_TIME" : type,
                        BranchIdentifier = branch,
                        Password = string.IsNullOrWhiteSpace(pass) ? "Password@123" : pass
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đọc file import nhân sự");
            return ApiResponse<BulkImportResultDto>.Fail($"Không thể đọc nội dung tệp tin: {ex.Message}");
        }

        if (parsedRows.Count == 0)
        {
            return ApiResponse<BulkImportResultDto>.Fail("Không tìm thấy dữ liệu nhân sự nào trong tệp tin.");
        }

        var result = new BulkImportResultDto
        {
            TotalRows = parsedRows.Count
        };

        // 2. Pre-fetch danh mục
        var existingUsers = await _context.Users.AsNoTracking().ToListAsync();
        var existingCodes = new HashSet<string>(existingUsers.Select(u => u.EmployeeCode.ToUpper()), StringComparer.OrdinalIgnoreCase);
        var existingEmails = new HashSet<string>(existingUsers.Select(u => u.Email.ToLower()), StringComparer.OrdinalIgnoreCase);
        var existingPhones = new HashSet<string>(existingUsers.Select(u => u.Phone), StringComparer.OrdinalIgnoreCase);

        var roles = await _context.Roles.AsNoTracking().ToListAsync();
        var rolesByCode = roles.ToDictionary(r => r.RoleCode.ToUpper(), r => r, StringComparer.OrdinalIgnoreCase);

        var branches = await _context.Branches.AsNoTracking().ToListAsync();
        var branchesByCode = branches.ToDictionary(b => b.BranchCode.ToUpper(), b => b, StringComparer.OrdinalIgnoreCase);
        var branchesById = branches.ToDictionary(b => b.Id, b => b);

        var validStoreRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STORE_MANAGER", "SHIFT_LEADER", "CASHIER", "SALES_STAFF", "SECURITY_GUARD"
        };

        var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmailsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhonesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validRowsToProcess = new List<(ParsedEmployeeRow Row, Role Role, Branch Branch)>();

        // 3. Validation từng dòng
        foreach (var r in parsedRows)
        {
            if (string.IsNullOrWhiteSpace(r.EmployeeCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, FullName = r.FullName, Email = r.Email, ErrorMessage = "Mã nhân viên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.FullName))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, Email = r.Email, ErrorMessage = "Họ và tên không được để trống." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.Email))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, Email = r.Email, ErrorMessage = "Email không được để trống." });
                continue;
            }

            if (!r.Email.Contains("@") || !r.Email.Contains("."))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Email '{r.Email}' không đúng định dạng." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(r.Phone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = r.EmployeeCode, FullName = r.FullName, Email = r.Email, ErrorMessage = "Số điện thoại không được để trống." });
                continue;
            }

            var normalizedCode = r.EmployeeCode.Trim().ToUpper();
            var normalizedEmail = r.Email.Trim().ToLower();
            var normalizedPhone = r.Phone.Trim();

            // Kiểm tra trùng lặp trong tệp tin
            if (seenCodesInFile.Contains(normalizedCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Mã nhân viên '{normalizedCode}' bị trùng lặp trong tệp tin." });
                continue;
            }
            if (seenEmailsInFile.Contains(normalizedEmail))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Email '{normalizedEmail}' bị trùng lặp trong tệp tin." });
                continue;
            }
            if (seenPhonesInFile.Contains(normalizedPhone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Số điện thoại '{normalizedPhone}' bị trùng lặp trong tệp tin." });
                continue;
            }

            // Kiểm tra trùng lặp với CSDL
            if (existingCodes.Contains(normalizedCode))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Mã nhân viên '{normalizedCode}' đã tồn tại trong hệ thống." });
                continue;
            }
            if (existingEmails.Contains(normalizedEmail))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Email '{normalizedEmail}' đã được sử dụng trong hệ thống." });
                continue;
            }
            if (existingPhones.Contains(normalizedPhone))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Số điện thoại '{normalizedPhone}' đã được sử dụng trong hệ thống." });
                continue;
            }

            // Kiểm tra Vai trò
            var normalizedRoleCode = r.RoleCode.Trim().ToUpper();
            if (!validStoreRoles.Contains(normalizedRoleCode) || !rolesByCode.TryGetValue(normalizedRoleCode, out var targetRole))
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Vai trò '{r.RoleCode}' không hợp lệ. Chỉ chấp nhận: STORE_MANAGER, SHIFT_LEADER, CASHIER, SALES_STAFF, SECURITY_GUARD." });
                continue;
            }

            // Xác định Chi nhánh
            Branch? targetBranch = null;
            var branchStr = r.BranchIdentifier.Trim();
            if (!string.IsNullOrWhiteSpace(branchStr))
            {
                if (branchesByCode.TryGetValue(branchStr.ToUpper(), out var bByCode))
                {
                    targetBranch = bByCode;
                }
                else if (ulong.TryParse(branchStr, out var bId) && branchesById.TryGetValue(bId, out var bById))
                {
                    targetBranch = bById;
                }
            }
            else if (dto.DefaultBranchId.HasValue && branchesById.TryGetValue(dto.DefaultBranchId.Value, out var defaultB))
            {
                targetBranch = defaultB;
            }

            if (targetBranch == null)
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Không tìm thấy chi nhánh '{r.BranchIdentifier}'." });
                continue;
            }

            if (targetBranch.Status != "ACTIVE")
            {
                result.Errors.Add(new ImportRowErrorDto { RowNumber = r.RowNumber, EmployeeCode = normalizedCode, FullName = r.FullName, Email = r.Email, ErrorMessage = $"Chi nhánh '{targetBranch.Name}' đã ngừng hoạt động." });
                continue;
            }

            seenCodesInFile.Add(normalizedCode);
            seenEmailsInFile.Add(normalizedEmail);
            seenPhonesInFile.Add(normalizedPhone);

            validRowsToProcess.Add((r, targetRole, targetBranch));
        }

        // Danh sách các chi nhánh đã có Cửa hàng trưởng ACTIVE
        var branchesWithActiveManagerList = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role.RoleCode == "STORE_MANAGER" && u.Status == "ACTIVE" && u.HomeBranchId != null)
            .Select(u => u.HomeBranchId!.Value)
            .ToListAsync();
        var branchesWithActiveManager = branchesWithActiveManagerList.ToHashSet();

        // 4. Thẩm định định biên (Effective Quota) và tạo nhân sự cho các dòng hợp lệ
        var newUsersToInsert = new List<User>();
        var branchBatchCounter = new Dictionary<ulong, int>();
        var emailQueueItems = new List<(string Email, string FullName, string Code, string RoleName, string BranchName, string PlainPass)>();

        foreach (var (row, role, branch) in validRowsToProcess)
        {
            // Kiểm tra ràng buộc duy nhất 1 Cửa hàng trưởng trên mỗi chi nhánh
            if (role.RoleCode == "STORE_MANAGER")
            {
                if (branchesWithActiveManager.Contains(branch.Id))
                {
                    result.Errors.Add(new ImportRowErrorDto
                    {
                        RowNumber = row.RowNumber,
                        EmployeeCode = row.EmployeeCode,
                        FullName = row.FullName,
                        Email = row.Email,
                        ErrorMessage = $"Chi nhánh '{branch.Name}' hiện đã có Cửa hàng trưởng đang hoạt động. Mỗi chi nhánh chỉ được phép có tối đa 1 Cửa hàng trưởng."
                    });
                    continue;
                }
                branchesWithActiveManager.Add(branch.Id);
            }

            branchBatchCounter.TryGetValue(branch.Id, out var currentBatchCount);
            var quotaCheck = await _headcountService.ValidateHeadcountAsync(branch.Id, currentBatchCount);

            if (!quotaCheck.IsSuccess)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    EmployeeCode = row.EmployeeCode,
                    FullName = row.FullName,
                    Email = row.Email,
                    ErrorMessage = quotaCheck.ErrorMessage ?? $"Chi nhánh '{branch.Name}' đã đạt trần định biên nhân sự."
                });
                continue;
            }

            branchBatchCounter[branch.Id] = currentBatchCount + 1;

            var normalizedCode = row.EmployeeCode.Trim().ToUpper();
            var normalizedEmail = row.Email.Trim().ToLower();
            var normalizedPhone = row.Phone.Trim();
            var plainPass = string.IsNullOrWhiteSpace(row.Password) ? "Password@123" : row.Password.Trim();

            var user = new User
            {
                EmployeeCode = normalizedCode,
                FullName = row.FullName.Trim(),
                Email = normalizedEmail,
                Phone = normalizedPhone,
                PasswordHash = _passwordHasher.Hash(plainPass),
                RoleId = role.Id,
                EmploymentType = string.Equals(row.EmploymentType, "PART_TIME", StringComparison.OrdinalIgnoreCase) ? "PART_TIME" : "FULL_TIME",
                HomeBranchId = branch.Id,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            newUsersToInsert.Add(user);
            emailQueueItems.Add((normalizedEmail, row.FullName.Trim(), normalizedCode, role.RoleName, branch.Name, plainPass));

            result.SuccessEmployees.Add(new EmployeeDetailDto
            {
                Id = user.Id,
                EmployeeCode = user.EmployeeCode,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = role.Id,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName,
                EmploymentType = user.EmploymentType,
                HomeBranchId = branch.Id,
                BranchCode = branch.BranchCode,
                BranchName = branch.Name,
                Status = user.Status,
                CreatedAt = user.CreatedAt
            });

            // Thêm vào existing list để ngăn ngừa dòng phía sau trùng nếu có
            existingCodes.Add(normalizedCode);
            existingEmails.Add(normalizedEmail);
            existingPhones.Add(normalizedPhone);
        }

        // 5. Lưu vào CSDL
        if (newUsersToInsert.Count > 0)
        {
            _context.Users.AddRange(newUsersToInsert);
            await _context.SaveChangesAsync();

            await LogAuditAsync(actorId, "BULK_IMPORT_EMPLOYEES", "users", 0, null, new
            {
                TotalImported = newUsersToInsert.Count
            }, ipAddress);

            // Tự động gửi Welcome Email thông báo tài khoản & mật khẩu khởi tạo cho nhân sự mới trong nền
            if (emailQueueItems.Count > 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EmployeeImportService>>();

                        foreach (var item in emailQueueItems)
                        {
                            try
                            {
                                await scopedEmailService.SendWelcomeEmailAsync(
                                    item.Email,
                                    item.FullName,
                                    item.Code,
                                    item.RoleName,
                                    item.BranchName,
                                    item.PlainPass
                                );
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Lỗi gửi welcome email cho nhân sự import {Email} ({Code})", item.Email, item.Code);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi thực thi tiến trình gửi email ngầm sau khi import nhân sự");
                    }
                    await Task.Delay(2500);
                });
            }
        }

        result.SuccessCount = newUsersToInsert.Count;
        result.FailureCount = result.Errors.Count;

        var message = result.SuccessCount > 0
            ? $"Import nhân sự hoàn tất: {result.SuccessCount} thành công (hệ thống đang tự động gửi email chào mừng kèm thông tin đăng nhập), {result.FailureCount} lỗi."
            : $"Import nhân sự hoàn tất: {result.SuccessCount} thành công, {result.FailureCount} lỗi.";
        return ApiResponse<BulkImportResultDto>.Ok(result, message);
    }

    private async Task LogAuditAsync(ulong actorId, string action, string targetTable, ulong targetId, object? oldValues, object? newValues, string? ipAddress)
    {
        try
        {
            var audit = new SystemAuditLog
            {
                ActorId = actorId,
                Action = action,
                TargetTable = targetTable,
                TargetId = targetId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.SystemAuditLogs.Add(audit);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Tránh crash nếu audit logging gặp lỗi tạm thời
        }
    }

    private static string GetCellString(IXLCell cell)
    {
        return cell.Value.ToString().Trim();
    }
}
