using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Dispatch.Interfaces;
using Shared.Data;

namespace Modules.Dispatch.Services;

public class DispatchSyncService : IDispatchSyncService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DispatchSyncService> _logger;

    public DispatchSyncService(AppDbContext context, ILogger<DispatchSyncService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Đồng bộ chi nhánh nhân sự theo lệnh điều động:
    /// - Khi trong thời hạn điều động: Cập nhật HomeBranchId = TargetBranchId, lưu OriginalHomeBranchId = chi nhánh ban đầu.
    /// - Khi hết hạn điều động: Trả HomeBranchId = OriginalHomeBranchId, đặt OriginalHomeBranchId = null.
    /// </summary>
    public async Task SyncDispatchesAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            // 1. Lấy tất cả các lệnh điều động đang có hiệu lực hôm nay (APPROVED hoặc PARTIAL)
            var activeDispatches = await _context.TemporaryDispatches
                .Include(d => d.DispatchEmployees)
                .Where(d => (d.Status == "APPROVED" || d.Status == "PARTIAL")
                         && d.StartDate <= today
                         && today <= d.EndDate)
                .ToListAsync();

            // Tập hợp các UserId đang trong thời hạn điều động hiệu lực cùng TargetBranchId
            var activeUserTargets = new Dictionary<ulong, ulong>(); // UserId -> TargetBranchId
            foreach (var d in activeDispatches)
            {
                if (d.DispatchEmployees != null && d.DispatchEmployees.Any())
                {
                    foreach (var de in d.DispatchEmployees.Where(de => de.Status == "APPROVED"))
                    {
                        activeUserTargets[de.UserId] = d.TargetBranchId;
                    }
                }
                else if (d.UserId > 0 && d.Status == "APPROVED")
                {
                    activeUserTargets[d.UserId] = d.TargetBranchId;
                }
            }

            bool hasChanges = false;

            // 2. Chuyển chi nhánh cho các nhân sự đang trong thời hạn điều động
            if (activeUserTargets.Any())
            {
                var userIdsToMove = activeUserTargets.Keys.ToList();
                var usersToMove = await _context.Users
                    .Where(u => userIdsToMove.Contains(u.Id))
                    .ToListAsync();

                foreach (var user in usersToMove)
                {
                    var targetBranchId = activeUserTargets[user.Id];
                    if (user.HomeBranchId != targetBranchId)
                    {
                        // Lưu lại chi nhánh ban đầu nếu chưa từng lưu
                        user.OriginalHomeBranchId ??= user.HomeBranchId;
                        user.HomeBranchId = targetBranchId;
                        user.UpdatedAt = DateTime.UtcNow;
                        hasChanges = true;

                        _logger.LogInformation("Nhân sự {EmployeeCode} ({FullName}) đã được tự động chuyển sang chi nhánh #{TargetBranchId} theo lệnh điều động có hiệu lực.",
                            user.EmployeeCode, user.FullName, targetBranchId);
                    }
                }
            }

            // 3. Hoàn trả các nhân sự đã HẾT HẠN điều động về lại chi nhánh ban đầu
            var usersWithOriginalBranch = await _context.Users
                .Where(u => u.OriginalHomeBranchId != null)
                .ToListAsync();

            foreach (var user in usersWithOriginalBranch)
            {
                // Nếu nhân sự này KHÔNG còn nằm trong danh sách lệnh điều động còn hiệu lực hôm nay
                if (!activeUserTargets.ContainsKey(user.Id) && user.OriginalHomeBranchId.HasValue)
                {
                    var returnBranchId = user.OriginalHomeBranchId.Value;
                    user.HomeBranchId = returnBranchId;
                    user.OriginalHomeBranchId = null;
                    user.UpdatedAt = DateTime.UtcNow;
                    hasChanges = true;

                    _logger.LogInformation("Nhân sự {EmployeeCode} ({FullName}) đã hết thời hạn điều động, được tự động hoàn trả về chi nhánh ban đầu #{ReturnBranchId}.",
                        user.EmployeeCode, user.FullName, returnBranchId);
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình đồng bộ điều chuyển nhân sự chi nhánh (DispatchSyncService).");
        }
    }
}
