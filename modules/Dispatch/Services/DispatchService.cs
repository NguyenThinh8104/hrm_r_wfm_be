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

    public async Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request)
    {
        if (request.StartDate > request.EndDate)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Ngày bắt đầu điều động không thể sau ngày kết thúc.");
        }

        var user = await _context.Users.FindAsync((ulong)request.EmployeeId);
        if (user == null) return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy nhân viên.");

        var fromBranch = await _context.Branches.FindAsync((ulong)request.FromStoreId);
        var toBranch = await _context.Branches.FindAsync((ulong)request.ToStoreId);
        if (fromBranch == null || toBranch == null) return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng không hợp lệ.");

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

    public async Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == (ulong)request.DispatchId);

        if (dispatch == null) return ApiResponse<bool>.Fail("Lệnh điều động không tồn tại.");

        if (dispatch.Status != "PENDING") return ApiResponse<bool>.Fail("Lệnh này đã được xử lý trước đó.");

        dispatch.Status = request.IsApproved ? "APPROVED" : "REJECTED";
        dispatch.ApprovedBy = (ulong)approverEmployeeId;

        await _context.SaveChangesAsync();

        var msg = request.IsApproved
            ? $"Đã phê duyệt điều động nhân sự {dispatch.User.FullName} sang chi nhánh đích."
            : "Đã từ chối lệnh điều động.";

        return ApiResponse<bool>.Ok(true, msg);
    }

    public async Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId)
    {
        var dispatches = await _context.TemporaryDispatches
            .Include(d => d.User)
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
}
