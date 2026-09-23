using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;
using Shared.Services;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ Quản lý Định biên Nhân sự chi nhánh & Thẩm định mở rộng chỉ tiêu.
/// </summary>
public class BranchHeadcountService : IBranchHeadcountService, IStoreHeadcountService
{
    private readonly AppDbContext _context;
    private readonly IS3StorageService _s3StorageService;
    private readonly ILogger<BranchHeadcountService> _logger;

    public BranchHeadcountService(
        AppDbContext context,
        IS3StorageService s3StorageService,
        ILogger<BranchHeadcountService> logger)
    {
        _context = context;
        _s3StorageService = s3StorageService;
        _logger = logger;
    }

    // =========================================================================
    // 1. Thẩm định Định biên Chuẩn & Trừ lùi Chỉ tiêu khi tạo Nhân sự (IBranchHeadcountService)
    // =========================================================================

    public async Task<HeadcountValidationResult> ValidateAndConsumeQuotaAsync(
        ulong branchId,
        ulong? importRequestId,
        string? expansionReason,
        ulong actorId)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return HeadcountValidationResult.Fail("Chi nhánh cửa hàng chỉ định không tồn tại.");
        }

        if (branch.Status != "ACTIVE")
        {
            return HeadcountValidationResult.Fail($"Chi nhánh '{branch.Name}' đã ngừng hoạt động, không thể tiếp nhận nhân sự.");
        }

        // Đếm số lượng nhân sự đang hoạt động (Status == 'ACTIVE')
        // Lưu ý: Cơ chế bù đắp định biên (Headcount Attrition Compensation):
        // Khi 1 nhân viên nghỉ việc (Status -> INACTIVE), currentActiveCount tự động giảm.
        // Khoảng trống dôi ra cho phép Admin tạo bù ngay nhân sự mới trong giới hạn Tier Quota mà không cần đơn Import!
        var currentActiveCount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.Status == "ACTIVE");

        var standardQuota = HeadcountConstants.GetStandardQuota(branch.BranchTier);

        // Trường hợp 1: Trong phạm vi định biên chuẩn (hoặc bù đắp vị trí nhân viên nghỉ việc)
        if (currentActiveCount < standardQuota)
        {
            return HeadcountValidationResult.Success(
                isOverride: false,
                importRequestId: null,
                currentCount: currentActiveCount,
                quota: standardQuota);
        }

        // Trường hợp 2: Chi nhánh đã đạt hoặc vượt định biên chuẩn -> Bắt buộc phải có đơn mở rộng hợp lệ
        if (!importRequestId.HasValue || importRequestId.Value == 0)
        {
            return HeadcountValidationResult.Fail(
                $"Chi nhánh '{branch.Name}' đã đạt giới hạn định biên quy định theo phân cấp ({currentActiveCount}/{standardQuota} nhân sự). Để tạo thêm nhân sự vượt định biên, Cửa hàng trưởng phải gửi đơn Import Excel và Quản trị viên bắt buộc phải chọn đơn hợp lệ kèm lý do giải trình.",
                currentActiveCount, standardQuota);
        }

        if (string.IsNullOrWhiteSpace(expansionReason))
        {
            return HeadcountValidationResult.Fail(
                "Vui lòng nhập lý do giải trình mở rộng định biên (ExpansionReason) khi tạo nhân sự vượt chỉ tiêu chi nhánh.",
                currentActiveCount, standardQuota);
        }

        var request = await _context.HeadcountImportRequests
            .FirstOrDefaultAsync(r => r.Id == importRequestId.Value);

        if (request == null)
        {
            return HeadcountValidationResult.Fail("Đơn đề xuất mở rộng định biên chỉ định không tồn tại trong hệ thống.", currentActiveCount, standardQuota);
        }

        if (request.BranchId != branchId)
        {
            return HeadcountValidationResult.Fail("Đơn đề xuất mở rộng định biên không thuộc chi nhánh này.", currentActiveCount, standardQuota);
        }

        // Kiểm tra thời hạn hiệu lực của đơn (Quota Expiration)
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value < DateTime.UtcNow)
        {
            if (request.Status != HeadcountConstants.StatusExpired)
            {
                request.Status = HeadcountConstants.StatusExpired;
                request.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return HeadcountValidationResult.Fail(
                $"Đơn đề xuất mở rộng định biên đã hết hạn sử dụng vào ngày {request.ExpiresAt.Value:dd/MM/yyyy HH:mm}. Vui lòng yêu cầu Cửa hàng trưởng tạo đơn mới.",
                currentActiveCount, standardQuota);
        }

        // Đơn phải ở trạng thái APPROVED hoặc PENDING (khi Admin trực tiếp kích hoạt sử dụng)
        if (request.Status != HeadcountConstants.StatusApproved && request.Status != HeadcountConstants.StatusPending)
        {
            return HeadcountValidationResult.Fail(
                $"Đơn đề xuất đang ở trạng thái '{request.Status}', không thể sử dụng để tạo nhân sự.",
                currentActiveCount, standardQuota);
        }

        // Nếu đơn đang PENDING, khi Admin tạo nhân sự dùng đơn này thì tự động duyệt với ApprovedQuantity = TotalRequested
        if (request.Status == HeadcountConstants.StatusPending)
        {
            request.Status = HeadcountConstants.StatusApproved;
            if (request.ApprovedQuantity == 0)
            {
                request.ApprovedQuantity = request.TotalRequested;
                request.AdditionalQuantity = request.TotalRequested;
            }
            request.ReviewedBy = actorId;
            request.ReviewedAt = DateTime.UtcNow;
            if (!request.ExpiresAt.HasValue)
            {
                request.ExpiresAt = DateTime.UtcNow.AddDays(HeadcountConstants.DefaultExpirationDays);
            }
        }

        if (request.AdditionalQuantity <= 0)
        {
            request.Status = HeadcountConstants.StatusExhausted;
            request.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return HeadcountValidationResult.Fail("Đơn đề xuất mở rộng định biên này đã sử dụng hết toàn bộ chỉ tiêu được duyệt.", currentActiveCount, standardQuota);
        }

        // Trừ lùi số lượng chỉ tiêu khả dụng
        request.AdditionalQuantity--;
        request.TotalApproved++;

        // Khi số lượng khả dụng về 0, chuyển trạng thái đơn sang EXHAUSTED
        if (request.AdditionalQuantity == 0)
        {
            request.Status = HeadcountConstants.StatusExhausted;
        }

        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Đã sử dụng 1 suất từ đơn mở rộng định biên #{RequestId} cho chi nhánh #{BranchId}. Còn lại: {Remaining}",
            request.Id, branchId, request.AdditionalQuantity);

        return HeadcountValidationResult.Success(
            isOverride: true,
            importRequestId: request.Id,
            currentCount: currentActiveCount,
            quota: standardQuota,
            remaining: request.AdditionalQuantity);
    }

    // =========================================================================
    // 2. Các nghiệp vụ Tra cứu, Upload & Phê duyệt Đơn mở rộng (IStoreHeadcountService)
    // =========================================================================

    public async Task<ApiResponse<BranchHeadcountStatusDto>> GetBranchHeadcountStatusAsync(ulong branchId)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<BranchHeadcountStatusDto>.Fail("Không tìm thấy chi nhánh cửa hàng.");
        }

        var currentHeadcount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.Status == "ACTIVE");

        var inactiveCount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.Status == "INACTIVE");

        var standardQuota = HeadcountConstants.GetStandardQuota(branch.BranchTier);

        var pendingCount = await _context.HeadcountImportRequests
            .CountAsync(r => r.BranchId == branchId && r.Status == HeadcountConstants.StatusPending);

        var now = DateTime.UtcNow;
        var availableSlots = await _context.HeadcountImportRequests
            .Where(r => r.BranchId == branchId 
                     && r.Status == HeadcountConstants.StatusApproved
                     && r.AdditionalQuantity > 0
                     && (!r.ExpiresAt.HasValue || r.ExpiresAt.Value > now))
            .SumAsync(r => r.AdditionalQuantity);

        var result = new BranchHeadcountStatusDto
        {
            BranchId = branch.Id,
            BranchCode = branch.BranchCode,
            BranchName = branch.Name,
            BranchTierName = branch.BranchTier.ToString(),
            BranchTierValue = (int)branch.BranchTier,
            StandardQuota = standardQuota,
            CurrentHeadcount = currentHeadcount,
            InactiveCount = inactiveCount,
            PendingRequestsCount = pendingCount,
            AvailableOverrideSlots = availableSlots
        };

        return ApiResponse<BranchHeadcountStatusDto>.Ok(result);
    }

    public async Task<ApiResponse<HeadcountImportRequestDto>> SubmitImportRequestAsync(
        ulong managerId,
        ulong? managerBranchId,
        UploadHeadcountRequestDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Vui lòng đính kèm tệp tin Excel đề xuất định biên (.xlsx).");
        }

        if (dto.TotalRequested <= 0 && dto.RequestedQuantity.HasValue && dto.RequestedQuantity.Value > 0)
        {
            dto.TotalRequested = dto.RequestedQuantity.Value;
        }

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls" && ext != ".csv" && ext != ".pdf")
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Định dạng tệp tin không hợp lệ. Hệ thống chấp nhận file Excel (.xlsx, .xls, .csv) hoặc tài liệu PDF (.pdf).");
        }

        // Ràng buộc chi nhánh: Nếu tài khoản là Store Manager thì bắt buộc upload cho chi nhánh mình quản lý
        if (managerBranchId.HasValue && managerBranchId.Value > 0 && managerBranchId.Value != dto.BranchId)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Bạn chỉ có quyền gửi đề xuất mở rộng định biên cho chi nhánh mình đang quản lý.");
        }

        var branch = await _context.Branches.FindAsync(dto.BranchId);
        if (branch == null || branch.Status != "ACTIVE")
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Chi nhánh chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        var manager = await _context.Users.FindAsync(managerId);
        if (manager == null)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Không xác định được danh tính người gửi đề xuất.");
        }

        string uploadedFilePath;
        try
        {
            if (ext == ".pdf")
            {
                uploadedFilePath = await _s3StorageService.UploadPdfAsync(dto.File, "headcount-requests");
            }
            else
            {
                uploadedFilePath = await _s3StorageService.UploadFileAsync(dto.File, "headcount-requests");
            }
        }
        catch (ArgumentException aex)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail(aex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu trữ file đề xuất định biên");
            return ApiResponse<HeadcountImportRequestDto>.Fail("Lỗi hệ thống khi tải lên tệp tin. Vui lòng thử lại sau.");
        }

        var request = new HeadcountImportRequest
        {
            BranchId = dto.BranchId,
            RequestedBy = managerId,
            FilePath = uploadedFilePath,
            FileName = dto.File.FileName,
            Status = HeadcountConstants.StatusPending,
            TotalRequested = dto.TotalRequested,
            ApprovedQuantity = 0,
            TotalApproved = 0,
            AdditionalQuantity = dto.TotalRequested,
            Reason = dto.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.HeadcountImportRequests.Add(request);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Cửa hàng trưởng {ManagerName} (NV #{ManagerId}) đã gửi đơn đề xuất mở rộng định biên #{RequestId} (+{Total} NV) cho chi nhánh {BranchName}",
            manager.FullName, managerId, request.Id, request.TotalRequested, branch.Name);

        return ApiResponse<HeadcountImportRequestDto>.Ok(
            MapToDto(request, branch, manager, null),
            "Gửi đề xuất mở rộng định biên thành công. Đơn đang chờ Operations Admin thẩm định.");
    }

    public async Task<ApiResponse<HeadcountImportRequestDto>> ReviewRequestAsync(
        ulong requestId,
        ReviewHeadcountRequestDto dto,
        ulong adminId)
    {
        var request = await _context.HeadcountImportRequests
            .Include(r => r.Branch)
            .Include(r => r.RequestedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Không tìm thấy đơn đề xuất mở rộng định biên.");
        }

        // Cho phép thẩm định đơn PENDING hoặc tái thẩm định/phê duyệt lại đơn từng bị REJECTED trước đó
        if (request.Status != HeadcountConstants.StatusPending && request.Status != HeadcountConstants.StatusRejected)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail($"Đơn này đã được xử lý trước đó (Trạng thái hiện tại: {request.Status}).");
        }

        var admin = await _context.Users.FindAsync(adminId);

        // Trường hợp 1: Từ chối đơn (Reject Request)
        if (!dto.IsApproved)
        {
            if (string.IsNullOrWhiteSpace(dto.AdminNotes))
            {
                return ApiResponse<HeadcountImportRequestDto>.Fail("Vui lòng nhập lý do từ chối để thông báo cho Cửa hàng trưởng.");
            }

            request.Status = HeadcountConstants.StatusRejected;
            request.ApprovedQuantity = 0;
            request.AdditionalQuantity = 0;
            request.ExpiresAt = null;
            request.AdminNotes = dto.AdminNotes.Trim();
            request.ReviewedBy = adminId;
            request.ReviewedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ApiResponse<HeadcountImportRequestDto>.Ok(
                MapToDto(request, request.Branch, request.RequestedByUser, admin),
                "Đã từ chối đơn đề xuất mở rộng định biên.");
        }

        // Trường hợp 2: Phê duyệt (Hỗ trợ Duyệt một phần Partial Approval hoặc Duyệt toàn bộ)
        int approvedQuantity = (dto.ApprovedQuantity.HasValue && dto.ApprovedQuantity.Value > 0)
            ? dto.ApprovedQuantity.Value
            : request.TotalRequested;

        int expirationDays = (dto.ExpirationDays.HasValue && dto.ExpirationDays.Value > 0)
            ? dto.ExpirationDays.Value
            : HeadcountConstants.DefaultExpirationDays;

        // Ưu tiên thời điểm hết hạn cụ thể từ frontend gửi lên nếu hợp lệ
        DateTime targetExpiry;
        if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value > DateTime.UtcNow)
        {
            targetExpiry = dto.ExpiresAt.Value;
        }
        else
        {
            targetExpiry = DateTime.UtcNow.AddDays(expirationDays);
        }

        request.Status = HeadcountConstants.StatusApproved;
        request.ApprovedQuantity = approvedQuantity;
        request.AdditionalQuantity = approvedQuantity; // Khởi tạo số lượng khả dụng
        request.ExpiresAt = targetExpiry;
        request.AdminNotes = dto.AdminNotes?.Trim();
        request.ReviewedBy = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var message = approvedQuantity < request.TotalRequested
            ? $"Đã phê duyệt một phần đơn mở rộng định biên: Cho phép tăng +{approvedQuantity}/{request.TotalRequested} nhân sự (Hạn dùng đến {targetExpiry:dd/MM/yyyy})."
            : $"Đã phê duyệt toàn bộ đơn mở rộng định biên: Cho phép tăng +{approvedQuantity} nhân sự (Hạn dùng đến {targetExpiry:dd/MM/yyyy}).";

        return ApiResponse<HeadcountImportRequestDto>.Ok(
            MapToDto(request, request.Branch, request.RequestedByUser, admin),
            message);
    }

    public async Task<ApiResponse<bool>> CloseOrExpireRequestAsync(
        ulong requestId,
        ulong adminId,
        string reason)
    {
        var request = await _context.HeadcountImportRequests.FindAsync(requestId);
        if (request == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn mở rộng định biên.");
        }

        if (request.Status == HeadcountConstants.StatusExhausted || request.Status == HeadcountConstants.StatusClosed)
        {
            return ApiResponse<bool>.Fail($"Đơn đã kết thúc trước đó (Trạng thái: {request.Status}).");
        }

        request.Status = HeadcountConstants.StatusClosed;
        request.AdminNotes = string.IsNullOrWhiteSpace(request.AdminNotes)
            ? $"Đóng đơn thủ công: {reason.Trim()}"
            : $"{request.AdminNotes} | Đóng đơn: {reason.Trim()}";
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã đóng đơn mở rộng định biên thành công.");
    }

    public async Task<ApiResponse<List<HeadcountImportRequestDto>>> GetAvailableRequestsForBranchAsync(ulong branchId)
    {
        var now = DateTime.UtcNow;
        var requests = await _context.HeadcountImportRequests
            .Include(r => r.Branch)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.BranchId == branchId
                     && (r.Status == HeadcountConstants.StatusApproved || r.Status == HeadcountConstants.StatusPending)
                     && r.AdditionalQuantity > 0
                     && (!r.ExpiresAt.HasValue || r.ExpiresAt.Value > now))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = requests.Select(r => MapToDto(r, r.Branch, r.RequestedByUser, r.ReviewedByUser)).ToList();
        return ApiResponse<List<HeadcountImportRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<HeadcountImportRequestDto>>> GetAllRequestsAsync(string? status, ulong? branchId)
    {
        var query = _context.HeadcountImportRequests
            .Include(r => r.Branch)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var cleanStatus = status.Trim().ToUpper();
            query = query.Where(r => r.Status == cleanStatus);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var dtos = list.Select(r => MapToDto(r, r.Branch, r.RequestedByUser, r.ReviewedByUser)).ToList();
        return ApiResponse<List<HeadcountImportRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<HeadcountImportRequestDto>> GetRequestByIdAsync(ulong id)
    {
        var request = await _context.HeadcountImportRequests
            .Include(r => r.Branch)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return ApiResponse<HeadcountImportRequestDto>.Fail("Không tìm thấy đơn mở rộng định biên.");
        }

        return ApiResponse<HeadcountImportRequestDto>.Ok(
            MapToDto(request, request.Branch, request.RequestedByUser, request.ReviewedByUser));
    }

    private HeadcountImportRequestDto MapToDto(
        HeadcountImportRequest entity,
        Branch branch,
        User requester,
        User? reviewer)
    {
        var ext = Path.GetExtension(entity.FilePath).ToLowerInvariant();
        var mime = ext == ".pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        var viewUrl = _s3StorageService.GetPresignedViewUrl(entity.FilePath, mime)
                   ?? $"/api/v1/headcount-requests/{entity.Id}/view";
        var downloadUrl = $"/api/v1/headcount-requests/{entity.Id}/download";

        return new HeadcountImportRequestDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            BranchCode = branch?.BranchCode ?? string.Empty,
            BranchName = branch?.Name ?? string.Empty,
            RequestedBy = entity.RequestedBy,
            RequesterName = requester?.FullName ?? string.Empty,
            RequesterEmployeeCode = requester?.EmployeeCode ?? string.Empty,
            FilePath = entity.FilePath,
            FileName = entity.FileName,
            ViewUrl = viewUrl,
            DownloadUrl = downloadUrl,
            Status = entity.Status,
            TotalRequested = entity.TotalRequested,
            ApprovedQuantity = entity.ApprovedQuantity,
            TotalApproved = entity.TotalApproved,
            AdditionalQuantity = entity.AdditionalQuantity,
            Reason = entity.Reason,
            AdminNotes = entity.AdminNotes,
            ReviewedBy = entity.ReviewedBy,
            ReviewerName = reviewer?.FullName,
            ReviewedAt = entity.ReviewedAt,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
