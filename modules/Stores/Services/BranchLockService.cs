using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Domain.Entities;
using Domain.Enums;
using Modules.Stores.DTOs;
using Modules.Stores.Interfaces;
using Shared.Data;

namespace Modules.Stores.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ kiểm tra điều kiện chặn, Khóa và Mở khóa chi nhánh cửa hàng.
/// </summary>
public class BranchLockService : IBranchLockService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BranchLockService> _logger;

    public BranchLockService(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<BranchLockService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra toàn bộ điều kiện chặn trước khi khóa chi nhánh (Không làm thay đổi dữ liệu).
    /// Trả về canLock, danh sách blockers và affectedEmployeeCount.
    /// </summary>
    public async Task<BranchLockCheckResponseDto> CheckLockConditionsAsync(ulong branchId, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        if (branch == null)
        {
            return new BranchLockCheckResponseDto
            {
                CanLock = false,
                Blockers = new List<BranchLockBlockerDto>(),
                AffectedEmployeeCount = 0
            };
        }

        var blockers = new List<BranchLockBlockerDto>();

        // 1. Blocker: ACTIVE_CHECKIN (còn nhân viên check-in chưa check-out tại chi nhánh)
        var activeCheckIns = await _context.AttendanceLogs
            .AsNoTracking()
            .Include(al => al.Assignment)
                .ThenInclude(sa => sa.User)
            .Where(al => al.BranchId == branchId && al.CheckInTime != default && al.CheckOutTime == null)
            .ToListAsync(cancellationToken);

        if (activeCheckIns.Count > 0)
        {
            blockers.Add(new BranchLockBlockerDto
            {
                Code = "ACTIVE_CHECKIN",
                Message = $"Còn {activeCheckIns.Count} nhân viên đã check-in chưa check-out tại chi nhánh.",
                Count = activeCheckIns.Count,
                Items = activeCheckIns.Select(al => new BranchLockBlockerItemDto
                {
                    Id = (al.Assignment?.UserId ?? al.Id).ToString(),
                    Name = al.Assignment?.User?.FullName ?? $"Lượt chấm công #{al.Id}"
                }).ToList()
            });
        }

        // 2. Blocker: ONGOING_SHIFT (có ca đang diễn ra tại thời điểm khóa)
        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var yesterday = today.AddDays(-1);

        var candidateSchedules = await _context.WorkSchedules
            .AsNoTracking()
            .Include(ws => ws.ShiftTemplate)
            .Where(ws => ws.BranchId == branchId &&
                         ws.Status != "CANCELLED" &&
                         (ws.WorkDate == today || (ws.WorkDate == yesterday && ws.ShiftTemplate.IsOvernight)))
            .ToListAsync(cancellationToken);

        var ongoingSchedules = candidateSchedules.Where(ws =>
        {
            var t = ws.ShiftTemplate;
            if (t == null) return false;

            var start = ws.WorkDate.ToDateTime(t.StartTime);
            var end = (t.IsOvernight || t.EndTime < t.StartTime)
                ? ws.WorkDate.AddDays(1).ToDateTime(t.EndTime)
                : ws.WorkDate.ToDateTime(t.EndTime);

            return now >= start && now <= end;
        }).ToList();

        if (ongoingSchedules.Count > 0)
        {
            blockers.Add(new BranchLockBlockerDto
            {
                Code = "ONGOING_SHIFT",
                Message = $"Có {ongoingSchedules.Count} ca làm việc đang diễn ra tại thời điểm khóa.",
                Count = ongoingSchedules.Count,
                Items = ongoingSchedules.Select(ws => new BranchLockBlockerItemDto
                {
                    Id = ws.Id.ToString(),
                    Name = $"{ws.ShiftTemplate?.Name ?? "Ca làm việc"} ({ws.WorkDate:dd/MM/yyyy} {ws.ShiftTemplate?.StartTime:HH:mm}-{ws.ShiftTemplate?.EndTime:HH:mm})"
                }).ToList()
            });
        }

        // 3. Blocker: PENDING_REQUESTS (còn đơn chờ duyệt thuộc chi nhánh: đổi ca, chuyển ca, nghỉ phép, tăng ca, điều động, định biên)
        var pendingItems = new List<BranchLockBlockerItemDto>();

        // 3a. ShiftSwapRequests (Nghỉ phép / Đổi ca / Chuyển ca)
        var pendingSwapRequests = await _context.ShiftSwapRequests
            .AsNoTracking()
            .Include(r => r.Schedule)
            .Include(r => r.RequestingAssignment)
                .ThenInclude(ra => ra!.Schedule)
            .Include(r => r.RequesterUser)
            .Where(r => r.Status == "PENDING" &&
                        ((r.Schedule != null && r.Schedule.BranchId == branchId) ||
                         (r.RequestingAssignment != null && r.RequestingAssignment.Schedule.BranchId == branchId) ||
                         (r.RequesterUser != null && r.RequesterUser.HomeBranchId == branchId)))
            .ToListAsync(cancellationToken);

        foreach (var r in pendingSwapRequests)
        {
            var reqType = r.RequestType switch
            {
                "LEAVE" => "Nghỉ phép",
                "SWAP" => "Đổi ca",
                "TRANSFER" => "Chuyển ca",
                _ => "Đổi/nghỉ ca"
            };
            pendingItems.Add(new BranchLockBlockerItemDto
            {
                Id = $"SWAP-{r.Id}",
                Name = $"Đơn {reqType} #{r.Id} - {r.RequesterUser?.FullName ?? "Nhân viên"}"
            });
        }

        // 3b. TemporaryDispatches (Điều động nhân sự đi/đến chi nhánh)
        var pendingDispatches = await _context.TemporaryDispatches
            .AsNoTracking()
            .Include(td => td.User)
            .Where(td => td.Status == "PENDING" && (td.SourceBranchId == branchId || td.TargetBranchId == branchId))
            .ToListAsync(cancellationToken);

        foreach (var td in pendingDispatches)
        {
            pendingItems.Add(new BranchLockBlockerItemDto
            {
                Id = $"DISPATCH-{td.Id}",
                Name = $"Đơn điều động #{td.Id} - {td.User?.FullName ?? "Nhân sự"}"
            });
        }

        if (pendingItems.Count > 0)
        {
            blockers.Add(new BranchLockBlockerDto
            {
                Code = "PENDING_REQUESTS",
                Message = $"Còn {pendingItems.Count} đơn chờ duyệt (nghỉ phép, đổi ca, tăng ca, điều động) thuộc chi nhánh.",
                Count = pendingItems.Count,
                Items = pendingItems
            });
        }

        // Số lượng nhân sự bị ảnh hưởng trực tiếp (nhân sự có HomeBranchId là chi nhánh này)
        var affectedEmployeeCount = await _context.Users
            .CountAsync(u => u.HomeBranchId == branchId && u.Status == "ACTIVE", cancellationToken);

        return new BranchLockCheckResponseDto
        {
            CanLock = blockers.Count == 0,
            Blockers = blockers,
            AffectedEmployeeCount = affectedEmployeeCount
        };
    }

    /// <summary>
    /// Thực hiện khóa chi nhánh kèm kiểm tra điều kiện chặn trong cùng transaction.
    /// </summary>
    public async Task<BranchLockOperationResult> LockBranchAsync(
        ulong branchId,
        LockBranchRequestDto request,
        string? performedBy,
        ulong? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra request cơ bản
        if (request == null)
        {
            return BranchLockOperationResult.BadRequest("Dữ liệu yêu cầu khóa chi nhánh không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10 || request.Reason.Trim().Length > 500)
        {
            return BranchLockOperationResult.BadRequest("Lý do khóa chi nhánh là bắt buộc và phải có độ dài từ 10 đến 500 ký tự.");
        }

        var branch = await _context.Branches
            .Include(b => b.Kiosks)
            .Include(b => b.Tier)
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        if (branch == null)
        {
            return BranchLockOperationResult.NotFound($"Không tìm thấy chi nhánh với ID {branchId}.");
        }

        // 2. Validate confirmBranchCode phải khớp mã chi nhánh
        if (string.IsNullOrWhiteSpace(request.ConfirmBranchCode) ||
            !string.Equals(request.ConfirmBranchCode.Trim(), branch.BranchCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BranchLockOperationResult.BadRequest($"Mã xác nhận '{request.ConfirmBranchCode}' không khớp với mã chi nhánh '{branch.BranchCode}'.");
        }

        // 3. Không cho khóa nếu chi nhánh đã ở trạng thái khóa (400)
        if (string.Equals(branch.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(branch.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            return BranchLockOperationResult.BadRequest($"Chi nhánh '{branch.Name}' đã ở trạng thái tạm khóa.");
        }

        // 4. Nếu Transfer staff hoặc Transfer shifts thì bắt buộc transferToBranchId, chi nhánh đích phải đang Hoạt động
        var isTransferStaff = string.Equals(request.StaffHandlingMode, "TransferTemporarily", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(request.StaffHandlingMode, "TRANSFER", StringComparison.OrdinalIgnoreCase);
        var isTransferShifts = string.Equals(request.FutureShiftHandling, "Transfer", StringComparison.OrdinalIgnoreCase);

        Branch? targetBranch = null;
        if (isTransferStaff || isTransferShifts)
        {
            if (!request.TransferToBranchId.HasValue || request.TransferToBranchId.Value == 0)
            {
                var actionMsg = isTransferStaff
                    ? "chuyển nhân sự tạm thời (TransferTemporarily)"
                    : "chuyển ca làm việc tương lai sang chi nhánh khác (Transfer)";
                return BranchLockOperationResult.BadRequest($"Khi chọn {actionMsg}, bắt buộc phải chọn chi nhánh đích (transferToBranchId).");
            }

            if (request.TransferToBranchId.Value == branch.Id)
            {
                return BranchLockOperationResult.BadRequest("Chi nhánh đích không thể trùng với chi nhánh đang khóa.");
            }

            targetBranch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.TransferToBranchId.Value, cancellationToken);

            if (targetBranch == null)
            {
                return BranchLockOperationResult.BadRequest($"Chi nhánh đích nhận nhân sự/ca làm việc với ID {request.TransferToBranchId.Value} không tồn tại.");
            }

            if (!string.Equals(targetBranch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                return BranchLockOperationResult.BadRequest($"Chi nhánh đích '{targetBranch.Name}' hiện không ở trạng thái Hoạt động. Không thể chuyển dữ liệu sang.");
            }
        }

        // 5. Mở Transaction và thực hiện kiểm tra blockers cùng lúc với cập nhật trạng thái
        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            // 5a. Chạy lại toàn bộ kiểm tra như lock-check trong cùng transaction
            var lockCheck = await CheckLockConditionsAsync(branchId, cancellationToken);
            if (!lockCheck.CanLock)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                return BranchLockOperationResult.Conflict(lockCheck.Blockers);
            }

            var now = DateTime.UtcNow;
            var actor = !string.IsNullOrWhiteSpace(performedBy) ? performedBy.Trim() : "System";

            // 5b. Đổi status chi nhánh, ghi LockedAt/By/Reason và cập nhật RowVersion (concurrency token)
            branch.Status = "INACTIVE";
            branch.LockedAt = now;
            branch.LockedBy = actor;
            branch.LockReason = request.Reason.Trim();
            branch.RowVersion = Guid.NewGuid();
            branch.UpdatedAt = now;

            // Chuyển toàn bộ Kiosk của chi nhánh sang trạng thái BLOCKED
            foreach (var kiosk in branch.Kiosks)
            {
                kiosk.Status = "BLOCKED";
                kiosk.UpdatedAt = now;
            }

            // 5c. Xử lý nhân sự theo StaffHandlingMode
            var activeStaff = await _context.Users
                .Where(u => u.HomeBranchId == branchId && u.Status == "ACTIVE")
                .ToListAsync(cancellationToken);

            if (isTransferStaff && request.TransferToBranchId.HasValue)
            {
                var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
                var callerId = performedByUserId ?? (ulong)1;

                foreach (var user in activeStaff)
                {
                    _context.TemporaryDispatches.Add(new TemporaryDispatch
                    {
                        UserId = user.Id,
                        SourceBranchId = branch.Id,
                        TargetBranchId = request.TransferToBranchId.Value,
                        StartDate = today,
                        EndDate = today.AddMonths(1),
                        Status = "APPROVED",
                        RequestedBy = callerId,
                        ApprovedBy = callerId,
                        Note = $"Tự động điều động tạm thời khi khóa chi nhánh {branch.Name} ({branch.BranchCode})",
                        CreatedAt = now
                    });
                }
            }
            else
            {
                foreach (var user in activeStaff)
                {
                    user.Status = "SUSPENDED";
                    user.UpdatedAt = now;
                }
            }

            // 5d. Xử lý ca làm việc tương lai theo FutureShiftHandling
            var todayDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
            var futureSchedules = await _context.WorkSchedules
                .Include(ws => ws.ShiftAssignments)
                .Where(ws => ws.BranchId == branchId && ws.WorkDate >= todayDate && ws.Status != "CANCELLED")
                .ToListAsync(cancellationToken);

            if (string.Equals(request.FutureShiftHandling, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var ws in futureSchedules)
                {
                    ws.Status = "CANCELLED";
                    foreach (var sa in ws.ShiftAssignments)
                    {
                        sa.Status = "CANCELLED";
                    }
                }
            }
            else if (string.Equals(request.FutureShiftHandling, "Suspend", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var ws in futureSchedules)
                {
                    ws.Status = "SUSPENDED";
                    foreach (var sa in ws.ShiftAssignments)
                    {
                        sa.Status = "SUSPENDED";
                    }
                }
            }
            else if (isTransferShifts && request.TransferToBranchId.HasValue)
            {
                var targetId = request.TransferToBranchId.Value;
                foreach (var ws in futureSchedules)
                {
                    var existsAtTarget = await _context.WorkSchedules.AnyAsync(
                        targetWs => targetWs.BranchId == targetId &&
                                    targetWs.ShiftTemplateId == ws.ShiftTemplateId &&
                                    targetWs.WorkDate == ws.WorkDate, cancellationToken);

                    if (!existsAtTarget)
                    {
                        ws.BranchId = targetId;
                    }
                    else
                    {
                        ws.Status = "TRANSFERRED";
                    }
                }
            }

            // 5e. Ghi BranchLockLog
            var normalizedStaffMode = isTransferStaff ? "TransferTemporarily" : "KeepAndBlock";
            var normalizedFutureShiftMode = string.Equals(request.FutureShiftHandling, "Suspend", StringComparison.OrdinalIgnoreCase)
                ? "Suspend"
                : (isTransferShifts ? "Transfer" : "Cancel");

            var log = new BranchLockLog
            {
                BranchId = branch.Id,
                Action = "Lock",
                Reason = request.Reason.Trim(),
                PerformedBy = actor,
                PerformedAt = now,
                AffectedEmployeeCount = lockCheck.AffectedEmployeeCount,
                StaffHandlingMode = normalizedStaffMode,
                FutureShiftHandling = normalizedFutureShiftMode,
                TransferredToBranchId = request.TransferToBranchId
            };
            _context.BranchLockLogs.Add(log);

            // 5f. Lưu thay đổi và commit transaction
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Khóa chi nhánh {BranchCode} (ID: {BranchId}) thành công bởi {PerformedBy}", branch.BranchCode, branch.Id, actor);

            return BranchLockOperationResult.Ok(MapToBranchDto(branch), $"Đã khóa chi nhánh '{branch.Name}' ({branch.BranchCode}) thành công.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Xung đột đồng thời khi khóa chi nhánh {BranchId}", branchId);
            return BranchLockOperationResult.ConcurrencyConflict();
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi khi thực hiện khóa chi nhánh {BranchId}", branchId);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Thực hiện mở khóa chi nhánh và khôi phục hoạt động cho chi nhánh, Kiosk và nhân sự.
    /// </summary>
    public async Task<BranchLockOperationResult> UnlockBranchAsync(
        ulong branchId,
        UnlockBranchRequestDto request,
        string? performedBy,
        ulong? performedByUserId,
        CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches
            .Include(b => b.Kiosks)
            .Include(b => b.Tier)
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        if (branch == null)
        {
            return BranchLockOperationResult.NotFound($"Không tìm thấy chi nhánh với ID {branchId}.");
        }

        // Không cho mở khóa nếu chi nhánh đã ở trạng thái Hoạt động
        if (string.Equals(branch.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return BranchLockOperationResult.BadRequest($"Chi nhánh '{branch.Name}' hiện đang ở trạng thái Hoạt động, không cần mở khóa.");
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var now = DateTime.UtcNow;
            var actor = !string.IsNullOrWhiteSpace(performedBy) ? performedBy.Trim() : "System";

            // 1. Đổi lại Hoạt động, ghi UnlockedAt/UnlockedBy và cập nhật RowVersion
            branch.Status = "ACTIVE";
            branch.UnlockedAt = now;
            branch.UnlockedBy = actor;
            branch.RowVersion = Guid.NewGuid();
            branch.UpdatedAt = now;

            // 2. Mở khóa lại các trạm Kiosk của chi nhánh
            foreach (var kiosk in branch.Kiosks)
            {
                kiosk.Status = "ACTIVE";
                kiosk.UpdatedAt = now;
            }

            // 3. Khôi phục lại trạng thái cho các nhân sự từng bị đình chỉ bởi KeepAndBlock
            var suspendedUsers = await _context.Users
                .Where(u => u.HomeBranchId == branchId && u.Status == "SUSPENDED")
                .ToListAsync(cancellationToken);

            foreach (var user in suspendedUsers)
            {
                user.Status = "ACTIVE";
                user.UpdatedAt = now;
            }

            var affectedEmployeeCount = await _context.Users
                .CountAsync(u => u.HomeBranchId == branchId, cancellationToken);

            // 4. Ghi BranchLockLog
            var unlockLog = new BranchLockLog
            {
                BranchId = branch.Id,
                Action = "Unlock",
                Reason = request?.Reason?.Trim(),
                PerformedBy = actor,
                PerformedAt = now,
                AffectedEmployeeCount = affectedEmployeeCount,
                StaffHandlingMode = null,
                FutureShiftHandling = null,
                TransferredToBranchId = null
            };
            _context.BranchLockLogs.Add(unlockLog);

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation("Mở khóa chi nhánh {BranchCode} (ID: {BranchId}) thành công bởi {PerformedBy}", branch.BranchCode, branch.Id, actor);

            return BranchLockOperationResult.Ok(MapToBranchDto(branch), $"Đã mở khóa chi nhánh '{branch.Name}' ({branch.BranchCode}) thành công.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Xung đột đồng thời khi mở khóa chi nhánh {BranchId}", branchId);
            return BranchLockOperationResult.ConcurrencyConflict();
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Lỗi khi mở khóa chi nhánh {BranchId}", branchId);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private static BranchDto MapToBranchDto(Branch b)
    {
        var kiosks = b.Kiosks ?? new List<KioskDevice>();
        return new BranchDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Address = b.Address,
            Latitude = b.Latitude,
            Longitude = b.Longitude,
            GeofenceRadiusMeters = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 50,
            BranchTier = b.BranchTier,
            StaffCount = b.StaffCount,
            TierId = b.TierId,
            Tier = b.Tier != null ? new TierDto
            {
                Id = b.Tier.Id,
                TierName = b.Tier.TierName,
                Description = b.Tier.Description,
                MinStaffCount = b.Tier.MinStaffCount,
                MaxStaffCount = b.Tier.MaxStaffCount,
                OtherConditions = b.Tier.OtherConditions,
                Conditions = b.Tier.Conditions,
                Benefits = b.Tier.Benefits,
                CreatedAt = b.Tier.CreatedAt,
                UpdatedAt = b.Tier.UpdatedAt
            } : null,
            Status = b.Status,
            LockedAt = b.LockedAt,
            LockedBy = b.LockedBy,
            LockReason = b.LockReason,
            UnlockedAt = b.UnlockedAt,
            UnlockedBy = b.UnlockedBy,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            TotalKiosks = kiosks.Count,
            ActiveKiosks = kiosks.Count(k => k.Status == "ACTIVE"),
            Kiosks = kiosks.Select(k => new KioskDto
            {
                Id = k.Id,
                BranchId = k.BranchId,
                BranchCode = b.Code,
                BranchName = b.Name,
                DeviceName = k.Name,
                KioskCode = k.KioskCode,
                IpWhitelist = k.IpAddress,
                KioskToken = k.DeviceToken,
                Status = k.Status,
                LastPingAt = k.LastPingAt,
                CreatedAt = k.CreatedAt,
                UpdatedAt = k.UpdatedAt
            }).ToList()
        };
    }
}
