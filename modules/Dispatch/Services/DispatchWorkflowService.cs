using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Dispatch.Services;

/// <summary>
/// Triển khai dịch vụ quản lý vòng đời và quy trình xét duyệt đơn điều động nhân sự liên chi nhánh.
/// </summary>
public class DispatchWorkflowService : IDispatchWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IDispatchSyncService _syncService;
    private readonly IDispatchQuotaService _quotaService;

    public DispatchWorkflowService(
        AppDbContext context, 
        IDispatchSyncService syncService,
        IDispatchQuotaService quotaService)
    {
        _context = context;
        _syncService = syncService;
        _quotaService = quotaService;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request)
    {
        if (request.FromStoreId == request.ToStoreId)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể tạo yêu cầu điều động nội bộ trong cùng một chi nhánh.");
        }

        if (request.StartDate > request.EndDate)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Ngày bắt đầu điều động không thể sau ngày kết thúc.");
        }

        int durationDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        if (durationDays > 30)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Thời hạn điều động tạm thời không được vượt quá 30 ngày (Khoảng thời gian bạn chọn: {durationDays} ngày).");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.StartDate < today)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể tạo yêu cầu điều động cho ngày trong quá khứ.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<DispatchRecordDto>.Fail("Vui lòng nhập lý do / công việc cần tăng cường điều động nhân sự.");
        }

        var fromBranch = await _context.Branches.FindAsync((ulong)request.FromStoreId);
        var toBranch = await _context.Branches.FindAsync((ulong)request.ToStoreId);
        if (fromBranch == null || toBranch == null || fromBranch.Status != "ACTIVE" || toBranch.Status != "ACTIVE")
        {
            return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        var employeeIds = (request.EmployeeIds != null && request.EmployeeIds.Any())
            ? request.EmployeeIds.Distinct().ToList()
            : (request.EmployeeId.HasValue && request.EmployeeId.Value > 0
                ? new List<int> { request.EmployeeId.Value }
                : new List<int>());

        if (!employeeIds.Any())
        {
            return ApiResponse<DispatchRecordDto>.Fail("Vui lòng chọn ít nhất một nhân sự đề xuất xin mượn.");
        }

        var ulongIds = employeeIds.Select(id => (ulong)id).ToList();
        var users = await _context.Users
            .Include(u => u.Role)
            .Where(u => ulongIds.Contains(u.Id) && u.Status == "ACTIVE")
            .ToListAsync();

        if (users.Count != employeeIds.Count)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Một số nhân sự được chọn không tồn tại hoặc tài khoản đang bị khóa.");
        }

        var invalidBranchUser = users.FirstOrDefault(u => u.HomeBranchId != (ulong)request.FromStoreId);
        if (invalidBranchUser != null)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Nhân sự '{invalidBranchUser.FullName}' không thuộc biên chế của chi nhánh hỗ trợ ({fromBranch.Name}).");
        }

        // Kiểm tra quân số an toàn của chi nhánh cho mượn qua IDispatchQuotaService
        var quotaCheck = await _quotaService.GetBranchDispatchQuotaAsync(fromBranch.Id, request.StartDate, request.EndDate);
        if (!quotaCheck.Success || quotaCheck.Data == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail(!string.IsNullOrWhiteSpace(quotaCheck.Message) ? quotaCheck.Message : "Không thể xác định hạn mức điều động an toàn của chi nhánh hỗ trợ.");
        }

        if (employeeIds.Count > quotaCheck.Data.AvailableDispatchQuota)
        {
            return ApiResponse<DispatchRecordDto>.Fail(
                $"Chi nhánh {fromBranch.Name} chỉ còn lại {quotaCheck.Data.AvailableDispatchQuota} nhân sự khả dụng. Việc điều động quá số lượng này sẽ làm gián đoạn vận hành của chi nhánh hỗ trợ.");
        }

        var firstUser = users.First();
        var dispatch = new TemporaryDispatch
        {
            UserId = firstUser.Id,
            SourceBranchId = (ulong)request.FromStoreId,
            TargetBranchId = (ulong)request.ToStoreId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Note = request.Reason,
            RequestedBy = (ulong)requesterEmployeeId,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _context.TemporaryDispatches.Add(dispatch);
        await _context.SaveChangesAsync();

        foreach (var u in users)
        {
            _context.DispatchEmployees.Add(new DispatchEmployee
            {
                DispatchId = dispatch.Id,
                UserId = u.Id,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        var createdDispatch = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees)
                .ThenInclude(de => de.User)
                    .ThenInclude(u => u.Role)
            .FirstAsync(d => d.Id == dispatch.Id);

        return ApiResponse<DispatchRecordDto>.Ok(MapToDispatchRecordDto(createdDispatch), "Đã tạo yêu cầu điều động liên chi nhánh thành công.");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.DispatchEmployees)
                .ThenInclude(de => de.User)
                    .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(d => d.Id == (ulong)request.DispatchId);

        if (dispatch == null) return ApiResponse<bool>.Fail("Lệnh điều động không tồn tại.");

        if (dispatch.Status != "PENDING")
        {
            if (dispatch.Status == "RECALLED")
            {
                return ApiResponse<bool>.Fail("Đơn đề nghị điều động này đã được bên gửi thu hồi để chỉnh sửa. Bạn không thể phê duyệt tại thời điểm này.");
            }
            return ApiResponse<bool>.Fail($"Lệnh này đã được xử lý trước đó (Trạng thái hiện tại: {dispatch.Status}).");
        }

        if (!dispatch.DispatchEmployees.Any() && dispatch.UserId > 0)
        {
            var fallbackDe = new DispatchEmployee
            {
                DispatchId = dispatch.Id,
                UserId = dispatch.UserId,
                Status = "PENDING",
                CreatedAt = dispatch.CreatedAt
            };
            dispatch.DispatchEmployees.Add(fallbackDe);
        }

        int approvingCount = 0;
        if (request.EmployeeReviews != null && request.EmployeeReviews.Any())
        {
            approvingCount = request.EmployeeReviews.Count(r => r.IsApproved);
        }
        else if (request.IsApproved == true)
        {
            approvingCount = dispatch.DispatchEmployees.Any() ? dispatch.DispatchEmployees.Count : 1;
        }

        if (approvingCount > 0)
        {
            var quotaCheck = await _quotaService.GetBranchDispatchQuotaAsync(dispatch.SourceBranchId, dispatch.StartDate, dispatch.EndDate, dispatch.Id);
            if (quotaCheck.Success && quotaCheck.Data != null && approvingCount > quotaCheck.Data.AvailableDispatchQuota)
            {
                return ApiResponse<bool>.Fail(
                    $"Chi nhánh {dispatch.SourceBranch?.Name ?? "hỗ trợ"} chỉ còn lại {quotaCheck.Data.AvailableDispatchQuota} nhân sự khả dụng. Việc điều động quá số lượng này sẽ làm gián đoạn vận hành của chi nhánh hỗ trợ.");
            }
        }

        // TRƯỜNG HỢP 1: Duyệt theo danh sách từng nhân sự (EmployeeReviews)
        if (request.EmployeeReviews != null && request.EmployeeReviews.Any())
        {
            foreach (var review in request.EmployeeReviews)
            {
                var targetDe = dispatch.DispatchEmployees.FirstOrDefault(de => de.UserId == (ulong)review.EmployeeId);
                if (targetDe == null) continue;

                if (review.IsApproved)
                {
                    var userToAssign = await _context.Users.FirstOrDefaultAsync(u => u.Id == (ulong)review.EmployeeId && u.Status == "ACTIVE");
                    if (userToAssign == null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự ID #{review.EmployeeId} không tồn tại hoặc đã bị khóa.");
                    }

                    var conflictingShift = await _context.ShiftAssignments
                        .Include(sa => sa.Schedule).ThenInclude(s => s.ShiftTemplate)
                        .FirstOrDefaultAsync(sa => sa.UserId == (ulong)review.EmployeeId
                                                && sa.Schedule.BranchId == dispatch.SourceBranchId
                                                && sa.Schedule.WorkDate >= dispatch.StartDate
                                                && sa.Schedule.WorkDate <= dispatch.EndDate
                                                && sa.Status != "CANCELLED");

                    if (conflictingShift != null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có ca trực '{conflictingShift.Schedule.ShiftTemplate.Name}' ngày {conflictingShift.Schedule.WorkDate:dd/MM/yyyy} tại cơ sở gốc. Vui lòng gỡ ca trước khi phê duyệt.");
                    }

                    var overlapping = await _context.TemporaryDispatches
                        .Include(d => d.DispatchEmployees)
                        .FirstOrDefaultAsync(d => d.Id != dispatch.Id
                                               && d.StartDate <= dispatch.EndDate
                                               && dispatch.StartDate <= d.EndDate
                                               && ((d.Status == "APPROVED" && d.UserId == (ulong)review.EmployeeId)
                                                   || d.DispatchEmployees.Any(de => de.UserId == (ulong)review.EmployeeId && de.Status == "APPROVED")));

                    if (overlapping != null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có một lệnh điều động khác đang có hiệu lực từ {overlapping.StartDate:dd/MM/yyyy} đến {overlapping.EndDate:dd/MM/yyyy}.");
                    }

                    targetDe.Status = "APPROVED";
                    targetDe.ApprovedBy = (ulong)approverEmployeeId;
                    targetDe.Note = review.Note;
                }
                else
                {
                    targetDe.Status = "REJECTED";
                    targetDe.ApprovedBy = (ulong)approverEmployeeId;
                    targetDe.Note = review.Note;
                }
            }

            var approvedCount = dispatch.DispatchEmployees.Count(de => de.Status == "APPROVED");
            var rejectedCount = dispatch.DispatchEmployees.Count(de => de.Status == "REJECTED");

            if (approvedCount > 0 && rejectedCount == 0)
                dispatch.Status = "APPROVED";
            else if (rejectedCount > 0 && approvedCount == 0)
                dispatch.Status = "REJECTED";
            else if (approvedCount > 0 && rejectedCount > 0)
                dispatch.Status = "PARTIAL";
            else
                dispatch.Status = "REJECTED";
        }
        else
        {
            // TRƯỜNG HỢP 2: Duyệt toàn bộ phiếu theo cờ IsApproved
            bool isAllApproved = request.IsApproved ?? false;

            if (!isAllApproved)
            {
                dispatch.Status = "REJECTED";
                foreach (var de in dispatch.DispatchEmployees)
                {
                    de.Status = "REJECTED";
                    de.ApprovedBy = (ulong)approverEmployeeId;
                }
            }
            else
            {
                if (request.AssignedEmployeeId.HasValue && request.AssignedEmployeeId.Value > 0)
                {
                    ulong assignedId = (ulong)request.AssignedEmployeeId.Value;
                    var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == assignedId && u.Status == "ACTIVE");
                    if (assignedUser == null)
                    {
                        return ApiResponse<bool>.Fail("Nhân sự chỉ định điều động không tồn tại hoặc đã bị khóa.");
                    }

                    dispatch.UserId = assignedId;
                    if (dispatch.DispatchEmployees.Any())
                    {
                        var firstDe = dispatch.DispatchEmployees.First();
                        firstDe.UserId = assignedId;
                    }
                    else
                    {
                        dispatch.DispatchEmployees.Add(new DispatchEmployee
                        {
                            DispatchId = dispatch.Id,
                            UserId = assignedId,
                            Status = "PENDING",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                foreach (var de in dispatch.DispatchEmployees)
                {
                    var userToAssign = await _context.Users.FirstOrDefaultAsync(u => u.Id == de.UserId && u.Status == "ACTIVE");
                    if (userToAssign == null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự ID #{de.UserId} không tồn tại hoặc đã bị khóa.");
                    }

                    var conflictingShift = await _context.ShiftAssignments
                        .Include(sa => sa.Schedule).ThenInclude(s => s.ShiftTemplate)
                        .FirstOrDefaultAsync(sa => sa.UserId == de.UserId
                                                && sa.Schedule.BranchId == dispatch.SourceBranchId
                                                && sa.Schedule.WorkDate >= dispatch.StartDate
                                                && sa.Schedule.WorkDate <= dispatch.EndDate
                                                && sa.Status != "CANCELLED");

                    if (conflictingShift != null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có ca trực '{conflictingShift.Schedule.ShiftTemplate.Name}' ngày {conflictingShift.Schedule.WorkDate:dd/MM/yyyy} tại cơ sở gốc. Vui lòng gỡ ca trước khi phê duyệt.");
                    }

                    var overlapping = await _context.TemporaryDispatches
                        .Include(d => d.DispatchEmployees)
                        .FirstOrDefaultAsync(d => d.Id != dispatch.Id
                                               && d.StartDate <= dispatch.EndDate
                                               && dispatch.StartDate <= d.EndDate
                                               && ((d.Status == "APPROVED" && d.UserId == de.UserId)
                                                   || d.DispatchEmployees.Any(x => x.UserId == de.UserId && x.Status == "APPROVED")));

                    if (overlapping != null)
                    {
                        return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có một lệnh điều động khác đang có hiệu lực từ {overlapping.StartDate:dd/MM/yyyy} đến {overlapping.EndDate:dd/MM/yyyy}.");
                    }

                    de.Status = "APPROVED";
                    de.ApprovedBy = (ulong)approverEmployeeId;
                }

                dispatch.Status = "APPROVED";
            }
        }

        dispatch.ApprovedBy = (ulong)approverEmployeeId;
        if (!string.IsNullOrWhiteSpace(request.ApprovalNotes))
        {
            dispatch.Note = string.IsNullOrEmpty(dispatch.Note)
                ? request.ApprovalNotes.Trim()
                : $"{dispatch.Note} | Ghi chú duyệt: {request.ApprovalNotes.Trim()}";
        }

        await _context.SaveChangesAsync();

        // Đồng bộ chi nhánh nhân sự thời gian thực
        await _syncService.SyncDispatchesAsync();

        var statusMsg = dispatch.Status switch
        {
            "APPROVED" => "Đã phê duyệt toàn bộ nhân sự trong lệnh điều động.",
            "PARTIAL" => "Đã xét duyệt lệnh điều động (Một số nhân sự được duyệt, một số bị từ chối).",
            _ => "Đã từ chối lệnh điều động nhân sự."
        };

        return ApiResponse<bool>.Ok(true, statusMsg);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId)
    {
        var dispatches = await _context.TemporaryDispatches
            .Include(d => d.User).ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.User).ThenInclude(u => u.Role)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.ApprovedByUser)
            .Where(d => d.SourceBranchId == (ulong)storeId || d.TargetBranchId == (ulong)storeId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = dispatches.Select(MapToDispatchRecordDto).ToList();
        return ApiResponse<List<DispatchRecordDto>>.Ok(dtos);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<DispatchRecordDto>>> GetAllDispatchesAsync(string? status, int? storeId)
    {
        var query = _context.TemporaryDispatches
            .Include(d => d.User).ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.User).ThenInclude(u => u.Role)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.ApprovedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var cleanStatus = status.Trim().ToUpper();
            query = query.Where(d => d.Status == cleanStatus);
        }

        if (storeId.HasValue && storeId.Value > 0)
        {
            ulong sId = (ulong)storeId.Value;
            query = query.Where(d => d.SourceBranchId == sId || d.TargetBranchId == sId);
        }

        var list = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = list.Select(MapToDispatchRecordDto).ToList();
        return ApiResponse<List<DispatchRecordDto>>.Ok(dtos);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<DispatchRecordDto>> UpdateDispatchRequestAsync(int managerId, int dispatchId, UpdateDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.DispatchEmployees)
            .FirstOrDefaultAsync(d => d.Id == (ulong)dispatchId);

        if (dispatch == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy lệnh điều động cần chỉnh sửa.");
        }

        if (dispatch.Status != "PENDING" && dispatch.Status != "RECALLED")
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Không thể chỉnh sửa đơn đã được xử lý (Trạng thái hiện tại: {dispatch.Status}).");
        }

        var manager = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == (ulong)managerId);
        if (manager == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính người thực hiện.");
        }

        bool isAdmin = manager.Role != null && (manager.Role.RoleCode == "OPERATIONS_ADMIN" || manager.Role.RoleCode == "BUSINESS_OWNER");
        bool isRequester = dispatch.RequestedBy == (ulong)managerId;
        bool isStoreManagerTarget = manager.HomeBranchId.HasValue && manager.HomeBranchId.Value == dispatch.TargetBranchId;

        if (!isAdmin && !isRequester && !isStoreManagerTarget)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Bạn không có quyền chỉnh sửa đơn điều động của chi nhánh khác.");
        }

        if (request.FromStoreId == request.ToStoreId)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể tạo yêu cầu điều động nội bộ trong cùng một chi nhánh.");
        }

        if (request.StartDate > request.EndDate)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Ngày bắt đầu điều động không thể sau ngày kết thúc.");
        }

        int durationDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        if (durationDays > 30)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Thời hạn điều động tạm thời không được vượt quá 30 ngày (Khoảng thời gian bạn chọn: {durationDays} ngày).");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.StartDate < today)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể điều động cho ngày trong quá khứ.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<DispatchRecordDto>.Fail("Vui lòng nhập lý do / công việc cần tăng cường điều động nhân sự.");
        }

        var fromBranch = await _context.Branches.FindAsync((ulong)request.FromStoreId);
        var toBranch = await _context.Branches.FindAsync((ulong)request.ToStoreId);
        if (fromBranch == null || toBranch == null || fromBranch.Status != "ACTIVE" || toBranch.Status != "ACTIVE")
        {
            return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        var employeeIds = (request.EmployeeIds != null && request.EmployeeIds.Any())
            ? request.EmployeeIds.Distinct().ToList()
            : (request.EmployeeId.HasValue && request.EmployeeId.Value > 0
                ? new List<int> { request.EmployeeId.Value }
                : new List<int>());

        if (!employeeIds.Any())
        {
            return ApiResponse<DispatchRecordDto>.Fail("Vui lòng chọn ít nhất một nhân sự đề xuất xin mượn.");
        }

        var ulongIds = employeeIds.Select(id => (ulong)id).ToList();
        var users = await _context.Users
            .Include(u => u.Role)
            .Where(u => ulongIds.Contains(u.Id) && u.Status == "ACTIVE")
            .ToListAsync();

        if (users.Count != employeeIds.Count)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Một số nhân sự không tồn tại hoặc tài khoản đang bị khóa.");
        }

        var invalidBranchUser = users.FirstOrDefault(u => u.HomeBranchId != (ulong)request.FromStoreId);
        if (invalidBranchUser != null)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Nhân sự '{invalidBranchUser.FullName}' không thuộc biên chế của chi nhánh hỗ trợ ({fromBranch.Name}).");
        }

        var quotaCheck = await _quotaService.GetBranchDispatchQuotaAsync(fromBranch.Id, request.StartDate, request.EndDate, dispatch.Id);
        if (!quotaCheck.Success || quotaCheck.Data == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail(!string.IsNullOrWhiteSpace(quotaCheck.Message) ? quotaCheck.Message : "Không thể xác định hạn mức điều động an toàn của chi nhánh hỗ trợ.");
        }

        if (employeeIds.Count > quotaCheck.Data.AvailableDispatchQuota)
        {
            return ApiResponse<DispatchRecordDto>.Fail(
                $"Chi nhánh {fromBranch.Name} chỉ còn lại {quotaCheck.Data.AvailableDispatchQuota} nhân sự khả dụng. Việc điều động quá số lượng này sẽ làm gián đoạn vận hành của chi nhánh hỗ trợ.");
        }

        dispatch.UserId = users.First().Id;
        dispatch.SourceBranchId = (ulong)request.FromStoreId;
        dispatch.TargetBranchId = (ulong)request.ToStoreId;
        dispatch.StartDate = request.StartDate;
        dispatch.EndDate = request.EndDate;
        dispatch.Note = request.Reason;
        dispatch.Status = "PENDING";

        _context.DispatchEmployees.RemoveRange(dispatch.DispatchEmployees);
        foreach (var u in users)
        {
            _context.DispatchEmployees.Add(new DispatchEmployee
            {
                DispatchId = dispatch.Id,
                UserId = u.Id,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        var updated = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.User).ThenInclude(u => u.Role)
            .FirstAsync(d => d.Id == dispatch.Id);

        return ApiResponse<DispatchRecordDto>.Ok(MapToDispatchRecordDto(updated), "Đã chỉnh sửa và gửi lại đơn điều động thành công (đang chờ phê duyệt).");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<DispatchRecordDto>> RecallDispatchRequestAsync(int managerId, int dispatchId, string? reason = null)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(d => d.Id == (ulong)dispatchId);

        if (dispatch == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy lệnh điều động cần thu hồi.");
        }

        if (dispatch.Status != "PENDING")
        {
            if (dispatch.Status == "RECALLED")
            {
                return ApiResponse<DispatchRecordDto>.Fail("Đơn này đã được thu hồi trước đó rồi. Bạn có thể tiến hành chỉnh sửa.");
            }
            return ApiResponse<DispatchRecordDto>.Fail($"Không thể thu hồi đơn đã được xử lý (Trạng thái hiện tại: {dispatch.Status}).");
        }

        var manager = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == (ulong)managerId);
        if (manager == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không xác định được danh tính người thực hiện.");
        }

        bool isAdmin = manager.Role != null && (manager.Role.RoleCode == "OPERATIONS_ADMIN" || manager.Role.RoleCode == "BUSINESS_OWNER");
        bool isRequester = dispatch.RequestedBy == (ulong)managerId;
        bool isStoreManagerTarget = manager.HomeBranchId.HasValue && manager.HomeBranchId.Value == dispatch.TargetBranchId;

        if (!isAdmin && !isRequester && !isStoreManagerTarget)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Bạn không có quyền thu hồi đơn điều động của chi nhánh khác.");
        }

        dispatch.Status = "RECALLED";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            dispatch.Note = string.IsNullOrEmpty(dispatch.Note)
                ? $"[Thu hồi]: {reason.Trim()}"
                : $"{dispatch.Note} | [Thu hồi]: {reason.Trim()}";
        }

        foreach (var de in dispatch.DispatchEmployees)
        {
            de.Status = "PENDING";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<DispatchRecordDto>.Ok(MapToDispatchRecordDto(dispatch), "Đã thu hồi đơn điều động thành công. Bạn có thể chỉnh sửa nội dung và gửi lại.");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> DeleteDispatchRequestAsync(int managerId, int dispatchId)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.DispatchEmployees)
            .FirstOrDefaultAsync(d => d.Id == (ulong)dispatchId);

        if (dispatch == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy lệnh điều động cần hủy.");
        }

        if (dispatch.Status != "PENDING" && dispatch.Status != "RECALLED")
        {
            return ApiResponse<bool>.Fail($"Không thể hủy đơn đã được xử lý (Trạng thái hiện tại: {dispatch.Status}).");
        }

        var manager = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == (ulong)managerId);
        if (manager == null)
        {
            return ApiResponse<bool>.Fail("Không xác định được danh tính người thực hiện.");
        }

        bool isAdmin = manager.Role != null && (manager.Role.RoleCode == "OPERATIONS_ADMIN" || manager.Role.RoleCode == "BUSINESS_OWNER");
        bool isRequester = dispatch.RequestedBy == (ulong)managerId;
        bool isStoreManagerTarget = manager.HomeBranchId.HasValue && manager.HomeBranchId.Value == dispatch.TargetBranchId;

        if (!isAdmin && !isRequester && !isStoreManagerTarget)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền hủy đơn điều động của chi nhánh khác.");
        }

        var pendingSwaps = await _context.Set<ShiftSwapRequest>()
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Where(s => s.RequesterUserId == dispatch.UserId
                     && (s.Status == "PENDING" || s.Status == "PENDING_PEER")
                     && s.RequestingAssignment != null
                     && s.RequestingAssignment.Schedule.BranchId == dispatch.TargetBranchId)
            .ToListAsync();

        foreach (var swap in pendingSwaps)
        {
            swap.Status = "CANCELLED";
        }

        _context.TemporaryDispatches.Remove(dispatch);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã hủy và xóa yêu cầu điều động thành công.");
    }

    private static DispatchRecordDto MapToDispatchRecordDto(TemporaryDispatch d)
    {
        var empList = d.DispatchEmployees != null && d.DispatchEmployees.Any()
            ? d.DispatchEmployees.Select(de => new DispatchEmployeeDto
            {
                Id = (int)de.Id,
                EmployeeId = (int)de.UserId,
                EmployeeName = de.User?.FullName ?? string.Empty,
                EmployeeCode = de.User?.EmployeeCode ?? string.Empty,
                PositionName = de.User?.Role?.RoleName ?? "Nhân viên",
                Status = de.Status,
                ApprovedByName = de.ApprovedByUser?.FullName,
                Note = de.Note
            }).ToList()
            : (d.User != null ? new List<DispatchEmployeeDto>
            {
                new()
                {
                    Id = (int)d.Id,
                    EmployeeId = (int)d.UserId,
                    EmployeeName = d.User.FullName,
                    EmployeeCode = d.User.EmployeeCode,
                    PositionName = d.User.Role?.RoleName ?? "Nhân viên",
                    Status = d.Status,
                    ApprovedByName = d.ApprovedByUser?.FullName,
                    Note = d.Note
                }
            } : new List<DispatchEmployeeDto>());

        string primaryName = empList.Count > 1
            ? $"{empList.Count} nhân sự ({string.Join(", ", empList.Take(2).Select(e => e.EmployeeName))}{(empList.Count > 2 ? "..." : "")})"
            : (empList.FirstOrDefault()?.EmployeeName ?? d.User?.FullName ?? string.Empty);

        string primaryCode = empList.Count > 1
            ? $"{empList.Count} nhân sự"
            : (empList.FirstOrDefault()?.EmployeeCode ?? d.User?.EmployeeCode ?? string.Empty);

        string primaryPos = empList.Count > 1
            ? "Nhiều vị trí"
            : (empList.FirstOrDefault()?.PositionName ?? d.User?.Role?.RoleName ?? string.Empty);

        int primaryId = empList.FirstOrDefault()?.EmployeeId ?? (d.UserId > 0 ? (int)d.UserId : 0);

        string? requestReason = d.Note;
        string? approvalNote = null;
        if (!string.IsNullOrEmpty(d.Note) && d.Note.Contains(" | Ghi chú duyệt: "))
        {
            var parts = d.Note.Split(new[] { " | Ghi chú duyệt: " }, StringSplitOptions.None);
            requestReason = parts[0];
            approvalNote = parts.Length > 1 ? parts[1] : null;
        }

        return new DispatchRecordDto
        {
            DispatchId = (int)d.Id,
            EmployeeId = primaryId,
            EmployeeName = primaryName,
            EmployeeCode = primaryCode,
            PositionName = primaryPos,
            FromStoreId = (int)d.SourceBranchId,
            FromStoreName = d.SourceBranch?.Name ?? string.Empty,
            ToStoreId = (int)d.TargetBranchId,
            ToStoreName = d.TargetBranch?.Name ?? string.Empty,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            Reason = requestReason,
            Note = d.Note,
            ApprovalNotes = approvalNote,
            Status = d.Status,
            RequestedByName = d.RequestedByUser?.FullName ?? string.Empty,
            ApprovedByName = d.ApprovedByUser?.FullName,
            CreatedAt = d.CreatedAt,
            Employees = empList
        };
    }
}
