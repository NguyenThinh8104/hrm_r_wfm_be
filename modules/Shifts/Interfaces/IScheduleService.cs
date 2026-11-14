using Modules.Shifts.DTOs;
using Shared.Common;

namespace Modules.Shifts.Interfaces;

/// <summary>
/// Interface dịch vụ quản lý khung lịch tuần/tháng, định mức nhu cầu nhân sự và phân bổ ca làm việc (UC 2.1 & UC 2.3).
/// </summary>
public interface IScheduleService
{
    // =========================================================
    // 1. Khởi Tạo Khung Lịch & Định Mức Nhu Cầu (Store Manager & Admin)
    // =========================================================

    Task<ApiResponse<List<WorkScheduleDto>>> GenerateMonthlyScheduleAsync(GenerateMonthlyScheduleDto dto, ulong createdByUserId);
    Task<ApiResponse<WorkScheduleDto>> UpdateScheduleRequirementAsync(UpdateScheduleRequirementDto dto);
    Task<ApiResponse<bool>> DeleteScheduleAsync(ulong scheduleId);
    Task<ApiResponse<List<WorkScheduleDto>>> GetMonthlySchedulesAsync(ulong branchId, int year, int month);

    // =========================================================
    // 2. Phân Bổ Nhân Sự & Công Bố Lịch Tháng
    // =========================================================

    Task<ApiResponse<List<ShiftAssignmentDto>>> BatchAssignShiftsAsync(BatchAssignShiftDto dto);
    Task<ApiResponse<MonthlyScheduleMatrixDto>> GetMonthlyRosterMatrixAsync(ulong branchId, int year, int month);
    Task<ApiResponse<bool>> PublishMonthlyScheduleAsync(ulong branchId, int year, int month, ulong publishedByUserId);

    // =========================================================
    // 3. Quản Lý Lịch Tuần & Xung Đột & Công Bố Tuần (UC 2.1 & UC 2.3)
    // =========================================================

    Task<ApiResponse<WeeklyScheduleMatrixDto>> GenerateWeeklyScheduleAsync(GenerateWeeklyScheduleDto dto, ulong createdByUserId);
    Task<ApiResponse<WeeklyScheduleMatrixDto>> GetWeeklyScheduleMatrixAsync(ulong branchId, DateOnly weekStartDate);
    Task<ApiResponse<List<ShiftAssignmentDto>>> AssignFullTimeBatchAsync(AssignFullTimeBatchDto dto);
    Task<ApiResponse<ScheduleConflictCheckResultDto>> CheckWeeklyConflictsAsync(ulong branchId, DateOnly weekStartDate);
    Task<ApiResponse<bool>> PublishWeeklyScheduleAsync(ulong branchId, DateOnly weekStartDate, ulong publishedByUserId);
    Task<ApiResponse<bool>> DeleteShiftAssignmentAsync(ulong assignmentId);
    Task<ApiResponse<AutoScheduleResultDto>> AutoScheduleWeeklyAsync(AutoScheduleWeeklyDto dto, ulong userId);
    Task<ApiResponse<AccessibleBranchesDto>> GetAccessibleBranchesAsync(ulong userId, string role, ulong? storeId);
    Task<ulong?> GetBranchIdByAssignmentIdAsync(ulong assignmentId);
    Task<ulong?> GetBranchIdByScheduleIdAsync(ulong scheduleId);

    // =========================================================
    // 4. Lịch Phân Công Nhân Sự Cá Nhân & Phân Ca Đơn Lẻ
    // =========================================================

    Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate);
    Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request);
    Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId);
    Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate);
}
