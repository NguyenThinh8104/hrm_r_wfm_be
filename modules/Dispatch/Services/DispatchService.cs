using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Dispatch.Services;

public class DispatchService : IDispatchService
{
    private readonly AppDbContext _context;

    public DispatchService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tạo phiếu đề nghị chi viện nhân sự liên chi nhánh (UC 4.1).
    /// </summary>
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

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.StartDate < today)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể tạo yêu cầu điều động cho ngày trong quá khứ.");
        }

        var fromBranch = await _context.Branches.FindAsync((ulong)request.FromStoreId);
        var toBranch = await _context.Branches.FindAsync((ulong)request.ToStoreId);
        if (fromBranch == null || toBranch == null || fromBranch.Status != "ACTIVE" || toBranch.Status != "ACTIVE")
        {
            return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy nhân sự hoặc tài khoản nhân sự đang bị khóa.");
        }

        if (user.HomeBranchId != (ulong)request.FromStoreId)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Nhân sự '{user.FullName}' không thuộc biên chế của chi nhánh hỗ trợ ({fromBranch.Name}).");
        }

        var requester = await _context.Users.FindAsync((ulong)requesterEmployeeId);

        var dispatch = new TemporaryDispatch
        {
            UserId = (ulong)request.EmployeeId,
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

        return ApiResponse<DispatchRecordDto>.Ok(new DispatchRecordDto
        {
            DispatchId = (int)dispatch.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PositionName = user.Role?.RoleName ?? string.Empty,
            FromStoreId = (int)fromBranch.Id,
            FromStoreName = fromBranch.Name,
            ToStoreId = (int)toBranch.Id,
            ToStoreName = toBranch.Name,
            StartDate = dispatch.StartDate,
            EndDate = dispatch.EndDate,
            Reason = dispatch.Note,
            Status = dispatch.Status,
            RequestedByName = requester?.FullName ?? "",
            CreatedAt = dispatch.CreatedAt
        }, "Đã tạo yêu cầu điều động liên chi nhánh thành công.");
    }

    /// <summary>
    /// Xét duyệt và chỉ định nhân viên điều động (UC 4.2).
    /// </summary>
    public async Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.User)
                .ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .FirstOrDefaultAsync(d => d.Id == (ulong)request.DispatchId);

        if (dispatch == null) return ApiResponse<bool>.Fail("Lệnh điều động không tồn tại.");

        if (dispatch.Status != "PENDING") return ApiResponse<bool>.Fail($"Lệnh này đã được xử lý trước đó (Trạng thái hiện tại: {dispatch.Status}).");

        if (!request.IsApproved)
        {
            dispatch.Status = "REJECTED";
            dispatch.ApprovedBy = (ulong)approverEmployeeId;
            if (!string.IsNullOrWhiteSpace(request.ApprovalNotes))
            {
                dispatch.Note = string.IsNullOrEmpty(dispatch.Note) 
                    ? $"Từ chối: {request.ApprovalNotes.Trim()}" 
                    : $"{dispatch.Note} | Lý do từ chối: {request.ApprovalNotes.Trim()}";
            }

            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Đã từ chối lệnh điều động.");
        }

        // Trường hợp Phê duyệt (APPROVED)
        ulong targetUserId = request.AssignedEmployeeId.HasValue && request.AssignedEmployeeId.Value > 0
            ? (ulong)request.AssignedEmployeeId.Value
            : dispatch.UserId;

        var userToAssign = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.Status == "ACTIVE");

        if (userToAssign == null)
        {
            return ApiResponse<bool>.Fail("Nhân sự chỉ định điều động không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        if (userToAssign.HomeBranchId != dispatch.SourceBranchId)
        {
            return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' không thuộc biên chế của chi nhánh hỗ trợ ({dispatch.SourceBranch.Name}).");
        }

        // Ràng buộc kiểm tra xung đột ca trực tại chi nhánh gốc trong khoảng [StartDate, EndDate]
        var conflictingShift = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .FirstOrDefaultAsync(sa => sa.UserId == targetUserId
                                    && sa.Schedule.BranchId == dispatch.SourceBranchId
                                    && sa.Schedule.WorkDate >= dispatch.StartDate
                                    && sa.Schedule.WorkDate <= dispatch.EndDate
                                    && sa.Status != "CANCELLED");

        if (conflictingShift != null)
        {
            return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có ca trực '{conflictingShift.Schedule.ShiftTemplate.Name}' ngày {conflictingShift.Schedule.WorkDate:dd/MM/yyyy} tại cơ sở gốc. Vui lòng gỡ ca trước khi phê duyệt điều động.");
        }

        // Ràng buộc kiểm tra không trùng lệnh điều động APPROVED khác trong cùng thời gian
        var overlappingDispatch = await _context.TemporaryDispatches
            .FirstOrDefaultAsync(d => d.Id != dispatch.Id
                                   && d.UserId == targetUserId
                                   && d.Status == "APPROVED"
                                   && d.StartDate <= dispatch.EndDate
                                   && dispatch.StartDate <= d.EndDate);

        if (overlappingDispatch != null)
        {
            return ApiResponse<bool>.Fail($"Nhân sự '{userToAssign.FullName}' đã có một lệnh điều động khác đang có hiệu lực trong khoảng từ {overlappingDispatch.StartDate:dd/MM/yyyy} đến {overlappingDispatch.EndDate:dd/MM/yyyy}.");
        }

        dispatch.UserId = targetUserId;
        dispatch.Status = "APPROVED";
        dispatch.ApprovedBy = (ulong)approverEmployeeId;
        if (!string.IsNullOrWhiteSpace(request.ApprovalNotes))
        {
            dispatch.Note = string.IsNullOrEmpty(dispatch.Note) 
                ? request.ApprovalNotes.Trim() 
                : $"{dispatch.Note} | Ghi chú duyệt: {request.ApprovalNotes.Trim()}";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, $"Đã phê duyệt điều động nhân sự '{userToAssign.FullName}' sang chi nhánh {dispatch.TargetBranch.Name} từ ngày {dispatch.StartDate:dd/MM/yyyy} đến {dispatch.EndDate:dd/MM/yyyy}.");
    }

    /// <summary>
    /// Lấy danh sách lệnh điều động theo chi nhánh (gồm cả chiều cho mượn và chiều nhận).
    /// </summary>
    public async Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId)
    {
        var dispatches = await _context.TemporaryDispatches
            .Include(d => d.User)
                .ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Where(d => d.SourceBranchId == (ulong)storeId || d.TargetBranchId == (ulong)storeId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DispatchRecordDto
            {
                DispatchId = (int)d.Id,
                EmployeeId = (int)d.UserId,
                EmployeeName = d.User.FullName,
                EmployeeCode = d.User.EmployeeCode,
                PositionName = d.User.Role != null ? d.User.Role.RoleName : string.Empty,
                FromStoreId = (int)d.SourceBranchId,
                FromStoreName = d.SourceBranch.Name,
                ToStoreId = (int)d.TargetBranchId,
                ToStoreName = d.TargetBranch.Name,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                Reason = d.Note,
                Status = d.Status,
                RequestedByName = d.RequestedByUser.FullName,
                ApprovedByName = d.ApprovedByUser != null ? d.ApprovedByUser.FullName : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<DispatchRecordDto>>.Ok(dispatches);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách lệnh điều động toàn hệ thống có hỗ trợ lọc (UC 4.4).
    /// </summary>
    public async Task<ApiResponse<List<DispatchRecordDto>>> GetAllDispatchesAsync(string? status, int? storeId)
    {
        var query = _context.TemporaryDispatches
            .Include(d => d.User)
                .ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
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
            .Select(d => new DispatchRecordDto
            {
                DispatchId = (int)d.Id,
                EmployeeId = (int)d.UserId,
                EmployeeName = d.User.FullName,
                EmployeeCode = d.User.EmployeeCode,
                PositionName = d.User.Role != null ? d.User.Role.RoleName : string.Empty,
                FromStoreId = (int)d.SourceBranchId,
                FromStoreName = d.SourceBranch.Name,
                ToStoreId = (int)d.TargetBranchId,
                ToStoreName = d.TargetBranch.Name,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                Reason = d.Note,
                Status = d.Status,
                RequestedByName = d.RequestedByUser.FullName,
                ApprovedByName = d.ApprovedByUser != null ? d.ApprovedByUser.FullName : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<DispatchRecordDto>>.Ok(list);
    }

    /// <summary>
    /// Giám sát chỉ số điều động toàn mạng lưới (Executive Dashboard & Audit Metrics - UC 4.4).
    /// </summary>
    public async Task<ApiResponse<DispatchNetworkMetricsDto>> GetNetworkMetricsAsync(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var query = _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.User)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(d => d.EndDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(d => d.StartDate <= toDate.Value);
        }

        var allDispatches = await query.ToListAsync();

        var total = allDispatches.Count;
        var pending = allDispatches.Count(d => d.Status == "PENDING");
        var approved = allDispatches.Count(d => d.Status == "APPROVED");
        var rejected = allDispatches.Count(d => d.Status == "REJECTED");
        var activeToday = allDispatches.Count(d => d.Status == "APPROVED" && d.StartDate <= today && today <= d.EndDate);

        // Tính ma trận điều động theo cặp chi nhánh (Source -> Target)
        var pairMatrix = allDispatches
            .Where(d => d.Status == "APPROVED")
            .GroupBy(d => new { d.SourceBranchId, SourceName = d.SourceBranch.Name, d.TargetBranchId, TargetName = d.TargetBranch.Name })
            .Select(g => new StorePairDispatchMatrixDto
            {
                FromStoreId = (int)g.Key.SourceBranchId,
                FromStoreName = g.Key.SourceName,
                ToStoreId = (int)g.Key.TargetBranchId,
                ToStoreName = g.Key.TargetName,
                DispatchCount = g.Count(),
                // Ước tính số giờ công chi viện (mỗi ngày điều động tương đương trung bình 8 giờ làm việc)
                TotalHours = g.Sum(d => (d.EndDate.DayNumber - d.StartDate.DayNumber + 1) * 8.0)
            })
            .OrderByDescending(p => p.DispatchCount)
            .ToList();

        var totalHours = pairMatrix.Sum(p => p.TotalHours);

        var recentList = await _context.TemporaryDispatches
            .Include(d => d.User)
                .ThenInclude(u => u.Role)
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Select(d => new DispatchRecordDto
            {
                DispatchId = (int)d.Id,
                EmployeeId = (int)d.UserId,
                EmployeeName = d.User.FullName,
                EmployeeCode = d.User.EmployeeCode,
                PositionName = d.User.Role != null ? d.User.Role.RoleName : string.Empty,
                FromStoreId = (int)d.SourceBranchId,
                FromStoreName = d.SourceBranch.Name,
                ToStoreId = (int)d.TargetBranchId,
                ToStoreName = d.TargetBranch.Name,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                Reason = d.Note,
                Status = d.Status,
                RequestedByName = d.RequestedByUser.FullName,
                ApprovedByName = d.ApprovedByUser != null ? d.ApprovedByUser.FullName : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        var metricsDto = new DispatchNetworkMetricsDto
        {
            TotalDispatches = total,
            PendingCount = pending,
            ApprovedCount = approved,
            RejectedCount = rejected,
            ActiveTodayCount = activeToday,
            TotalDispatchedHours = totalHours,
            StorePairMatrix = pairMatrix,
            RecentDispatches = recentList
        };

        return ApiResponse<DispatchNetworkMetricsDto>.Ok(metricsDto, "Lấy chỉ số điều động toàn chuỗi thành công.");
    }

    /// <summary>
    /// Chỉnh sửa đơn đề nghị chi viện đang chờ duyệt (PENDING) theo cơ chế cập nhật trực tiếp tại chỗ (Cách 1).
    /// </summary>
    public async Task<ApiResponse<DispatchRecordDto>> UpdateDispatchRequestAsync(int managerId, int dispatchId, UpdateDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(d => d.Id == (ulong)dispatchId);

        if (dispatch == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy lệnh điều động cần chỉnh sửa.");
        }

        if (dispatch.Status != "PENDING")
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Không thể chỉnh sửa đơn đã được xử lý (Trạng thái hiện tại: {dispatch.Status}).");
        }

        // Kiểm tra quyền hạn: Chỉ người tạo đơn, Quản lý chi nhánh mượn, hoặc Admin mới được sửa
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

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.StartDate < today)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không thể điều động cho ngày trong quá khứ.");
        }

        var fromBranch = await _context.Branches.FindAsync((ulong)request.FromStoreId);
        var toBranch = await _context.Branches.FindAsync((ulong)request.ToStoreId);
        if (fromBranch == null || toBranch == null || fromBranch.Status != "ACTIVE" || toBranch.Status != "ACTIVE")
        {
            return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng chỉ định không tồn tại hoặc đã ngừng hoạt động.");
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == (ulong)request.EmployeeId && u.Status == "ACTIVE");

        if (user == null)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy nhân sự hoặc tài khoản nhân sự đang bị khóa.");
        }

        if (user.HomeBranchId != (ulong)request.FromStoreId)
        {
            return ApiResponse<DispatchRecordDto>.Fail($"Nhân sự '{user.FullName}' không thuộc biên chế của chi nhánh hỗ trợ ({fromBranch.Name}).");
        }

        // Cập nhật trực tiếp trên đơn hiện tại và giữ nguyên trạng thái PENDING
        dispatch.UserId = (ulong)request.EmployeeId;
        dispatch.SourceBranchId = (ulong)request.FromStoreId;
        dispatch.TargetBranchId = (ulong)request.ToStoreId;
        dispatch.StartDate = request.StartDate;
        dispatch.EndDate = request.EndDate;
        dispatch.Note = request.Reason;
        // Status vẫn giữ PENDING để bên cơ sở hỗ trợ tiếp tục thẩm định và duyệt

        await _context.SaveChangesAsync();

        var requester = await _context.Users.FindAsync(dispatch.RequestedBy);

        return ApiResponse<DispatchRecordDto>.Ok(new DispatchRecordDto
        {
            DispatchId = (int)dispatch.Id,
            EmployeeId = (int)user.Id,
            EmployeeName = user.FullName,
            EmployeeCode = user.EmployeeCode,
            PositionName = user.Role?.RoleName ?? string.Empty,
            FromStoreId = (int)fromBranch.Id,
            FromStoreName = fromBranch.Name,
            ToStoreId = (int)toBranch.Id,
            ToStoreName = toBranch.Name,
            StartDate = dispatch.StartDate,
            EndDate = dispatch.EndDate,
            Reason = dispatch.Note,
            Status = dispatch.Status,
            RequestedByName = requester?.FullName ?? "",
            CreatedAt = dispatch.CreatedAt
        }, "Đã chỉnh sửa và lưu lại đơn điều động thành công (đang chờ phê duyệt).");
    }

    /// <summary>
    /// Hủy / Xóa đơn đề nghị chi viện khi đang chờ duyệt (PENDING).
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteDispatchRequestAsync(int managerId, int dispatchId)
    {
        var dispatch = await _context.TemporaryDispatches.FirstOrDefaultAsync(d => d.Id == (ulong)dispatchId);

        if (dispatch == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy lệnh điều động cần hủy.");
        }

        if (dispatch.Status != "PENDING")
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

        _context.TemporaryDispatches.Remove(dispatch);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Đã hủy và xóa yêu cầu điều động thành công.");
    }
}
