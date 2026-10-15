using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Shared.Common;

namespace Modules.Shifts.Services;

/// <summary>
/// Facade dịch vụ Shifts tổng hợp, chuyển tiếp cuộc gọi đến các dịch vụ chuyên biệt:
/// - IShiftTemplateService: Quản lý mẫu ca chuẩn toàn hệ thống & chi nhánh.
/// - IScheduleService: Quản lý khung lịch tuần/tháng, định mức nhu cầu nhân sự & tối ưu hóa xếp lịch.
/// - IShiftSwapService: Quản lý quy trình đổi / chuyển ca trực 2 bước & xác thực đồng nghiệp.
/// </summary>
public class ShiftService : IShiftService
{
    private readonly IShiftTemplateService _templateService;
    private readonly IScheduleService _scheduleService;
    private readonly IShiftSwapService _swapService;

    public ShiftService(
        IShiftTemplateService templateService,
        IScheduleService scheduleService,
        IShiftSwapService swapService)
    {
        _templateService = templateService;
        _scheduleService = scheduleService;
        _swapService = swapService;
    }

    #region 1. Quản lý Mẫu Ca Chuẩn (IShiftTemplateService)

    public Task<PagedResult<ShiftTemplateDto>> GetShiftTemplatesPagedAsync(string? scope, ulong? branchId, string? type, bool? isActive, string? q, int page, int pageSize)
        => _templateService.GetShiftTemplatesPagedAsync(scope, branchId, type, isActive, q, page, pageSize);

    public Task<ShiftTemplateStatsDto> GetShiftTemplateStatsAsync()
        => _templateService.GetShiftTemplateStatsAsync();

    public Task<ShiftOperationResult<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateRequest request, ulong? actorId = null, string? ipAddress = null)
        => _templateService.CreateShiftTemplateAsync(request, actorId, ipAddress);

    public Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateRequest request, bool confirm, ulong? actorId = null, string? ipAddress = null)
        => _templateService.UpdateShiftTemplateAsync(id, request, confirm, actorId, ipAddress);

    public Task<ShiftOperationResult<ShiftTemplateDto>> UpdateShiftTemplateActiveAsync(uint id, bool isActive, bool confirm, ulong? actorId = null, string? ipAddress = null)
        => _templateService.UpdateShiftTemplateActiveAsync(id, isActive, confirm, actorId, ipAddress);

    public Task<ShiftOperationResult<BranchEffectiveShiftsDto>> GetBranchEffectiveShiftsAsync(ulong branchId, ulong? currentUserId = null, string? currentUserRole = null)
        => _templateService.GetBranchEffectiveShiftsAsync(branchId, currentUserId, currentUserRole);

    public Task<ShiftOperationResult<List<ShiftTemplateDto>>> GetBranchCustomShiftsAsync(ulong branchId)
        => _templateService.GetBranchCustomShiftsAsync(branchId);

    public Task<ShiftOperationResult<BranchEffectiveShiftsDto>> UpdateBranchShiftModeAsync(ulong branchId, string mode, bool confirm, ulong? actorId = null, string? ipAddress = null)
        => _templateService.UpdateBranchShiftModeAsync(branchId, mode, confirm, actorId, ipAddress);

    public Task<List<BranchSelectorItemDto>> GetBranchesForSelectorAsync(string? shiftMode, string? q)
        => _templateService.GetBranchesForSelectorAsync(shiftMode, q);

    public Task<ApiResponse<ShiftTemplateDto>> CreateShiftTemplateAsync(CreateShiftTemplateDto dto)
        => _templateService.CreateShiftTemplateAsync(dto);

    public Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateAsync(uint id, UpdateShiftTemplateDto dto)
        => _templateService.UpdateShiftTemplateAsync(id, dto);

    public Task<ApiResponse<ShiftTemplateDto>> UpdateShiftTemplateStatusAsync(uint id, UpdateShiftTemplateStatusDto dto)
        => _templateService.UpdateShiftTemplateStatusAsync(id, dto);

    public Task<ApiResponse<ShiftTemplateDto>> GetShiftTemplateByIdAsync(uint id)
        => _templateService.GetShiftTemplateByIdAsync(id);

    public Task<ApiResponse<bool>> DeleteShiftTemplateAsync(uint id)
        => _templateService.DeleteShiftTemplateAsync(id);

    public Task<ApiResponse<List<ShiftTemplateDto>>> GetAllShiftTemplatesAsync(string? status = null)
        => _templateService.GetAllShiftTemplatesAsync(status);

    public Task<ApiResponse<List<ShiftDto>>> GetAllShiftTemplatesAsync(ulong? branchId = null, bool includeInactive = false)
        => _templateService.GetAllShiftTemplatesAsync(branchId, includeInactive);

    public Task<ApiResponse<List<ShiftDto>>> GetAllShiftsAsync()
        => _templateService.GetAllShiftsAsync();

    #endregion

    #region 2. Khung Lịch & Định Mức Nhu Cầu & Xếp Ca (IScheduleService)

    public Task<ApiResponse<List<WorkScheduleDto>>> GenerateMonthlyScheduleAsync(GenerateMonthlyScheduleDto dto, ulong createdByUserId)
        => _scheduleService.GenerateMonthlyScheduleAsync(dto, createdByUserId);

    public Task<ApiResponse<WorkScheduleDto>> UpdateScheduleRequirementAsync(UpdateScheduleRequirementDto dto)
        => _scheduleService.UpdateScheduleRequirementAsync(dto);

    public Task<ApiResponse<bool>> DeleteScheduleAsync(ulong scheduleId)
        => _scheduleService.DeleteScheduleAsync(scheduleId);

    public Task<ApiResponse<List<WorkScheduleDto>>> GetMonthlySchedulesAsync(ulong branchId, int year, int month)
        => _scheduleService.GetMonthlySchedulesAsync(branchId, year, month);

    public Task<ApiResponse<List<ShiftAssignmentDto>>> BatchAssignShiftsAsync(BatchAssignShiftDto dto)
        => _scheduleService.BatchAssignShiftsAsync(dto);

    public Task<ApiResponse<MonthlyScheduleMatrixDto>> GetMonthlyRosterMatrixAsync(ulong branchId, int year, int month)
        => _scheduleService.GetMonthlyRosterMatrixAsync(branchId, year, month);

    public Task<ApiResponse<bool>> PublishMonthlyScheduleAsync(ulong branchId, int year, int month, ulong publishedByUserId)
        => _scheduleService.PublishMonthlyScheduleAsync(branchId, year, month, publishedByUserId);

    public Task<ApiResponse<WeeklyScheduleMatrixDto>> GenerateWeeklyScheduleAsync(GenerateWeeklyScheduleDto dto, ulong createdByUserId)
        => _scheduleService.GenerateWeeklyScheduleAsync(dto, createdByUserId);

    public Task<ApiResponse<WeeklyScheduleMatrixDto>> GetWeeklyScheduleMatrixAsync(ulong branchId, DateOnly weekStartDate)
        => _scheduleService.GetWeeklyScheduleMatrixAsync(branchId, weekStartDate);

    public Task<ApiResponse<List<ShiftAssignmentDto>>> AssignFullTimeBatchAsync(AssignFullTimeBatchDto dto)
        => _scheduleService.AssignFullTimeBatchAsync(dto);

    public Task<ApiResponse<ScheduleConflictCheckResultDto>> CheckWeeklyConflictsAsync(ulong branchId, DateOnly weekStartDate)
        => _scheduleService.CheckWeeklyConflictsAsync(branchId, weekStartDate);

    public Task<ApiResponse<bool>> PublishWeeklyScheduleAsync(ulong branchId, DateOnly weekStartDate, ulong publishedByUserId)
        => _scheduleService.PublishWeeklyScheduleAsync(branchId, weekStartDate, publishedByUserId);

    public Task<ApiResponse<bool>> DeleteShiftAssignmentAsync(ulong assignmentId)
        => _scheduleService.DeleteShiftAssignmentAsync(assignmentId);

    public Task<ApiResponse<AutoScheduleResultDto>> AutoScheduleWeeklyAsync(AutoScheduleWeeklyDto dto, ulong userId)
        => _scheduleService.AutoScheduleWeeklyAsync(dto, userId);

    public Task<ApiResponse<AccessibleBranchesDto>> GetAccessibleBranchesAsync(ulong userId, string role, ulong? storeId)
        => _scheduleService.GetAccessibleBranchesAsync(userId, role, storeId);

    public Task<ulong?> GetBranchIdByAssignmentIdAsync(ulong assignmentId)
        => _scheduleService.GetBranchIdByAssignmentIdAsync(assignmentId);

    public Task<ulong?> GetBranchIdByScheduleIdAsync(ulong scheduleId)
        => _scheduleService.GetBranchIdByScheduleIdAsync(scheduleId);

    public Task<ApiResponse<List<ShiftAssignmentDto>>> GetScheduleAsync(int storeId, DateOnly startDate, DateOnly endDate)
        => _scheduleService.GetScheduleAsync(storeId, startDate, endDate);

    public Task<ApiResponse<ShiftAssignmentDto>> AssignShiftAsync(CreateShiftAssignmentDto request)
        => _scheduleService.AssignShiftAsync(request);

    public Task<ApiResponse<bool>> PublishScheduleAsync(int storeId, DateOnly weekStartDate, int publishedByEmployeeId)
        => _scheduleService.PublishScheduleAsync(storeId, weekStartDate, publishedByEmployeeId);

    public Task<ApiResponse<List<ShiftAssignmentDto>>> GetEmployeeShiftsAsync(int employeeId, DateOnly startDate, DateOnly endDate)
        => _scheduleService.GetEmployeeShiftsAsync(employeeId, startDate, endDate);

    #endregion

    #region 3. Quy Trình Đổi Ca & Chuyển Ca Trực (IShiftSwapService)

    public Task<ApiResponse<ShiftSwapRequestDto>> RequestShiftSwapAsync(int requesterEmployeeId, CreateSwapRequestDto request)
        => _swapService.RequestShiftSwapAsync(requesterEmployeeId, request);

    public Task<ApiResponse<bool>> ReviewShiftSwapAsync(int managerEmployeeId, ReviewSwapRequestDto request)
        => _swapService.ReviewShiftSwapAsync(managerEmployeeId, request);

    public Task<ApiResponse<bool>> RespondToSwapRequestAsync(int peerEmployeeId, PeerReviewSwapRequestDto request)
        => _swapService.RespondToSwapRequestAsync(peerEmployeeId, request);

    public Task<ApiResponse<bool>> CancelSwapRequestAsync(int requesterEmployeeId, int swapRequestId)
        => _swapService.CancelSwapRequestAsync(requesterEmployeeId, swapRequestId);

    public Task<ApiResponse<List<ShiftSwapRequestDto>>> GetSwapRequestsByStoreAsync(int storeId)
        => _swapService.GetSwapRequestsByStoreAsync(storeId);

    public Task<ApiResponse<List<ShiftSwapRequestDto>>> GetMySwapRequestsAsync(int employeeId)
        => _swapService.GetMySwapRequestsAsync(employeeId);

    public Task<ApiResponse<List<ColleagueDto>>> GetColleaguesForSwapAsync(int currentEmployeeId, int branchId, DateOnly? workDate = null)
        => _swapService.GetColleaguesForSwapAsync(currentEmployeeId, branchId, workDate);

    public Task<ApiResponse<List<ColleagueShiftDto>>> GetColleagueShiftsAsync(int currentEmployeeId, int colleagueEmployeeId, int? requestingAssignmentId = null)
        => _swapService.GetColleagueShiftsAsync(currentEmployeeId, colleagueEmployeeId, requestingAssignmentId);

    #endregion
}
