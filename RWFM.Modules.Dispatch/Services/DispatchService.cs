using Microsoft.EntityFrameworkCore;
using RWFM.Domain.Entities;
using RWFM.Modules.Dispatch.DTOs;
using RWFM.Modules.Dispatch.Interfaces;
using RWFM.Shared.Common;
using RWFM.Shared.Data;

namespace RWFM.Modules.Dispatch.Services;

public class DispatchService : IDispatchService
{
    private readonly RWFMDbContext _context;

    public DispatchService(RWFMDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<DispatchRecordDto>> CreateDispatchRequestAsync(int requesterEmployeeId, CreateDispatchRequestDto request)
    {
        if (request.StartDate > request.EndDate)
        {
            return ApiResponse<DispatchRecordDto>.Fail("Ngày bắt đầu điều động không thể sau ngày kết thúc.");
        }

        var employee = await _context.Employees.FindAsync(request.EmployeeId);
        if (employee == null) return ApiResponse<DispatchRecordDto>.Fail("Không tìm thấy nhân viên.");

        var fromStore = await _context.Stores.FindAsync(request.FromStoreId);
        var toStore = await _context.Stores.FindAsync(request.ToStoreId);
        if (fromStore == null || toStore == null) return ApiResponse<DispatchRecordDto>.Fail("Cửa hàng không hợp lệ.");

        var requester = await _context.Employees.FindAsync(requesterEmployeeId);

        var dispatch = new TemporaryDispatch
        {
            EmployeeId = request.EmployeeId,
            FromStoreId = request.FromStoreId,
            ToStoreId = request.ToStoreId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            RequestedBy = requesterEmployeeId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.TemporaryDispatches.Add(dispatch);
        await _context.SaveChangesAsync();

        return ApiResponse<DispatchRecordDto>.Ok(new DispatchRecordDto
        {
            DispatchId = dispatch.DispatchId,
            EmployeeId = employee.EmployeeId,
            EmployeeName = employee.FullName,
            EmployeeCode = employee.EmployeeCode,
            FromStoreId = fromStore.StoreId,
            FromStoreName = fromStore.StoreName,
            ToStoreId = toStore.StoreId,
            ToStoreName = toStore.StoreName,
            StartDate = dispatch.StartDate,
            EndDate = dispatch.EndDate,
            Reason = dispatch.Reason,
            Status = dispatch.Status,
            RequestedByName = requester?.FullName ?? "",
            CreatedAt = dispatch.CreatedAt
        }, "Đã tạo yêu cầu điều động liên chi nhánh thành công.");
    }

    public async Task<ApiResponse<bool>> ReviewDispatchRequestAsync(int approverEmployeeId, ReviewDispatchRequestDto request)
    {
        var dispatch = await _context.TemporaryDispatches
            .Include(d => d.Employee)
            .FirstOrDefaultAsync(d => d.DispatchId == request.DispatchId);

        if (dispatch == null) return ApiResponse<bool>.Fail("Lệnh điều động không tồn tại.");

        if (dispatch.Status != "Pending") return ApiResponse<bool>.Fail("Lệnh này đã được xử lý trước đó.");

        dispatch.Status = request.IsApproved ? "Approved" : "Rejected";
        dispatch.ApprovedBy = approverEmployeeId;
        dispatch.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var msg = request.IsApproved
            ? $"Đã phê duyệt điều động nhân sự {dispatch.Employee.FullName} sang chi nhánh đích."
            : "Đã từ chối lệnh điều động.";

        return ApiResponse<bool>.Ok(true, msg);
    }

    public async Task<ApiResponse<List<DispatchRecordDto>>> GetDispatchesByStoreAsync(int storeId)
    {
        var dispatches = await _context.TemporaryDispatches
            .Include(d => d.Employee)
            .Include(d => d.FromStore)
            .Include(d => d.ToStore)
            .Include(d => d.RequestedByNavigation)
            .Include(d => d.ApprovedByNavigation)
            .Where(d => d.FromStoreId == storeId || d.ToStoreId == storeId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DispatchRecordDto
            {
                DispatchId = d.DispatchId,
                EmployeeId = d.EmployeeId,
                EmployeeName = d.Employee.FullName,
                EmployeeCode = d.Employee.EmployeeCode,
                FromStoreId = d.FromStoreId,
                FromStoreName = d.FromStore.StoreName,
                ToStoreId = d.ToStoreId,
                ToStoreName = d.ToStore.StoreName,
                StartDate = d.StartDate,
                EndDate = d.EndDate,
                Reason = d.Reason,
                Status = d.Status,
                RequestedByName = d.RequestedByNavigation.FullName,
                ApprovedByName = d.ApprovedByNavigation != null ? d.ApprovedByNavigation.FullName : null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<DispatchRecordDto>>.Ok(dispatches);
    }
}
