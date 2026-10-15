using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Domain.Constants;
using Domain.Entities;
using Modules.Shifts.Common;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;

namespace Modules.Shifts.Services;

/// <summary>
/// Dịch vụ quản lý quy trình đổi / chuyển ca trực và xác thực đồng nghiệp 2 bước (UC 2.2).
/// </summary>
public class ShiftSwapService : IShiftSwapService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ShiftSwapService> _logger;

    public ShiftSwapService(
        AppDbContext context,
        IEmailService emailService,
        ILogger<ShiftSwapService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Gửi yêu cầu đổi ca trực hoặc chuyển ca (nhờ làm thay) giữa các nhân viên.
    /// <summary>
    /// Gửi yêu cầu đổi ca trực, chuyển ca (nhờ làm thay) hoặc xin nghỉ ca (gửi Cửa hàng trưởng xếp lại).
    /// </summary>
    public async Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
    {
        var requestingAssignment = await _context.ShiftAssignments
            .Include(sa => sa.User)
            .Include(sa => sa.AssignedRole)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.AssignmentId && sa.UserId == (ulong)requesterEmployeeId);

        if (requestingAssignment == null)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của bạn để yêu cầu đổi/chuyển/nghỉ.");
        }

        if (requestingAssignment.Schedule?.Branch != null && !string.Equals(requestingAssignment.Schedule.Branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail($"Chi nhánh '{requestingAssignment.Schedule.Branch.Name}' đã bị khóa. Không thể tạo đơn mới.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var minAllowedDate = today.AddDays(1); // RÀNG BUỘC: Phải báo trước ít nhất 1 ngày (chỉ cho phép từ ngày mai trở đi)
        if (requestingAssignment.Schedule.WorkDate < minAllowedDate)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Đơn xin đổi/nghỉ ca phải được gửi trước ngày làm việc ít nhất 1 ngày. Không thể tạo đơn cho ca trực hôm nay hoặc trong quá khứ.");
        }

        // Kiểm tra xem ca này đã có đơn chờ duyệt chưa (bao gồm cả đơn chờ đồng nghiệp xác nhận)
        var hasPending = await _context.ShiftSwapRequests
            .AnyAsync(r => (r.RequestingAssignmentId == requestingAssignment.Id || (r.TargetAssignmentId != null && r.TargetAssignmentId == requestingAssignment.Id))
                        && (r.Status == "PENDING" || r.Status == "PENDING_PEER"));
        if (hasPending)
        {
            return ApiResponse<ShiftSwapRequestDto>.Fail("Ca trực này hiện đang có đơn xin đổi/chuyển/nghỉ ca đang chờ xử lý.");
        }

        // VALIDATE BIÊN THỜI GIAN BIỆT PHÁI: Nếu ca trực thuộc chi nhánh khác HomeBranch,
        // kiểm tra nhân viên có lệnh biệt phái hợp lệ bao phủ ngày ca trực không.
        var requesterUser = requestingAssignment.User;
        var assignmentBranchId = requestingAssignment.Schedule.BranchId;
        if (requesterUser != null && assignmentBranchId != requesterUser.HomeBranchId)
        {
            var hasValidDispatch = await _context.Set<Domain.Entities.TemporaryDispatch>()
                .AnyAsync(d => d.UserId == (ulong)requesterEmployeeId
                            && d.TargetBranchId == assignmentBranchId
                            && d.Status == "APPROVED"
                            && d.StartDate <= requestingAssignment.Schedule.WorkDate
                            && d.EndDate >= requestingAssignment.Schedule.WorkDate);
            if (!hasValidDispatch)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Ngày ca trực nằm ngoài thời gian điều chuyển hợp lệ của bạn tại chi nhánh này.");
            }
        }

        var reqType = (request.RequestType ?? "SWAP").ToUpper().Trim();
        User? targetUser = null;
        ShiftAssignment? targetAssignment = null;

        if (reqType == "LEAVE")
        {
            // TH1: Xin nghỉ không có người làm thay -> Gửi Cửa hàng trưởng
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng nhập lý do xin nghỉ ca trực để Cửa hàng trưởng xem xét.");
            }
        }
        else if (reqType == "TRANSFER")
        {
            // TH2a: Chuyển ca 1 chiều - nhờ đồng nghiệp làm thay
            if (!request.TargetEmployeeId.HasValue || request.TargetEmployeeId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn hoặc nhập mã nhân viên đồng nghiệp đồng ý làm thay.");
            }

            targetUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == (ulong)request.TargetEmployeeId.Value);

            if (targetUser == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy thông tin nhân viên được nhờ làm thay.");
            }

            if (targetUser.Id == (ulong)requesterEmployeeId)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể gửi đơn nhờ làm thay cho chính bản thân bạn.");
            }

            // Kiểm tra trùng ca trong cùng ngày của đồng nghiệp
            var targetHasScheduleConflict = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.ScheduleId == requestingAssignment.ScheduleId && sa.Status != "CANCELLED");
            if (targetHasScheduleConflict)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Đồng nghiệp đã có phân công trong cùng khung ca trực này.");
            }

            var targetHasSameDayShift = await _context.ShiftAssignments
                .AnyAsync(sa => sa.UserId == targetUser.Id && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate && sa.Status != "CANCELLED");
            if (targetHasSameDayShift)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có lịch trực ca khác trong ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể tiếp nhận ca này.");
            }
        }
        else
        {
            // TH2b: Đổi ca 2 chiều (SWAP)
            if (!request.TargetEmployeeId.HasValue || request.TargetEmployeeId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn đồng nghiệp để thực hiện đổi ca.");
            }

            targetUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == (ulong)request.TargetEmployeeId.Value);

            if (targetUser == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy thông tin đồng nghiệp được chọn.");
            }

            if (targetUser.Id == (ulong)requesterEmployeeId)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không thể đổi ca với chính bản thân bạn.");
            }

            // RÀNG BUỘC: Chỉ những người có cùng vai trò mới được đổi lịch cho nhau
            if (requestingAssignment.User.RoleId != targetUser.RoleId)
            {
                var reqRoleName = requestingAssignment.AssignedRole?.RoleName ?? requestingAssignment.User.Role?.RoleName ?? "Vai trò hiện tại";
                var targetRoleName = targetUser.Role?.RoleName ?? "Vai trò khác";
                return ApiResponse<ShiftSwapRequestDto>.Fail($"Chỉ những nhân viên có cùng vai trò mới được đổi lịch cho nhau. Bạn là '{reqRoleName}', còn đồng nghiệp là '{targetRoleName}'.");
            }

            if (!request.TargetAssignmentId.HasValue || request.TargetAssignmentId.Value <= 0)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Vui lòng chọn ca trực của đồng nghiệp để tráo đổi.");
            }

            targetAssignment = await _context.ShiftAssignments
                .Include(sa => sa.User)
                .Include(sa => sa.AssignedRole)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .FirstOrDefaultAsync(sa => sa.Id == (ulong)request.TargetAssignmentId.Value && sa.UserId == targetUser.Id);

            if (targetAssignment == null)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Không tìm thấy ca trực của đồng nghiệp để đổi.");
            }

            if (targetAssignment.Id == requestingAssignment.Id)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Hai ca trực xin đổi không thể là cùng một ca.");
            }

            // RÀNG BUỘC: Ca đồng nghiệp cũng phải trước ít nhất 1 ngày
            if (targetAssignment.Schedule.WorkDate < minAllowedDate)
            {
                return ApiResponse<ShiftSwapRequestDto>.Fail("Ca trực của đồng nghiệp được chọn phải diễn ra sau ngày hôm nay ít nhất 1 ngày.");
            }

            bool isSameDaySwap = requestingAssignment.Schedule.WorkDate == targetAssignment.Schedule.WorkDate;

            if (isSameDaySwap)
            {
                // 1. Chặn nếu cùng một khung ca (ScheduleId)
                if (requestingAssignment.ScheduleId == targetAssignment.ScheduleId)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail("Hai nhân viên đang cùng trực chung một khung ca, không thể tráo đổi cho nhau.");
                }

                // 2. Đổi cùng ngày: Bỏ qua kiểm tra trùng ngày của 2 ca đang đổi.
                // Chỉ kiểm tra xem requester có ca THỨ 3 nào khác trong ngày không
                var requesterHasOtherShift = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == (ulong)requesterEmployeeId 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
                if (requesterHasOtherShift)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Bạn đã có ca trực khác chưa hoàn tất trong ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}.");
                }

                // Chỉ kiểm tra xem đồng nghiệp có ca THỨ 3 nào khác trong ngày không
                var targetHasOtherShift = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == targetUser.Id 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == targetAssignment.Schedule.WorkDate);
                if (targetHasOtherShift)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có ca trực khác chưa hoàn tất trong ngày {targetAssignment.Schedule.WorkDate:dd/MM/yyyy}.");
                }
            }
            else
            {
                // Đổi ca khác ngày: Kiểm tra xung đột ngày chéo giữa 2 bên
                var requesterConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == (ulong)requesterEmployeeId 
                                 && sa.Id != requestingAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == targetAssignment.Schedule.WorkDate);
                if (requesterConflict)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Bạn đã có ca trực khác ngày {targetAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể tiếp nhận ca của đồng nghiệp.");
                }

                var targetConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == targetUser.Id 
                                 && sa.Id != targetAssignment.Id 
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == requestingAssignment.Schedule.WorkDate);
                if (targetConflict)
                {
                    return ApiResponse<ShiftSwapRequestDto>.Fail($"Đồng nghiệp '{targetUser.FullName}' đã có ca trực khác ngày {requestingAssignment.Schedule.WorkDate:dd/MM/yyyy}, không thể nhận ca xin đổi.");
                }
            }
        }

        // ── KIỂM TRA LUẬT LAO ĐỘNG (Điều 110, 105/107, 111 BLLĐ) ──────────────────
        if (reqType != "LEAVE")
        {
            var laborCheck = await ValidateSwapLaborLawsAsync(
                reqType: reqType,
                requesterId: (ulong)requesterEmployeeId,
                requesterExcludeId: requestingAssignment.Id,
                requesterNewShiftDate: targetAssignment?.Schedule.WorkDate ?? requestingAssignment.Schedule.WorkDate,
                requesterNewShiftStart: targetAssignment?.Schedule.ShiftTemplate?.StartTime ?? requestingAssignment.Schedule.ShiftTemplate!.StartTime,
                requesterNewShiftEnd: targetAssignment?.Schedule.ShiftTemplate?.EndTime ?? requestingAssignment.Schedule.ShiftTemplate!.EndTime,
                requesterNewIsOvernight: targetAssignment?.Schedule.ShiftTemplate?.IsOvernight ?? requestingAssignment.Schedule.ShiftTemplate!.IsOvernight,
                targetId: targetUser?.Id,
                targetExcludeId: targetAssignment?.Id,
                targetNewShiftDate: requestingAssignment.Schedule.WorkDate,
                targetNewShiftStart: requestingAssignment.Schedule.ShiftTemplate!.StartTime,
                targetNewShiftEnd: requestingAssignment.Schedule.ShiftTemplate!.EndTime,
                targetNewIsOvernight: requestingAssignment.Schedule.ShiftTemplate!.IsOvernight);

            if (!laborCheck.IsValid)
                return ApiResponse<ShiftSwapRequestDto>.Fail(laborCheck.ErrorMessage!);
        }
        // ────────────────────────────────────────────────────────────────────────────

        // LEAVE → PENDING (gửi thẳng Quản lý), SWAP/TRANSFER → PENDING_PEER (chờ đồng nghiệp xác nhận trước)
        var initialStatus = reqType == "LEAVE" ? "PENDING" : "PENDING_PEER";

        var swap = new ShiftSwapRequest
        {
            RequestingAssignmentId = requestingAssignment.Id,
            RequesterUserId = (ulong)requesterEmployeeId,
            ScheduleId = requestingAssignment.ScheduleId,
            TargetUserId = targetUser?.Id,
            TargetAssignmentId = targetAssignment?.Id,
            RequestType = reqType,
            Reason = request.Reason,
            Status = initialStatus,
            CreatedAt = DateTime.UtcNow
        };

        _context.ShiftSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        var resultDto = new ShiftSwapRequestDto
        {
            SwapRequestId = (int)swap.Id,
            RequestType = swap.RequestType,
            AssignmentId = (int)requestingAssignment.Id,
            RequesterEmployeeId = requesterEmployeeId,
            RequesterName = requestingAssignment.User.FullName,
            RequesterRoleName = requestingAssignment.AssignedRole != null ? requestingAssignment.AssignedRole.RoleName : (requestingAssignment.User.Role != null ? requestingAssignment.User.Role.RoleName : ""),
            RequesterShiftName = requestingAssignment.Schedule.ShiftTemplate?.Name ?? "",
            RequesterWorkDate = requestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd"),
            RequesterTimeRange = requestingAssignment.Schedule.ShiftTemplate != null
                ? $"{requestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {requestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                : "",
            TargetEmployeeId = targetUser != null ? (int)targetUser.Id : null,
            TargetName = targetUser != null ? targetUser.FullName : "Không có (Xin nghỉ ca)",
            TargetRoleName = targetUser != null && targetUser.Role != null ? targetUser.Role.RoleName : "",
            TargetAssignmentId = targetAssignment != null ? (int)targetAssignment.Id : null,
            TargetShiftName = targetAssignment?.Schedule?.ShiftTemplate?.Name,
            TargetWorkDate = targetAssignment?.Schedule?.WorkDate.ToString("yyyy-MM-dd"),
            TargetTimeRange = targetAssignment?.Schedule?.ShiftTemplate != null
                ? $"{targetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {targetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                : null,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        };

        var successMsg = reqType == "LEAVE"
            ? "Đã gửi đơn xin nghỉ ca trực cho Cửa hàng trưởng phê duyệt và sắp xếp lại."
            : reqType == "TRANSFER"
                ? "Đã gửi đơn xin chuyển ca (nhờ làm thay), chờ đồng nghiệp xác nhận."
                : "Đã gửi đơn xin đổi ca với đồng nghiệp, chờ đồng nghiệp xác nhận.";

        // Gửi thông báo email nếu là yêu cầu đổi ca / chuyển ca cho đồng nghiệp
        if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var reqShiftInfo = $"{requestingAssignment.Schedule.ShiftTemplate?.Name} ({requestingAssignment.Schedule.WorkDate:dd/MM/yyyy} {requestingAssignment.Schedule.ShiftTemplate?.StartTime:hh\\:mm}-{requestingAssignment.Schedule.ShiftTemplate?.EndTime:hh\\:mm})";
                    var actionText = reqType == "TRANSFER" ? "Yêu cầu nhờ nhận ca làm việc" : "Yêu cầu đổi ca làm việc";
                    // PENDING_PEER: Gửi email cho đồng nghiệp với trạng thái CHỜ BẠN XÁC NHẬN
                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        targetUser.Email,
                        targetUser.FullName,
                        $"{actionText} từ {requestingAssignment.User.FullName}",
                        $"Ca của đồng nghiệp: {reqShiftInfo}" + (targetAssignment?.Schedule != null ? $" | Ca dự kiến của bạn: {targetAssignment.Schedule.ShiftTemplate?.Name} ({targetAssignment.Schedule.WorkDate:dd/MM/yyyy})" : ""),
                        requestingAssignment.Schedule.WorkDate.ToString("dd/MM/yyyy"),
                        request.Reason ?? "Không có ghi chú thêm",
                        "CHỜ BẠN XÁC NHẬN"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email cho nhân viên nhận đổi ca");
                }
            });
        }

        return ApiResponse<ShiftSwapRequestDto>.Ok(resultDto, successMsg);
    }

    /// <summary>
    /// Quản lý duyệt/từ chối yêu cầu đổi, chuyển hoặc xin nghỉ ca trực.
    /// </summary>
    public async Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.User)
            .Include(s => s.TargetUser)
            .Include(s => s.Schedule)
                .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển/nghỉ ca không tồn tại.");
        }

        if (swap.Status != "PENDING")
        {
            return ApiResponse<bool>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        // PHÂN QUYỀN CHI NHÁNH: Quản lý chỉ duyệt được đơn thuộc chi nhánh mình
        var assignmentBranchId = swap.RequestingAssignment?.Schedule?.BranchId ?? swap.Schedule?.BranchId;
        if (assignmentBranchId.HasValue)
        {
            var manager = await _context.Users.FindAsync((ulong)managerEmployeeId);
            if (manager != null && manager.HomeBranchId != assignmentBranchId.Value)
            {
                // Kiểm tra thêm: Quản lý có thể là global role (OPERATIONS_ADMIN, BUSINESS_OWNER)
                var managerRole = (await _context.Roles.FindAsync(manager.RoleId))?.RoleName?.ToUpperInvariant();
                bool isGlobal = managerRole == "OPERATIONS_ADMIN" || managerRole == "BUSINESS_OWNER" || managerRole == "ADMIN";
                if (!isGlobal)
                {
                    return ApiResponse<bool>.Fail("Bạn không phải quản lý của chi nhánh này. Không thể phê duyệt đơn.");
                }
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (swap.RequestingAssignment != null && swap.RequestingAssignment.Schedule.WorkDate < today)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt đơn điều chỉnh cho ca làm việc đã trôi qua trong quá khứ.");
        }
        if (swap.TargetAssignment != null && swap.TargetAssignment.Schedule.WorkDate < today)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt đơn điều chỉnh cho ca làm việc đã trôi qua trong quá khứ.");
        }

        // ── KIỂM TRA LUẬT LAO ĐỘNG khi Manager duyệt (Điều 110, 105/107, 111) ─────
        if (!string.Equals(swap.RequestType, "LEAVE", StringComparison.OrdinalIgnoreCase)
            && swap.RequestingAssignment?.Schedule?.ShiftTemplate != null)
        {
            var ra = swap.RequestingAssignment;
            var ta = swap.TargetAssignment;

            var laborCheck = await ValidateSwapLaborLawsAsync(
                reqType: swap.RequestType,
                requesterId: swap.RequesterUserId ?? ra.UserId,
                requesterExcludeId: ra.Id,
                requesterNewShiftDate: ta?.Schedule.WorkDate ?? ra.Schedule.WorkDate,
                requesterNewShiftStart: ta?.Schedule.ShiftTemplate?.StartTime ?? ra.Schedule.ShiftTemplate.StartTime,
                requesterNewShiftEnd: ta?.Schedule.ShiftTemplate?.EndTime ?? ra.Schedule.ShiftTemplate.EndTime,
                requesterNewIsOvernight: ta?.Schedule.ShiftTemplate?.IsOvernight ?? ra.Schedule.ShiftTemplate.IsOvernight,
                targetId: swap.TargetUserId,
                targetExcludeId: ta?.Id,
                targetNewShiftDate: ra.Schedule.WorkDate,
                targetNewShiftStart: ra.Schedule.ShiftTemplate.StartTime,
                targetNewShiftEnd: ra.Schedule.ShiftTemplate.EndTime,
                targetNewIsOvernight: ra.Schedule.ShiftTemplate.IsOvernight);

            if (!laborCheck.IsValid)
                return ApiResponse<bool>.Fail(laborCheck.ErrorMessage!);
        }
        // ────────────────────────────────────────────────────────────────────────────

        // Lưu thông tin người làm đơn & đối tác trước khi thay đổi quan hệ DB
        var requesterUser = swap.RequesterUser ?? swap.RequestingAssignment?.User;
        if (requesterUser == null && swap.RequesterUserId.HasValue)
        {
            requesterUser = await _context.Users.FindAsync(swap.RequesterUserId.Value);
        }

        var targetUser = swap.TargetUser ?? swap.TargetAssignment?.User;
        if (targetUser == null && swap.TargetUserId.HasValue)
        {
            targetUser = await _context.Users.FindAsync(swap.TargetUserId.Value);
        }

        var requesterShiftTemplate = swap.RequestingAssignment?.Schedule?.ShiftTemplate ?? swap.Schedule?.ShiftTemplate;
        var requesterWorkDate = swap.RequestingAssignment?.Schedule?.WorkDate ?? swap.Schedule?.WorkDate ?? today;
        var targetShiftTemplate = swap.TargetAssignment?.Schedule?.ShiftTemplate;
        var targetWorkDate = swap.TargetAssignment?.Schedule?.WorkDate ?? today;

        swap.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        swap.ReviewedBy = (ulong)managerEmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;

        if (request.IsApproved)
        {
            if (string.Equals(swap.RequestType, "LEAVE", StringComparison.OrdinalIgnoreCase))
            {
                // TH1: Xin nghỉ ca -> Phê duyệt đổi trạng thái ca thành CANCELLED (giữ lại bản ghi, không xóa hẳn)
                if (swap.RequestingAssignment != null)
                {
                    swap.RequesterUserId ??= swap.RequestingAssignment.UserId;
                    swap.ScheduleId ??= swap.RequestingAssignment.ScheduleId;
                    swap.RequestingAssignment.Status = "CANCELLED";
                }
            }
            else if (string.Equals(swap.RequestType, "TRANSFER", StringComparison.OrdinalIgnoreCase))
            {
                // TH2a: Chuyển ca 1 chiều -> Gán lại UserId cho TargetUser
                if (swap.RequestingAssignment != null && swap.TargetUserId.HasValue)
                {
                    var isTargetAlreadyAssigned = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.Id != swap.RequestingAssignmentId 
                                     && sa.UserId == swap.TargetUserId.Value 
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                    if (isTargetAlreadyAssigned)
                    {
                        return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp nhận ca đã có phân công ca trực khác trong cùng ngày.");
                    }

                    swap.RequestingAssignment.UserId = swap.TargetUserId.Value;
                    swap.RequestingAssignment.Status = "CONFIRMED";
                }
            }
            else
            {
                // TH2b: Đổi ca 2 chiều -> Hoán đổi UserId giữa 2 ca trực
                if (swap.RequestingAssignment != null && swap.TargetAssignment != null)
                {
                    bool isSameDaySwap = swap.RequestingAssignment.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate;

                    if (isSameDaySwap)
                    {
                        if (swap.RequestingAssignment.ScheduleId == swap.TargetAssignment.ScheduleId)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Hai nhân viên đang trực chung một khung ca.");
                        }

                        var requesterHasOtherShift = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.UserId == swap.RequestingAssignment.UserId 
                                         && sa.Id != swap.RequestingAssignmentId 
                                         && sa.Id != swap.TargetAssignmentId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                        if (requesterHasOtherShift)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Nhân viên xin đổi đã có ca trực khác chưa hoàn tất trong ngày.");
                        }

                        var targetHasOtherShift = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.UserId == swap.TargetAssignment.UserId 
                                         && sa.Id != swap.RequestingAssignmentId 
                                         && sa.Id != swap.TargetAssignmentId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                        if (targetHasOtherShift)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp đã có ca trực khác chưa hoàn tất trong ngày.");
                        }
                    }
                    else
                    {
                        var requesterConflict = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.Id != swap.RequestingAssignmentId 
                                         && sa.UserId == swap.RequestingAssignment.UserId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                        if (requesterConflict)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Nhân viên xin đổi đã có ca trực khác trong ngày của ca được đổi.");
                        }

                        var targetConflict = await _context.ShiftAssignments
                            .AnyAsync(sa => sa.Id != swap.TargetAssignmentId 
                                         && sa.UserId == swap.TargetAssignment.UserId 
                                         && sa.Status != "CANCELLED"
                                         && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                        if (targetConflict)
                        {
                            return ApiResponse<bool>.Fail("Không thể phê duyệt: Đồng nghiệp đã có ca trực khác trong ngày của ca xin đổi.");
                        }
                    }

                    var tempUserId = swap.RequestingAssignment.UserId;
                    swap.RequestingAssignment.UserId = swap.TargetAssignment.UserId;
                    swap.TargetAssignment.UserId = tempUserId;
                    swap.RequestingAssignment.Status = "CONFIRMED";
                    swap.TargetAssignment.Status = "CONFIRMED";
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponse<bool>.Fail("Không thể phê duyệt: Phát sinh xung đột phân công ca trực trong hệ thống.");
        }

        // Gửi email thông báo kết quả duyệt lịch cho các bên liên quan
        _ = Task.Run(async () =>
        {
            try
            {
                var reqShiftInfo = $"{requesterShiftTemplate?.Name} ({requesterWorkDate:dd/MM/yyyy} {requesterShiftTemplate?.StartTime:hh\\:mm}-{requesterShiftTemplate?.EndTime:hh\\:mm})";
                var statusText = request.IsApproved ? "ĐÃ ĐƯỢC PHÊ DUYỆT" : "ĐÃ BỊ TỪ CHỐI";

                // 1. Gửi email cho người làm đơn
                if (requesterUser != null && !string.IsNullOrWhiteSpace(requesterUser.Email))
                {
                    var changeTitle = swap.RequestType switch
                    {
                        "LEAVE" => "Đơn xin nghỉ ca trực",
                        "TRANSFER" => "Đơn xin chuyển ca trực (nhờ làm thay)",
                        _ => "Đơn xin đổi ca trực với đồng nghiệp"
                    };

                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        requesterUser.Email,
                        requesterUser.FullName,
                        changeTitle,
                        reqShiftInfo,
                        requesterWorkDate.ToString("dd/MM/yyyy"),
                        swap.Reason ?? "Không có ghi chú thêm",
                        statusText
                    );
                }

                // 2. Gửi email cho đồng nghiệp tiếp nhận (nếu có)
                if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
                {
                    var targetTitle = swap.RequestType == "TRANSFER"
                        ? "Phân công nhận ca thay từ đồng nghiệp"
                        : "Kết quả đổi ca trực với đồng nghiệp";

                    var targetDetails = swap.RequestType == "TRANSFER"
                        ? $"Bạn nhận ca: {reqShiftInfo}"
                        : $"Ca mới của bạn: {reqShiftInfo} (Đổi ca của bạn: {targetShiftTemplate?.Name} {targetWorkDate:dd/MM/yyyy})";

                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        targetUser.Email,
                        targetUser.FullName,
                        targetTitle,
                        targetDetails,
                        requesterWorkDate.ToString("dd/MM/yyyy"),
                        swap.Reason ?? "Không có ghi chú thêm",
                        statusText
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email thông báo kết quả phê duyệt đổi ca");
            }
        });

        var message = request.IsApproved
            ? "Đã phê duyệt đơn và cập nhật lịch làm việc thành công."
            : "Đã từ chối đơn yêu cầu điều chỉnh lịch ca.";

        return ApiResponse<bool>.Ok(true, message);
    }

    /// <summary>
    /// Đồng nghiệp (TargetUser) xác nhận hoặc từ chối đơn đổi/chuyển ca (Bước 1 của luồng 2 bước).
    /// Chỉ đơn có Status = "PENDING_PEER" mới được xử lý.
    /// </summary>
    public async Task<ApiResponse<bool>> RespondToSwapRequestAsync(int peerEmployeeId, PeerReviewSwapRequestDto request)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .Include(s => s.TargetUser)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)request.SwapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển ca không tồn tại.");
        }

        // Chỉ TargetUser mới được phản hồi
        if (swap.TargetUserId != (ulong)peerEmployeeId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải là người được yêu cầu xác nhận đơn này.");
        }

        if (swap.Status != "PENDING_PEER")
        {
            return ApiResponse<bool>.Fail("Đơn này không ở trạng thái chờ bạn xác nhận.");
        }

        // Lazy expiry: Kiểm tra nếu WorkDate đã qua hoặc bằng hôm nay → auto EXPIRED
        var today = DateOnly.FromDateTime(DateTime.Today);
        var workDate = swap.RequestingAssignment?.Schedule?.WorkDate;
        if (workDate.HasValue && workDate.Value <= today)
        {
            swap.Status = "EXPIRED";
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Fail("Đơn đã hết hạn vì ngày ca trực đã qua hoặc quá gần.");
        }

        if (request.IsAccepted)
        {
            // Re-validate xung đột ca trực (lịch có thể thay đổi trong lúc chờ peer review)
            if (swap.RequestType == "TRANSFER" && swap.RequestingAssignment != null)
            {
                var targetHasConflict = await _context.ShiftAssignments
                    .AnyAsync(sa => sa.UserId == swap.TargetUserId.Value
                                 && sa.Status != "CANCELLED"
                                 && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                if (targetHasConflict)
                {
                    return ApiResponse<bool>.Fail("Không thể xác nhận: Bạn đã có ca trực khác trong ngày này kể từ khi đơn được tạo.");
                }
            }
            else if (swap.RequestType == "SWAP" && swap.TargetAssignment != null && swap.RequestingAssignment != null)
            {
                bool isSameDay = swap.RequestingAssignment.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate;
                if (!isSameDay)
                {
                    // Kiểm tra requester có ca vào ngày ca target
                    var requesterConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.UserId == swap.RequesterUserId.Value
                                     && sa.Id != swap.RequestingAssignmentId
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.TargetAssignment.Schedule.WorkDate);
                    if (requesterConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể xác nhận: Nhân viên xin đổi đã có ca trực mới trong ngày ca của bạn.");
                    }

                    // Kiểm tra target có ca vào ngày ca requester
                    var targetConflict = await _context.ShiftAssignments
                        .AnyAsync(sa => sa.UserId == swap.TargetUserId.Value
                                     && sa.Id != swap.TargetAssignmentId
                                     && sa.Status != "CANCELLED"
                                     && sa.Schedule.WorkDate == swap.RequestingAssignment.Schedule.WorkDate);
                    if (targetConflict)
                    {
                        return ApiResponse<bool>.Fail("Không thể xác nhận: Bạn đã có ca trực khác trong ngày ca của đồng nghiệp.");
                    }
                }
            }

            // ── KIỂM TRA LUẬT LAO ĐỘNG khi Peer xác nhận (Điều 110, 105/107, 111) ──
            if (swap.RequestingAssignment?.Schedule?.ShiftTemplate != null)
            {
                var ra = swap.RequestingAssignment;
                var ta = swap.TargetAssignment;

                var laborCheck = await ValidateSwapLaborLawsAsync(
                    reqType: swap.RequestType,
                    requesterId: swap.RequesterUserId ?? ra.UserId,
                    requesterExcludeId: ra.Id,
                    requesterNewShiftDate: ta?.Schedule.WorkDate ?? ra.Schedule.WorkDate,
                    requesterNewShiftStart: ta?.Schedule.ShiftTemplate?.StartTime ?? ra.Schedule.ShiftTemplate.StartTime,
                    requesterNewShiftEnd: ta?.Schedule.ShiftTemplate?.EndTime ?? ra.Schedule.ShiftTemplate.EndTime,
                    requesterNewIsOvernight: ta?.Schedule.ShiftTemplate?.IsOvernight ?? ra.Schedule.ShiftTemplate.IsOvernight,
                    targetId: swap.TargetUserId,
                    targetExcludeId: ta?.Id,
                    targetNewShiftDate: ra.Schedule.WorkDate,
                    targetNewShiftStart: ra.Schedule.ShiftTemplate.StartTime,
                    targetNewShiftEnd: ra.Schedule.ShiftTemplate.EndTime,
                    targetNewIsOvernight: ra.Schedule.ShiftTemplate.IsOvernight);

                if (!laborCheck.IsValid)
                    return ApiResponse<bool>.Fail(laborCheck.ErrorMessage!);
            }
            // ────────────────────────────────────────────────────────────────────────

            swap.Status = "PENDING";
            await _context.SaveChangesAsync();

            // Gửi email cho người tạo đơn: Đồng nghiệp đã đồng ý
            if (swap.RequesterUser != null && !string.IsNullOrWhiteSpace(swap.RequesterUser.Email))
            {
                var peerName = swap.TargetUser?.FullName ?? "Đồng nghiệp";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                        await _emailService.SendShiftChangeNotificationEmailAsync(
                            swap.RequesterUser.Email,
                            swap.RequesterUser.FullName,
                            $"{peerName} đã đồng ý đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} của bạn",
                            $"Ca trực: {shiftInfo}. Đơn đang chờ Quản lý chi nhánh phê duyệt.",
                            swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                            swap.Reason ?? "",
                            "CHỜ QUẢN LÝ DUYỆT"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email thông báo peer accept");
                    }
                });
            }

            return ApiResponse<bool>.Ok(true, $"Bạn đã đồng ý đơn. Đơn chuyển sang Quản lý chi nhánh phê duyệt.");
        }
        else
        {
            swap.Status = "REJECTED";
            await _context.SaveChangesAsync();

            // Gửi email cho người tạo đơn: Đồng nghiệp đã từ chối
            if (swap.RequesterUser != null && !string.IsNullOrWhiteSpace(swap.RequesterUser.Email))
            {
                var peerName = swap.TargetUser?.FullName ?? "Đồng nghiệp";
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                        await _emailService.SendShiftChangeNotificationEmailAsync(
                            swap.RequesterUser.Email,
                            swap.RequesterUser.FullName,
                            $"{peerName} đã từ chối đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} của bạn",
                            $"Ca trực: {shiftInfo}. Bạn có thể tạo đơn mới với đồng nghiệp khác.",
                            swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                            swap.Reason ?? "",
                            "ĐÃ BỊ TỪ CHỐI"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email thông báo peer reject");
                    }
                });
            }

            return ApiResponse<bool>.Ok(true, "Bạn đã từ chối đơn đổi/chuyển ca.");
        }
    }

    /// <summary>
    /// Nhân viên tạo đơn (RequesterUser) hủy đơn đổi/chuyển/nghỉ ca đang chờ xử lý.
    /// Chỉ áp dụng cho đơn có Status = "PENDING" hoặc "PENDING_PEER".
    /// </summary>
    public async Task<ApiResponse<bool>> CancelSwapRequestAsync(int requesterEmployeeId, int swapRequestId)
    {
        var swap = await _context.ShiftSwapRequests
            .Include(s => s.TargetUser)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(ws => ws.ShiftTemplate)
            .FirstOrDefaultAsync(s => s.Id == (ulong)swapRequestId);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi/chuyển/nghỉ ca không tồn tại.");
        }

        if (swap.RequesterUserId != (ulong)requesterEmployeeId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải là người tạo đơn này.");
        }

        if (swap.Status != "PENDING" && swap.Status != "PENDING_PEER")
        {
            return ApiResponse<bool>.Fail("Đơn này đã được xử lý, không thể hủy.");
        }

        swap.Status = "CANCELLED";
        await _context.SaveChangesAsync();

        // Gửi email cho TargetUser (nếu có) thông báo đơn bị hủy
        if (swap.TargetUser != null && !string.IsNullOrWhiteSpace(swap.TargetUser.Email))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var shiftInfo = $"{swap.RequestingAssignment?.Schedule?.ShiftTemplate?.Name} ({swap.RequestingAssignment?.Schedule?.WorkDate:dd/MM/yyyy})";
                    await _emailService.SendShiftChangeNotificationEmailAsync(
                        swap.TargetUser.Email,
                        swap.TargetUser.FullName,
                        $"Đơn {(swap.RequestType == "TRANSFER" ? "chuyển ca" : "đổi ca")} đã bị hủy bởi người tạo",
                        $"Ca trực: {shiftInfo}",
                        swap.RequestingAssignment?.Schedule?.WorkDate.ToString("dd/MM/yyyy") ?? "",
                        swap.Reason ?? "",
                        "ĐÃ BỊ HỦY"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email thông báo hủy đơn swap");
                }
            });
        }

        return ApiResponse<bool>.Ok(true, "Đã hủy đơn thành công.");
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển/nghỉ ca của toàn chi nhánh (cho Quản lý cửa hàng duyệt).
    /// </summary>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
    {
        // LAZY AUTO-EXPIRY: Tự động hết hạn các đơn chờ mà ngày ca trực đã qua hoặc bằng hôm nay
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expiredRequests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.Schedule)
            .Where(s => (s.Status == "PENDING" || s.Status == "PENDING_PEER")
                     && ((s.RequestingAssignment != null && s.RequestingAssignment.Schedule.WorkDate <= today)
                      || (s.Schedule != null && s.Schedule.WorkDate <= today))
                     && ((s.Schedule != null && s.Schedule.BranchId == (ulong)storeId)
                      || (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.BranchId == (ulong)storeId)))
            .ToListAsync();

        if (expiredRequests.Any())
        {
            foreach (var r in expiredRequests)
            {
                r.Status = "EXPIRED";
            }
            await _context.SaveChangesAsync();
        }
        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
                    .ThenInclude(u => u.Role)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.TargetUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.ReviewedByUser)
            .Where(s => (s.Schedule != null && s.Schedule.BranchId == (ulong)storeId) 
                     || (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.BranchId == (ulong)storeId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                RequestType = s.RequestType,
                AssignmentId = (int)(s.RequestingAssignmentId ?? 0),
                RequesterEmployeeId = (int)(s.RequesterUserId ?? (s.RequestingAssignment != null ? s.RequestingAssignment.UserId : 0)),
                RequesterName = s.RequesterUser != null 
                    ? s.RequesterUser.FullName 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.User.FullName : "Nhân viên"),
                RequesterRoleName = s.RequesterUser != null && s.RequesterUser.Role != null 
                    ? s.RequesterUser.Role.RoleName 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.AssignedRole != null 
                        ? s.RequestingAssignment.AssignedRole.RoleName 
                        : (s.RequestingAssignment != null && s.RequestingAssignment.User.Role != null ? s.RequestingAssignment.User.Role.RoleName : "")),
                RequesterShiftName = s.Schedule != null && s.Schedule.ShiftTemplate != null 
                    ? s.Schedule.ShiftTemplate.Name 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null 
                        ? s.RequestingAssignment.Schedule.ShiftTemplate.Name : ""),
                RequesterWorkDate = s.Schedule != null 
                    ? s.Schedule.WorkDate.ToString("yyyy-MM-dd") 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : ""),
                RequesterTimeRange = s.Schedule != null && s.Schedule.ShiftTemplate != null
                    ? $"{s.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null
                        ? $"{s.RequestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.RequestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                        : ""),
                TargetEmployeeId = s.TargetUserId != null ? (int)s.TargetUserId : null,
                TargetName = s.TargetUser != null ? s.TargetUser.FullName : "Không có (Xin nghỉ ca)",
                TargetRoleName = s.TargetUser != null && s.TargetUser.Role != null ? s.TargetUser.Role.RoleName : "",
                TargetAssignmentId = s.TargetAssignmentId != null ? (int)s.TargetAssignmentId : null,
                TargetShiftName = s.TargetAssignment != null ? s.TargetAssignment.Schedule.ShiftTemplate.Name : null,
                TargetWorkDate = s.TargetAssignment != null ? s.TargetAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : null,
                TargetTimeRange = s.TargetAssignment != null && s.TargetAssignment.Schedule.ShiftTemplate != null
                    ? $"{s.TargetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.TargetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : null,
                Reason = s.Reason,
                Status = s.Status,
                ReviewedByName = s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null,
                ReviewedAt = s.ReviewedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }

    /// <summary>
    /// Lấy danh sách các yêu cầu đổi/chuyển/nghỉ ca của chính nhân viên (đã gửi hoặc được nhờ).
    /// </summary>
    public async Task<ApiResponse<List<ShiftSwapRequestDto>>> GetMySwapRequestsAsync(int employeeId)
    {
        // LAZY AUTO-EXPIRY: Tự động hết hạn các đơn chờ mà ngày ca trực đã qua hoặc bằng hôm nay
        var today = DateOnly.FromDateTime(DateTime.Today);
        var expiredRequests = await _context.ShiftSwapRequests
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
            .Include(s => s.Schedule)
            .Where(s => (s.Status == "PENDING" || s.Status == "PENDING_PEER")
                     && ((s.RequesterUserId != null && s.RequesterUserId == (ulong)employeeId)
                      || (s.TargetUserId != null && s.TargetUserId == (ulong)employeeId))
                     && ((s.RequestingAssignment != null && s.RequestingAssignment.Schedule.WorkDate <= today)
                      || (s.Schedule != null && s.Schedule.WorkDate <= today)))
            .ToListAsync();

        if (expiredRequests.Any())
        {
            foreach (var r in expiredRequests)
            {
                r.Status = "EXPIRED";
            }
            await _context.SaveChangesAsync();
        }

        var requests = await _context.ShiftSwapRequests
            .Include(s => s.RequesterUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.Schedule)
                .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.User)
                    .ThenInclude(u => u.Role)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.AssignedRole)
            .Include(s => s.RequestingAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.TargetUser)
                .ThenInclude(u => u.Role)
            .Include(s => s.TargetAssignment)
                .ThenInclude(sa => sa.Schedule)
                    .ThenInclude(sc => sc.ShiftTemplate)
            .Include(s => s.ReviewedByUser)
            .Where(s => (s.RequesterUserId != null && s.RequesterUserId == (ulong)employeeId)
                     || (s.RequestingAssignment != null && s.RequestingAssignment.UserId == (ulong)employeeId) 
                     || (s.TargetUserId != null && s.TargetUserId == (ulong)employeeId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShiftSwapRequestDto
            {
                SwapRequestId = (int)s.Id,
                RequestType = s.RequestType,
                AssignmentId = (int)(s.RequestingAssignmentId ?? 0),
                RequesterEmployeeId = (int)(s.RequesterUserId ?? (s.RequestingAssignment != null ? s.RequestingAssignment.UserId : 0)),
                RequesterName = s.RequesterUser != null 
                    ? s.RequesterUser.FullName 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.User.FullName : "Nhân viên"),
                RequesterRoleName = s.RequesterUser != null && s.RequesterUser.Role != null 
                    ? s.RequesterUser.Role.RoleName 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.AssignedRole != null 
                        ? s.RequestingAssignment.AssignedRole.RoleName 
                        : (s.RequestingAssignment != null && s.RequestingAssignment.User.Role != null ? s.RequestingAssignment.User.Role.RoleName : "")),
                RequesterShiftName = s.Schedule != null && s.Schedule.ShiftTemplate != null 
                    ? s.Schedule.ShiftTemplate.Name 
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null 
                        ? s.RequestingAssignment.Schedule.ShiftTemplate.Name : ""),
                RequesterWorkDate = s.Schedule != null 
                    ? s.Schedule.WorkDate.ToString("yyyy-MM-dd") 
                    : (s.RequestingAssignment != null ? s.RequestingAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : ""),
                RequesterTimeRange = s.Schedule != null && s.Schedule.ShiftTemplate != null
                    ? $"{s.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : (s.RequestingAssignment != null && s.RequestingAssignment.Schedule.ShiftTemplate != null
                        ? $"{s.RequestingAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.RequestingAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                        : ""),
                TargetEmployeeId = s.TargetUserId != null ? (int)s.TargetUserId : null,
                TargetName = s.TargetUser != null ? s.TargetUser.FullName : "Không có (Xin nghỉ ca)",
                TargetRoleName = s.TargetUser != null && s.TargetUser.Role != null ? s.TargetUser.Role.RoleName : "",
                TargetAssignmentId = s.TargetAssignmentId != null ? (int)s.TargetAssignmentId : null,
                TargetShiftName = s.TargetAssignment != null ? s.TargetAssignment.Schedule.ShiftTemplate.Name : null,
                TargetWorkDate = s.TargetAssignment != null ? s.TargetAssignment.Schedule.WorkDate.ToString("yyyy-MM-dd") : null,
                TargetTimeRange = s.TargetAssignment != null && s.TargetAssignment.Schedule.ShiftTemplate != null
                    ? $"{s.TargetAssignment.Schedule.ShiftTemplate.StartTime:hh\\:mm} - {s.TargetAssignment.Schedule.ShiftTemplate.EndTime:hh\\:mm}"
                    : null,
                Reason = s.Reason,
                Status = s.Status,
                ReviewedByName = s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null,
                ReviewedAt = s.ReviewedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<ShiftSwapRequestDto>>.Ok(requests);
    }

    /// <summary>
    /// Helper: Xác định chi nhánh làm việc thực tế của nhân viên vào một ngày cụ thể.
    /// Nếu nhân viên đang có lệnh điều chuyển (TemporaryDispatch APPROVED) bao phủ ngày workDate → trả về TargetBranchId.
    /// Ngược lại → trả về HomeBranchId.
    /// </summary>
    private async Task<ulong> GetEffectiveBranchIdAsync(ulong userId, DateOnly workDate)
    {
        var dispatch = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Include(d => d.DispatchEmployees)
            .Where(d => (d.UserId == userId || d.DispatchEmployees.Any(de => de.UserId == userId && de.Status == "APPROVED"))
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL")
                     && d.StartDate <= workDate
                     && d.EndDate >= workDate)
            .FirstOrDefaultAsync();

        if (dispatch != null)
        {
            return dispatch.TargetBranchId;
        }

        var user = await _context.Users.FindAsync(userId);
        return user?.OriginalHomeBranchId ?? user?.HomeBranchId ?? 0;
    }

    /// <summary>
    /// Lấy danh sách đồng nghiệp đang thực sự làm việc tại chi nhánh vào ngày cụ thể để nhân viên chọn khi đổi/chuyển ca.
    /// Bao gồm: (1) NV gốc chi nhánh không bị biệt phái đi nơi khác, (2) NV từ chi nhánh khác được biệt phái đến.
    /// Ràng buộc: Cùng RoleId, loại trừ chính mình.
    /// </summary>
    public async Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId, DateOnly? workDate = null)
    {
        var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == (ulong)currentEmployeeId);
        if (currentUser == null)
        {
            return ApiResponse<List<ColleagueDto>>.Fail("Không tìm thấy thông tin nhân viên yêu cầu.");
        }

        var effectiveWorkDate = workDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        // === NHÓM 1: NV gốc của chi nhánh, KHÔNG bị biệt phái đi nơi khác vào workDate ===
        // Lấy danh sách ID nhân viên gốc chi nhánh đang bị dispatch đi chỗ khác
        var dispatchedAwayUserIds = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Where(d => d.Status == "APPROVED"
                     && d.StartDate <= effectiveWorkDate
                     && d.EndDate >= effectiveWorkDate
                     && d.SourceBranchId == (ulong)branchId)
            .Select(d => d.UserId)
            .ToListAsync();

        var nativeColleagues = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Id != (ulong)currentEmployeeId
                     && u.HomeBranchId == (ulong)branchId
                     && u.Status == "ACTIVE"
                     && u.RoleId == currentUser.RoleId
                     && !dispatchedAwayUserIds.Contains(u.Id))
            .Select(u => new ColleagueDto
            {
                EmployeeId = (int)u.Id,
                FullName = u.FullName,
                RoleName = u.Role != null ? u.Role.RoleName : "",
                PhoneNumber = u.Phone ?? ""
            })
            .ToListAsync();

        // === NHÓM 2: NV từ chi nhánh khác được biệt phái ĐẾN chi nhánh này vào workDate ===
        var dispatchedInUserIds = await _context.Set<Domain.Entities.TemporaryDispatch>()
            .Where(d => d.Status == "APPROVED"
                     && d.StartDate <= effectiveWorkDate
                     && d.EndDate >= effectiveWorkDate
                     && d.TargetBranchId == (ulong)branchId)
            .Select(d => d.UserId)
            .ToListAsync();

        // Loại bỏ chính mình và lọc cùng role
        var dispatchedInColleagues = dispatchedInUserIds.Contains((ulong)currentEmployeeId)
            ? new List<ColleagueDto>()
            : await _context.Users
                .Include(u => u.Role)
                .Where(u => dispatchedInUserIds.Contains(u.Id)
                         && u.Id != (ulong)currentEmployeeId
                         && u.Status == "ACTIVE"
                         && u.RoleId == currentUser.RoleId)
                .Select(u => new ColleagueDto
                {
                    EmployeeId = (int)u.Id,
                    FullName = u.FullName,
                    RoleName = u.Role != null ? u.Role.RoleName : "",
                    PhoneNumber = u.Phone ?? ""
                })
                .ToListAsync();

        // Gộp 2 nhóm, loại trùng, sắp xếp theo tên
        var colleagues = nativeColleagues
            .Concat(dispatchedInColleagues)
            .GroupBy(c => c.EmployeeId)
            .Select(g => g.First())
            .OrderBy(c => c.FullName)
            .ToList();

        return ApiResponse<List<ColleagueDto>>.Ok(colleagues);
    }

    /// <summary>
    /// Lấy danh sách các ca làm việc của một đồng nghiệp trong tương lai để nhân viên chọn đổi.
    /// Hỗ trợ kiểm tra báo trước 1 ngày và xử lý đổi ca cùng ngày.
    /// </summary>
    public async Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId, int? requestingAssignmentId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var minAllowedDate = today.AddDays(1); // RÀNG BUỘC: Chỉ hiển thị các ca diễn ra từ ngày mai trở đi

        DateOnly? requestingWorkDate = null;
        ulong? requestingScheduleId = null;

        if (requestingAssignmentId.HasValue && requestingAssignmentId.Value > 0)
        {
            var reqAssignment = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                .FirstOrDefaultAsync(sa => sa.Id == (ulong)requestingAssignmentId.Value && sa.UserId == (ulong)currentEmployeeId);

            if (reqAssignment != null)
            {
                requestingWorkDate = reqAssignment.Schedule.WorkDate;
                requestingScheduleId = reqAssignment.ScheduleId;
            }
        }

        // Lấy danh sách ca trực hiện có của nhân viên yêu cầu từ ngày mai trở đi
        var myActiveAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
            .Where(sa => sa.UserId == (ulong)currentEmployeeId 
                      && sa.Status != "CANCELLED"
                      && sa.Schedule.WorkDate >= minAllowedDate)
            .ToListAsync();

        // Danh sách các ngày nhân viên đã có lịch bận (loại trừ ca đang đem ra đổi)
        var myBusyDates = myActiveAssignments
            .Where(sa => !requestingAssignmentId.HasValue || sa.Id != (ulong)requestingAssignmentId.Value)
            .Select(sa => sa.Schedule.WorkDate)
            .ToHashSet();

        // Danh sách khung ca nhân viên hiện tại đang trực
        var myActiveScheduleIds = myActiveAssignments
            .Select(sa => sa.ScheduleId)
            .ToHashSet();

        // Lấy danh sách ca khả dụng của đồng nghiệp
        var colleagueShifts = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.Branch)
            .Where(sa => sa.UserId == (ulong)colleagueEmployeeId 
                      && sa.Status != "CANCELLED"
                      && sa.Schedule.WorkDate >= minAllowedDate)
            .OrderBy(sa => sa.Schedule.WorkDate)
            .ThenBy(sa => sa.Schedule.ShiftTemplate.StartTime)
            .ToListAsync();

        var availableShifts = new List<ColleagueShiftDto>();

        foreach (var sa in colleagueShifts)
        {
            // Trùng khung giờ trực chung
            if (myActiveScheduleIds.Contains(sa.ScheduleId))
            {
                continue;
            }

            // FILTER THEO CHI NHÁNH: Ca của đồng nghiệp phải thuộc chi nhánh mà nhân viên hiện tại được phép làm việc vào ngày đó
            var myEffectiveBranchId = await GetEffectiveBranchIdAsync((ulong)currentEmployeeId, sa.Schedule.WorkDate);
            if (sa.Schedule.BranchId != myEffectiveBranchId)
            {
                continue;
            }

            // Nếu là ca cùng ngày với ca đem ra đổi
            if (requestingWorkDate.HasValue && sa.Schedule.WorkDate == requestingWorkDate.Value)
            {
                // Cho phép đổi cùng ngày miễn là nhân viên không có ca thứ 3 bận trong ngày đó
                if (myBusyDates.Contains(sa.Schedule.WorkDate))
                {
                    continue;
                }
            }
            else
            {
                // Khác ngày: Nhân viên không được có ca trực trong ngày đó
                if (myBusyDates.Contains(sa.Schedule.WorkDate))
                {
                    continue;
                }
            }

            availableShifts.Add(new ColleagueShiftDto
            {
                AssignmentId = (int)sa.Id,
                ScheduleId = (int)sa.ScheduleId,
                ShiftName = sa.Schedule.EffectiveShiftName,
                WorkDate = sa.Schedule.WorkDate.ToString("yyyy-MM-dd"),
                TimeRange = $"{sa.Schedule.EffectiveStartTime:hh\\:mm} - {sa.Schedule.EffectiveEndTime:hh\\:mm}",
                BranchName = sa.Schedule.Branch.Name
            });
        }

        return ApiResponse<List<ColleagueShiftDto>>.Ok(availableShifts);
    }

    // ============================================================
    // PRIVATE HELPERS: Kiểm tra Luật Lao động khi Swap / Transfer
    // ============================================================

    /// <summary>
    /// Orchestrator: Chạy 3 bộ kiểm tra luật lao động cho cả 2 bên (requester và target).
    /// SWAP: check cả 2 (mỗi người nhận ca của người kia).
    /// TRANSFER: chỉ check target (người nhận ca mới).
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> ValidateSwapLaborLawsAsync(
        string reqType,
        ulong requesterId,
        ulong requesterExcludeId,
        DateOnly requesterNewShiftDate,
        TimeOnly requesterNewShiftStart,
        TimeOnly requesterNewShiftEnd,
        bool requesterNewIsOvernight,
        ulong? targetId,
        ulong? targetExcludeId,
        DateOnly targetNewShiftDate,
        TimeOnly targetNewShiftStart,
        TimeOnly targetNewShiftEnd,
        bool targetNewIsOvernight)
    {
        // --- Kiểm tra cho REQUESTER (chỉ với SWAP – người nhận ca của target) ---
        if (string.Equals(reqType, "SWAP", StringComparison.OrdinalIgnoreCase))
        {
            double requesterNewHours = GetShiftNetWorkingHours(
                requesterNewShiftStart, requesterNewShiftEnd, requesterNewIsOvernight);

            var r1 = await CheckMinRestBetweenShiftsAsync(
                requesterNewShiftDate, requesterId, requesterExcludeId,
                requesterNewShiftStart, requesterNewShiftEnd, requesterNewIsOvernight,
                isRequester: true);
            if (!r1.IsValid) return r1;

            var r2 = await CheckMaxDailyWorkingHoursAsync(
                requesterId, requesterExcludeId,
                requesterNewShiftDate, requesterNewHours,
                isRequester: true);
            if (!r2.IsValid) return r2;

            var r3 = await CheckWeeklyRestRequirementAsync(
                requesterId, requesterExcludeId,
                requesterNewShiftDate, requesterNewShiftStart,
                requesterNewShiftEnd, requesterNewIsOvernight,
                isRequester: true);
            if (!r3.IsValid) return r3;
        }

        // --- Kiểm tra cho TARGET (SWAP hoặc TRANSFER – người nhận ca của requester) ---
        if (targetId.HasValue)
        {
            double targetNewHours = GetShiftNetWorkingHours(
                targetNewShiftStart, targetNewShiftEnd, targetNewIsOvernight);

            var t1 = await CheckMinRestBetweenShiftsAsync(
                targetNewShiftDate, targetId.Value, targetExcludeId,
                targetNewShiftStart, targetNewShiftEnd, targetNewIsOvernight,
                isRequester: false);
            if (!t1.IsValid) return t1;

            var t2 = await CheckMaxDailyWorkingHoursAsync(
                targetId.Value, targetExcludeId,
                targetNewShiftDate, targetNewHours,
                isRequester: false);
            if (!t2.IsValid) return t2;

            var t3 = await CheckWeeklyRestRequirementAsync(
                targetId.Value, targetExcludeId,
                targetNewShiftDate, targetNewShiftStart,
                targetNewShiftEnd, targetNewIsOvernight,
                isRequester: false);
            if (!t3.IsValid) return t3;
        }

        return (true, null);
    }

    /// <summary>
    /// Tính số giờ làm việc thực của một ca (trừ break). Xử lý ca overnight.
    /// </summary>
    private static double GetShiftNetWorkingHours(
        TimeOnly startTime, TimeOnly endTime, bool isOvernight)
    {
        double totalMinutes = isOvernight
            ? (24 * 60 - startTime.ToTimeSpan().TotalMinutes + endTime.ToTimeSpan().TotalMinutes)
            : (endTime.ToTimeSpan().TotalMinutes - startTime.ToTimeSpan().TotalMinutes);
        return Math.Max(0, totalMinutes / 60.0);
    }

    /// <summary>
    /// Điều 110 BLLĐ: Khoảng nghỉ tối thiểu 12 giờ liên tục giữa 2 ca liên tiếp.
    /// Kiểm tra ca liền trước (∆t₁) và ca liền sau (∆t₂) ca mới.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckMinRestBetweenShiftsAsync(
        DateOnly newShiftDate,
        ulong userId,
        ulong? excludeAssignmentId,
        TimeOnly newShiftStart,
        TimeOnly newShiftEnd,
        bool newIsOvernight,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Chuyển ca mới sang DateTime
        var newStartDt = newShiftDate.ToDateTime(newShiftStart);
        var newEndDt = newIsOvernight
            ? newShiftDate.AddDays(1).ToDateTime(newShiftEnd)
            : newShiftDate.ToDateTime(newShiftEnd);

        // Query tất cả ca trong khoảng ±2 ngày để bắt được ca overnight lân cận
        var windowStart = newShiftDate.AddDays(-2);
        var windowEnd = newShiftDate.AddDays(2);

        var nearbyAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && sa.Schedule.WorkDate >= windowStart
                      && sa.Schedule.WorkDate <= windowEnd
                      && sa.Schedule.ShiftTemplate != null)
            .ToListAsync();

        DateTime? latestEndBeforeNew = null;  // ca liền trước
        DateTime? earliestStartAfterNew = null; // ca liền sau

        foreach (var sa in nearbyAssignments)
        {
            var sStart = sa.Schedule.WorkDate.ToDateTime(sa.Schedule.EffectiveStartTime);
            var sEnd = (sa.Schedule.EffectiveIsOvernight || sa.Schedule.EffectiveEndTime < sa.Schedule.EffectiveStartTime)
                ? sa.Schedule.WorkDate.AddDays(1).ToDateTime(sa.Schedule.EffectiveEndTime)
                : sa.Schedule.WorkDate.ToDateTime(sa.Schedule.EffectiveEndTime);

            if (sEnd <= newStartDt) // ca kết thúc TRƯỚC ca mới bắt đầu
            {
                if (latestEndBeforeNew == null || sEnd > latestEndBeforeNew)
                    latestEndBeforeNew = sEnd;
            }
            else if (sStart >= newEndDt) // ca bắt đầu SAU ca mới kết thúc
            {
                if (earliestStartAfterNew == null || sStart < earliestStartAfterNew)
                    earliestStartAfterNew = sStart;
            }
        }

        int minRest = HeadcountConstants.MinRestBetweenShiftsHours;

        if (latestEndBeforeNew.HasValue)
        {
            double gapHours = (newStartDt - latestEndBeforeNew.Value).TotalHours;
            if (gapHours < minRest)
                return (false,
                    $"{who} vi phạm quy định nghỉ tối thiểu {minRest} giờ trước khi chuyển sang ca mới " +
                    $"(khoảng cách hiện tại: {gapHours:F1} giờ < {minRest} giờ). [Điều 110 BLLĐ]");
        }

        if (earliestStartAfterNew.HasValue)
        {
            double gapHours = (earliestStartAfterNew.Value - newEndDt).TotalHours;
            if (gapHours < minRest)
                return (false,
                    $"{who} vi phạm quy định nghỉ tối thiểu {minRest} giờ sau ca mới trước ca tiếp theo " +
                    $"(khoảng cách hiện tại: {gapHours:F1} giờ < {minRest} giờ). [Điều 110 BLLĐ]");
        }

        return (true, null);
    }

    /// <summary>
    /// Điều 105 &amp; 107 BLLĐ: Tổng giờ làm việc trong ngày ≤ 12 giờ.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckMaxDailyWorkingHoursAsync(
        ulong userId,
        ulong? excludeAssignmentId,
        DateOnly targetDate,
        double newShiftHours,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Lấy tất cả ca trong ngày targetDate (bao gồm ca overnight bắt đầu ngày trước)
        var dayAssignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && (sa.Schedule.WorkDate == targetDate
                          || (sa.Schedule.ShiftTemplate!.IsOvernight && sa.Schedule.WorkDate == targetDate.AddDays(-1))))
            .ToListAsync();

        double existingHours = 0;
        foreach (var sa in dayAssignments)
        {
            var t = sa.Schedule.ShiftTemplate!;
            // Với ca overnight bắt đầu hôm trước: chỉ tính phần giờ rơi vào targetDate
            if (t.IsOvernight && sa.Schedule.WorkDate == targetDate.AddDays(-1))
            {
                existingHours += t.EndTime.ToTimeSpan().TotalHours; // giờ từ 00:00 đến EndTime
            }
            else
            {
                existingHours += GetShiftNetWorkingHours(t.StartTime, t.EndTime, t.IsOvernight);
            }
        }

        double totalHours = existingHours + newShiftHours;
        int maxHours = HeadcountConstants.MaxWorkingHoursPerDay;

        if (totalHours > maxHours)
            return (false,
                $"{who}: Tổng giờ làm việc trong ngày {targetDate:dd/MM/yyyy} sẽ đạt {totalHours:F1} giờ, " +
                $"vượt quá giới hạn {maxHours} giờ/ngày. [Điều 105 & 107 BLLĐ]");

        return (true, null);
    }

    /// <summary>
    /// Điều 111 BLLĐ: Trong chu kỳ 7 ngày chứa ca mới, phải có ít nhất 1 khoảng trống ≥ 24 giờ liên tục.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage)> CheckWeeklyRestRequirementAsync(
        ulong userId,
        ulong? excludeAssignmentId,
        DateOnly newShiftDate,
        TimeOnly newShiftStart,
        TimeOnly newShiftEnd,
        bool newIsOvernight,
        bool isRequester)
    {
        var who = isRequester ? "Bạn" : "Đồng nghiệp";

        // Cửa sổ kiểm tra: 6 ngày trước + ngày ca mới = 7 ngày liên tiếp
        var windowStart = newShiftDate.AddDays(-(HeadcountConstants.WeeklyRestWindowDays - 1));
        var windowEnd   = newShiftDate;
        var windowStartDt = windowStart.ToDateTime(TimeOnly.MinValue);
        var windowEndDt   = windowEnd.AddDays(1).ToDateTime(TimeOnly.MinValue); // exclusive

        // Query tất cả ca trong cửa sổ (±1 ngày để bắt overnight)
        var assignments = await _context.ShiftAssignments
            .Include(sa => sa.Schedule)
                .ThenInclude(s => s.ShiftTemplate)
            .Where(sa => sa.UserId == userId
                      && sa.Status != "CANCELLED"
                      && (excludeAssignmentId == null || sa.Id != excludeAssignmentId.Value)
                      && sa.Schedule.WorkDate >= windowStart.AddDays(-1)
                      && sa.Schedule.WorkDate <= windowEnd.AddDays(1)
                      && sa.Schedule.ShiftTemplate != null)
            .ToListAsync();

        // Xây dựng danh sách (StartDt, EndDt) bao gồm ca mới
        var intervals = new List<(DateTime Start, DateTime End)>();

        foreach (var sa in assignments)
        {
            var t = sa.Schedule.ShiftTemplate!;
            var sStart = sa.Schedule.WorkDate.ToDateTime(t.StartTime);
            var sEnd = t.IsOvernight
                ? sa.Schedule.WorkDate.AddDays(1).ToDateTime(t.EndTime)
                : sa.Schedule.WorkDate.ToDateTime(t.EndTime);
            // Chỉ đưa vào nếu interval cắt qua cửa sổ [windowStartDt, windowEndDt]
            if (sEnd > windowStartDt && sStart < windowEndDt)
                intervals.Add((sStart, sEnd));
        }

        // Thêm ca mới
        var newStart = newShiftDate.ToDateTime(newShiftStart);
        var newEnd   = newIsOvernight
            ? newShiftDate.AddDays(1).ToDateTime(newShiftEnd)
            : newShiftDate.ToDateTime(newShiftEnd);
        intervals.Add((newStart, newEnd));

        // Sắp xếp và merge overlapping/adjacent intervals
        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(DateTime Start, DateTime End)>();
        foreach (var iv in intervals)
        {
            if (merged.Count == 0 || iv.Start > merged[^1].End)
                merged.Add(iv);
            else
                merged[^1] = (merged[^1].Start, iv.End > merged[^1].End ? iv.End : merged[^1].End);
        }

        // Kiểm tra các khoảng trống trong cửa sổ
        int minRest = HeadcountConstants.MinWeeklyRestHours;
        bool hasAdequateRest = false;

        // Khoảng trống trước ca đầu tiên
        if (merged.Count == 0 || (merged[0].Start - windowStartDt).TotalHours >= minRest)
        {
            hasAdequateRest = true;
        }

        if (!hasAdequateRest)
        {
            // Khoảng trống giữa các ca liên tiếp
            for (int i = 0; i < merged.Count - 1; i++)
            {
                double gap = (merged[i + 1].Start - merged[i].End).TotalHours;
                if (gap >= minRest) { hasAdequateRest = true; break; }
            }
        }

        if (!hasAdequateRest)
        {
            // Khoảng trống sau ca cuối cùng
            if (merged.Count > 0 && (windowEndDt - merged[^1].End).TotalHours >= minRest)
                hasAdequateRest = true;
        }

        if (!hasAdequateRest)
            return (false,
                $"{who} vi phạm quy định nghỉ tuần tối thiểu {minRest} giờ liên tục trong chu kỳ 7 ngày " +
                $"({windowStart:dd/MM} – {windowEnd:dd/MM/yyyy}). Nhận ca này sẽ không còn khoảng nghỉ đủ {minRest} giờ. [Điều 111 BLLĐ]");

        return (true, null);
    }

    private async Task LogAuditAsync(ulong? actorId, string action, string targetTable, ulong targetId, object? oldValues, object? newValues, string? ipAddress)
    {
        try
        {
            var audit = new SystemAuditLog
            {
                ActorId = actorId ?? 1,
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

}
