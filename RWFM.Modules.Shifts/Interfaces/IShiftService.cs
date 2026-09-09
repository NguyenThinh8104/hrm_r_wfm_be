using RWFM.Modules.Shifts.DTOs;
using RWFM.Shared.Common;

namespace RWFM.Modules.Shifts.Interfaces;

public interface IShiftService
{
    Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync();
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate);
    Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request);
    Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId);
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate);
    Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request);
    Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request);
    Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId);
}
