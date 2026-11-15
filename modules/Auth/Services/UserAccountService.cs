using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domain.Entities;
using Modules.Auth.DTOs;
using Modules.Auth.Interfaces;
using Shared.Common;
using Shared.Data;
using Shared.Interfaces;
using Shared.Security;

namespace Modules.Auth.Services;

/// <summary>
/// Quản lý vòng đời tài khoản: khóa/mở khóa, cấp lại mật khẩu, xóa mềm (Soft Delete).
/// Soft Delete: đặt Status = "DELETED", xóa ca tương lai, BẢO LƯU toàn bộ lịch sử
/// chấm công, bàn giao tiền (CashHandovers), bàn giao an ninh (SecurityHandovers),
/// bàn giao ca (ShiftHandovers) và SystemAuditLogs để đối soát.
/// </summary>
public class UserAccountService : IUserAccountService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ILogger<UserAccountService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> ToggleUserStatusAsync(ulong userId, UpdateStatusDto dto, ulong actorId, string? ipAddress)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.HomeBranch)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return ApiResponse<bool>.Fail("Không tìm thấy tài khoản người dùng.");

        if (user.Id == actorId)
            return ApiResponse<bool>.Fail("Không thể tự khóa tài khoản của chính mình.");

        // Bắt buộc phải có lý do
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return ApiResponse<bool>.Fail("Bắt buộc phải nhập lý do khi thay đổi trạng thái tài khoản.");

        var oldStatus = user.Status;
        var newStatus = string.IsNullOrWhiteSpace(dto.Status) ? (user.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE") : dto.Status.ToUpper();

        // RÀNG BUỘC KHI KHÓA TÀI KHOẢN: Kiểm tra các dịch vụ đang hoạt động
        if (newStatus == "INACTIVE")
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 1. Kiểm tra lịch làm việc (Work Schedules & Shift Assignments sắp tới hoặc hôm nay)
            var activeShifts = await _context.ShiftAssignments
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.ShiftTemplate)
                .Include(sa => sa.Schedule)
                    .ThenInclude(s => s.Branch)
                .Where(sa => sa.UserId == userId
                          && sa.Schedule.WorkDate >= today
                          && sa.Schedule.Status != "CANCELLED"
                          && sa.Status != "CANCELLED")
                .OrderBy(sa => sa.Schedule.WorkDate)
                .Take(5)
                .ToListAsync();

            if (activeShifts.Any())
            {
                var shiftListStr = string.Join("; ", activeShifts.Select(s =>
                    $"{s.Schedule.WorkDate:dd/MM/yyyy} ({s.Schedule.ShiftTemplate?.Name ?? "Ca làm việc"} - {s.Schedule.Branch?.Name ?? "Chi nhánh"})"));
                return ApiResponse<bool>.Fail(
                    $"Không thể khóa tài khoản: Nhân viên đang có {activeShifts.Count} ca làm việc sắp tới [{shiftListStr}]. " +
                    "Vui lòng hủy lịch phân ca hoặc điều chuyển người thay thế trước khi khóa tài khoản.");
            }

            // 2. Kiểm tra ca trực đang hoạt động hiện tại (Đã Check-in và chưa Check-out)
            var isCurrentlyWorking = await _context.AttendanceLogs
                .AnyAsync(a => a.Assignment.UserId == userId && a.CheckOutTime == null && a.Status == Domain.Enums.AttendanceLogStatus.PRESENT);
            if (isCurrentlyWorking)
            {
                return ApiResponse<bool>.Fail("Không thể khóa tài khoản: Nhân viên hiện đang trong ca trực làm việc (đã điểm danh vào ca và chưa hoàn tất Check-out).");
            }

            // 3. Kiểm tra điều động nhân sự đang có hiệu lực
            if (user.OriginalHomeBranchId != null)
            {
                return ApiResponse<bool>.Fail("Không thể khóa tài khoản: Nhân viên đang trong thời gian điều động chi viện tại chi nhánh khác. Vui lòng hoàn tất điều động trước khi khóa.");
            }

            var activeDispatch = await _context.DispatchEmployees
                .Include(de => de.Dispatch)
                .AnyAsync(de => de.UserId == userId
                             && (de.Dispatch.Status == "PENDING" || de.Dispatch.Status == "APPROVED")
                             && de.Dispatch.EndDate >= today);
            if (activeDispatch)
            {
                return ApiResponse<bool>.Fail("Không thể khóa tài khoản: Nhân viên đang có đơn điều động nhân sự đang chờ duyệt hoặc đang có hiệu lực.");
            }
        }

        user.Status = newStatus;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "UPDATE_USER_STATUS", "users", user.Id,
            new { Status = oldStatus },
            new { Status = newStatus, Reason = dto.Reason.Trim() }, ipAddress);

        // Gửi email thông báo về email của hồ sơ nhân sự
        bool emailSent = false;
        try
        {
            emailSent = await _emailService.SendAccountStatusChangeEmailAsync(
                user.Email,
                user.FullName,
                user.EmployeeCode,
                newStatus,
                dto.Reason.Trim()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email thông báo trạng thái cho {Email}", user.Email);
        }

        var actionText = newStatus == "INACTIVE" ? "Đã khóa tạm thời" : "Đã kích hoạt lại";
        var statusMsg = emailSent
            ? $"{actionText} tài khoản '{user.FullName}' thành công. Email thông báo kèm lý do đã được gửi tới '{user.Email}'."
            : $"{actionText} tài khoản '{user.FullName}' thành công. (Lưu ý: Không gửi được email thông báo tới '{user.Email}' do giới hạn máy chủ Gmail SMTP hoặc địa chỉ email không hợp lệ).";

        return ApiResponse<bool>.Ok(true, statusMsg);
    }

    public async Task<ApiResponse<ResetPasswordResultDto>> ResetPasswordAsync(ulong userId, ResetPasswordDto dto, ulong actorId, string? ipAddress)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return ApiResponse<ResetPasswordResultDto>.Fail("Không tìm thấy người dùng.");

        // Bắt buộc phải có lý do
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return ApiResponse<ResetPasswordResultDto>.Fail("Bắt buộc phải nhập lý do khi cấp lại mật khẩu cho nhân sự.");

        var newPassword = string.IsNullOrWhiteSpace(dto.NewPassword) ? "Password@123" : dto.NewPassword.Trim();
        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogAuditAsync(actorId, "RESET_PASSWORD", "users", user.Id, null, new { Message = "Đã đặt lại mật khẩu", Reason = dto.Reason.Trim() }, ipAddress);

        // Gửi email thông báo mật khẩu mới về email của hồ sơ nhân sự
        bool resetEmailSent = false;
        try
        {
            resetEmailSent = await _emailService.SendPasswordResetNotificationEmailAsync(
                user.Email,
                user.FullName,
                user.EmployeeCode,
                newPassword,
                dto.Reason.Trim()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email cấp lại mật khẩu cho {Email}", user.Email);
        }

        var resetMsg = resetEmailSent
            ? $"Đặt lại mật khẩu thành công. Mật khẩu mới là: {newPassword}. Email thông báo đã được gửi về '{user.Email}'."
            : $"Đặt lại mật khẩu thành công. Mật khẩu mới là: {newPassword}. (Lưu ý: Không gửi được email tới '{user.Email}' do giới hạn máy chủ Gmail SMTP hoặc địa chỉ email không hợp lệ).";

        var resultDto = new ResetPasswordResultDto
        {
            NewPassword = newPassword,
            Email = user.Email,
            EmailSent = resetEmailSent,
            Message = resetMsg
        };

        return ApiResponse<ResetPasswordResultDto>.Ok(resultDto, resetMsg);
    }

    /// <summary>
    /// Soft Delete: đánh dấu Status = "DELETED", xóa chỉ các ca phân công TƯƠNG LAI,
    /// bảo lưu toàn bộ lịch sử chấm công, bàn giao tiền, bàn giao an ninh, bàn giao ca,
    /// và audit log để đối soát kế toán.
    /// Điều kiện: tài khoản phải ở trạng thái INACTIVE và đã qua 14 ngày kể từ khi bị khóa.
    /// </summary>
    public async Task<ApiResponse<bool>> DeleteUserAsync(ulong userId, ulong actorId, string actorRole, string? ipAddress)
    {
        var normalizedRole = actorRole.ToUpper();
        if (normalizedRole != "OPERATIONS_ADMIN" && normalizedRole != "BUSINESS_OWNER" && !normalizedRole.Contains("ADMIN") && !normalizedRole.Contains("OWNER"))
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền thực hiện xóa tài khoản nhân sự.");
        }

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.Status == "DELETED")
            return ApiResponse<bool>.Fail("Không tìm thấy tài khoản người dùng.");

        if (user.Id == actorId)
            return ApiResponse<bool>.Fail("Không thể tự xóa tài khoản của chính mình.");

        // Điều kiện 1: Tài khoản phải ở trạng thái ĐÃ KHÓA (INACTIVE)
        if (user.Status != "INACTIVE")
        {
            return ApiResponse<bool>.Fail("Tài khoản đang hoạt động. Bạn phải khóa tài khoản trước khi thực hiện xóa.");
        }

        // Điều kiện 2: Bắt buộc tuân thủ thời hạn lưu trữ đối soát 14 ngày
        var lastLockedLog = await _context.SystemAuditLogs
            .Where(l => l.TargetTable == "users" && l.TargetId == userId && l.Action == "UPDATE_USER_STATUS" && l.NewValues != null && l.NewValues.Contains("\"INACTIVE\""))
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var lockedTime = lastLockedLog?.Timestamp ?? user.UpdatedAt;
        const int RetentionDays = 14;
        var lockedDuration = DateTime.UtcNow - lockedTime;

        if (lockedDuration.TotalDays < RetentionDays)
        {
            var remainingDays = (int)Math.Ceiling(RetentionDays - lockedDuration.TotalDays);
            var remainingHours = (int)Math.Ceiling((RetentionDays * 24) - lockedDuration.TotalHours);
            var timeText = remainingDays > 1 ? $"{remainingDays} ngày" : $"{remainingHours} giờ";

            return ApiResponse<bool>.Fail(
                $"Tài khoản này mới bị khóa được {(int)lockedDuration.TotalDays} ngày (từ {lockedTime.AddHours(7):dd/MM/yyyy HH:mm}). " +
                $"Theo chính sách đối soát bảng công và tiền lương (bắt buộc áp dụng cho mọi cấp quản trị), tài khoản chỉ có thể xóa vĩnh viễn sau đủ 14 ngày kể từ khi bị khóa (còn {timeText} nữa).");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // 1. Xóa chỉ các ca phân công TƯƠNG LAI (bảo lưu lịch sử chấm công quá khứ)
            var futureAssignmentIds = await _context.ShiftAssignments
                .Where(sa => sa.UserId == userId && sa.Schedule.WorkDate > today)
                .Select(sa => sa.Id)
                .ToListAsync();

            if (futureAssignmentIds.Count > 0)
            {
                // Xóa attendance logs của ca tương lai (chưa có lịch sử thực tế)
                await _context.AttendanceLogs
                    .Where(al => futureAssignmentIds.Contains(al.AssignmentId))
                    .ExecuteDeleteAsync();

                // Xóa swap requests liên quan đến ca tương lai
                await _context.ShiftSwapRequests
                    .Where(ssr => (ssr.RequestingAssignmentId != null && futureAssignmentIds.Contains(ssr.RequestingAssignmentId.Value))
                               || (ssr.TargetAssignmentId != null && futureAssignmentIds.Contains(ssr.TargetAssignmentId.Value)))
                    .ExecuteDeleteAsync();

                // Xóa ca phân công tương lai
                await _context.ShiftAssignments
                    .Where(sa => futureAssignmentIds.Contains(sa.Id))
                    .ExecuteDeleteAsync();
            }

            // 2. Xóa điều động nhân sự (DispatchEmployees & TemporaryDispatches) - vì đây là trạng thái vận hành
            await _context.DispatchEmployees.Where(de => de.UserId == userId).ExecuteDeleteAsync();
            await _context.TemporaryDispatches.Where(td => td.UserId == userId).ExecuteDeleteAsync();

            // 3. Cập nhật các trường khóa ngoại non-nullable sang actorId để tránh lỗi ràng buộc
            await _context.WorkSchedules.Where(ws => ws.CreatedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.CreatedBy, actorId));
            await _context.KioskActivationCodes.Where(k => k.GeneratedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.GeneratedBy, actorId));
            await _context.TemporaryDispatches.Where(td => td.RequestedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.RequestedBy, actorId));

            // 4. Cập nhật các trường khóa ngoại nullable về NULL
            await _context.TemporaryDispatches.Where(td => td.ApprovedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ApprovedBy, (ulong?)null));
            await _context.DispatchEmployees.Where(de => de.ApprovedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ApprovedBy, (ulong?)null));
            await _context.ShiftSwapRequests.Where(ssr => ssr.ReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReviewedBy, (ulong?)null));

            // 5. SOFT DELETE: Đánh dấu Status = "DELETED", KHÔNG xóa bản ghi User khỏi DB
            //    Bảo lưu: AttendanceLogs (quá khứ), CashHandovers, SecurityHandovers,
            //             ShiftHandovers, SystemAuditLogs để đối soát kế toán.
            user.Status = "DELETED";
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi soft-delete tài khoản {FullName} (UserId={UserId})", user.FullName, userId);
            return ApiResponse<bool>.Fail($"Không thể xóa tài khoản: {ex.Message}");
        }

        await LogAuditAsync(actorId, "DELETE_USER", "users", userId,
            new { user.FullName, user.EmployeeCode, user.Email, PreviousStatus = "INACTIVE" },
            new { Action = "SOFT_DELETED", Note = "Bảo lưu lịch sử chấm công, bàn giao tiền, bàn giao an ninh, bàn giao ca và audit log." },
            ipAddress);

        return ApiResponse<bool>.Ok(true, $"Đã xóa tài khoản '{user.FullName}' ({user.EmployeeCode}) thành công. Lịch sử chấm công và bàn giao được bảo lưu để đối soát.");
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
}
