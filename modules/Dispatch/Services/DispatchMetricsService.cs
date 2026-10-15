using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Modules.Dispatch.DTOs;
using Modules.Dispatch.Interfaces;
using Shared.Common;
using Shared.Data;

namespace Modules.Dispatch.Services;

/// <summary>
/// Triển khai dịch vụ tính toán báo cáo và ma trận điều động toàn chuỗi bán lẻ.
/// </summary>
public class DispatchMetricsService : IDispatchMetricsService
{
    private readonly AppDbContext _context;

    public DispatchMetricsService(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<DispatchNetworkMetricsDto>> GetNetworkMetricsAsync(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var query = _context.TemporaryDispatches
            .Include(d => d.SourceBranch)
            .Include(d => d.TargetBranch)
            .Include(d => d.User).ThenInclude(u => u.Role)
            .Include(d => d.RequestedByUser)
            .Include(d => d.ApprovedByUser)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.User).ThenInclude(u => u.Role)
            .Include(d => d.DispatchEmployees).ThenInclude(de => de.ApprovedByUser)
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
        var approved = allDispatches.Count(d => d.Status == "APPROVED" || d.Status == "PARTIAL");
        var rejected = allDispatches.Count(d => d.Status == "REJECTED");
        var activeToday = allDispatches.Count(d => (d.Status == "APPROVED" || d.Status == "PARTIAL") && d.StartDate <= today && today <= d.EndDate);

        // Tính ma trận điều động theo cặp chi nhánh (Source -> Target)
        var pairMatrix = allDispatches
            .Where(d => d.Status == "APPROVED" || d.Status == "PARTIAL")
            .GroupBy(d => new { d.SourceBranchId, SourceName = d.SourceBranch?.Name ?? "Chưa rõ", d.TargetBranchId, TargetName = d.TargetBranch?.Name ?? "Chưa rõ" })
            .Select(g => new StorePairDispatchMatrixDto
            {
                FromStoreId = (int)g.Key.SourceBranchId,
                FromStoreName = g.Key.SourceName,
                ToStoreId = (int)g.Key.TargetBranchId,
                ToStoreName = g.Key.TargetName,
                DispatchCount = g.Count(),
                TotalHours = g.Sum(d => {
                    int empCount = d.DispatchEmployees.Any(de => de.Status == "APPROVED")
                        ? d.DispatchEmployees.Count(de => de.Status == "APPROVED")
                        : 1;
                    return (d.EndDate.DayNumber - d.StartDate.DayNumber + 1) * 8.0 * empCount;
                })
            })
            .OrderByDescending(p => p.DispatchCount)
            .ToList();

        var totalHours = pairMatrix.Sum(p => p.TotalHours);

        var recentList = allDispatches
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Select(MapToDispatchRecordDto)
            .ToList();

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
