using Microsoft.EntityFrameworkCore;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Dispatch.Services;

/// <summary>
/// Triển khai dịch vụ tính toán hạn mức định biên điều động an toàn và lọc nhân sự đủ điều kiện.
/// </summary>
public class DispatchQuotaService : IDispatchQuotaService
{
    private readonly AppDbContext _context;

    public DispatchQuotaService(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<BranchDispatchQuotaStatusDto>> GetBranchDispatchQuotaAsync(
        ulong branchId, 
        DateOnly? startDate, 
        DateOnly? endDate, 
        ulong? excludeDispatchId = null)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<BranchDispatchQuotaStatusDto>.Fail("Không tìm thấy chi nhánh chỉ định.");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var queryStart = startDate ?? today;
        var queryEnd = endDate ?? (startDate.HasValue ? startDate.Value : today);

        // Tổng quân số ACTIVE của chi nhánh gốc
        var totalHeadcount = await _context.Users
            .CountAsync(u => ((u.OriginalHomeBranchId ?? u.HomeBranchId) == branchId) && u.Status == "ACTIVE");

        // Tìm các lệnh điều động APPROVED hoặc PARTIAL từ chi nhánh này trùng với khoảng thời gian [queryStart, queryEnd]
        var overlappingDispatches = await _context.TemporaryDispatches
            .Include(d => d.DispatchEmployees)
            .Where(d => d.SourceBranchId == branchId
                     && (d.Status == "APPROVED" || d.Status == "PARTIAL")
                     && (!excludeDispatchId.HasValue || d.Id != excludeDispatchId.Value)
                     && d.StartDate <= queryEnd
                     && queryStart <= d.EndDate)
            .ToListAsync();

        var dispatchedUserIds = new HashSet<ulong>();
        foreach (var d in overlappingDispatches)
        {
            if (d.DispatchEmployees != null && d.DispatchEmployees.Any())
            {
                foreach (var de in d.DispatchEmployees.Where(de => de.Status == "APPROVED"))
                {
                    dispatchedUserIds.Add(de.UserId);
                }
            }
            else if (d.UserId > 0 && d.Status == "APPROVED")
            {
                dispatchedUserIds.Add(d.UserId);
            }
        }

        int currentlyDispatchedCount = dispatchedUserIds.Count;
        int maxAllowedDispatchCount = (int)Math.Floor(totalHeadcount * 0.40);
        int availableDispatchQuota = Math.Max(0, maxAllowedDispatchCount - currentlyDispatchedCount);

        var dto = new BranchDispatchQuotaStatusDto
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            TotalHeadcount = totalHeadcount,
            CurrentlyDispatchedCount = currentlyDispatchedCount,
            MaxAllowedDispatchCount = maxAllowedDispatchCount,
            AvailableDispatchQuota = availableDispatchQuota,
            MaxDispatchRatio = 0.40
        };

        return ApiResponse<BranchDispatchQuotaStatusDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<List<DispatchEmployeeOptionDto>>> GetBranchEmployeesForDispatchAsync(ulong branchId)
    {
        var branch = await _context.Branches.FindAsync(branchId);
        if (branch == null)
        {
            return ApiResponse<List<DispatchEmployeeOptionDto>>.Fail("Không tìm thấy chi nhánh chỉ định.");
        }

        var excludedRoleCodes = new[] { "STORE_MANAGER", "OPERATIONS_ADMIN", "BUSINESS_OWNER" };

        var employees = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.HomeBranchId == branchId && u.Status == "ACTIVE")
            .Where(u => u.Role != null && !excludedRoleCodes.Contains(u.Role.RoleCode))
            .OrderBy(u => u.RoleId)
            .ThenBy(u => u.FullName)
            .Select(u => new DispatchEmployeeOptionDto
            {
                Id = u.Id,
                EmployeeCode = u.EmployeeCode,
                FullName = u.FullName,
                RoleName = u.Role != null ? u.Role.RoleName : "Nhân viên",
                PositionName = u.Role != null ? u.Role.RoleName : "Nhân viên",
                RoleCode = u.Role != null ? u.Role.RoleCode : string.Empty,
                HomeBranchId = u.HomeBranchId ?? branchId
            })
            .ToListAsync();

        return ApiResponse<List<DispatchEmployeeOptionDto>>.Ok(employees, "Lấy danh sách nhân sự chi viện thành công.");
    }
}
