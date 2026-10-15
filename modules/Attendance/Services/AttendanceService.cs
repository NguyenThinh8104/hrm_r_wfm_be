using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;

namespace Modules.Attendance.Services;

/// <summary>
/// Facade Service chuyển tiếp các yêu cầu điểm danh đến các service chuyên biệt:
/// - <see cref="IKioskAttendanceService"/>: Luồng máy trạm Kiosk thời gian thực.
/// - <see cref="IAttendanceAuditService"/>: Giám sát quân số và lịch sử điểm danh cửa hàng.
/// - <see cref="IAttendanceSelfService"/>: Lịch tuần và lịch sử chuyên cần cá nhân của nhân viên.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IKioskAttendanceService _kioskAttendanceService;
    private readonly IAttendanceAuditService _attendanceAuditService;
    private readonly IAttendanceSelfService _attendanceSelfService;

    public AttendanceService(
        IKioskAttendanceService kioskAttendanceService,
        IAttendanceAuditService attendanceAuditService,
        IAttendanceSelfService attendanceSelfService)
    {
        _kioskAttendanceService = kioskAttendanceService;
        _attendanceAuditService = attendanceAuditService;
        _attendanceSelfService = attendanceSelfService;
    }

    /// <inheritdoc />
    public Task<ApiResponse<ValidatePinResponseDto>> ValidatePinAsync(ValidatePinRequestDto request)
        => _kioskAttendanceService.ValidatePinAsync(request);

    /// <inheritdoc />
    public Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date)
        => _kioskAttendanceService.GetKioskRosterAsync(storeId, date);

    /// <inheritdoc />
    public Task<ApiResponse<List<KioskEmployeeSearchDto>>> SearchStoreEmployeesAsync(int storeId, string? query = null)
        => _kioskAttendanceService.SearchStoreEmployeesAsync(storeId, query);

    /// <inheritdoc />
    public Task<ApiResponse<AttendanceRecordDto>> CheckInAsync(KioskCheckInDto request)
        => _kioskAttendanceService.CheckInAsync(request);

    /// <inheritdoc />
    public Task<ApiResponse<AttendanceRecordDto>> CheckOutAsync(KioskCheckOutDto request)
        => _kioskAttendanceService.CheckOutAsync(request);

    /// <inheritdoc />
    public Task<ApiResponse<UploadAttendancePhotoResponseDto>> UploadAttendancePhotoAsync(UploadAttendancePhotoDto request)
        => _kioskAttendanceService.UploadAttendancePhotoAsync(request);

    /// <inheritdoc />
    public Task<ApiResponse<bool>> CancelAttendanceAsync(CancelKioskAttendanceDto request)
        => _kioskAttendanceService.CancelAttendanceAsync(request);

    /// <inheritdoc />
    public Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null)
        => _attendanceAuditService.GetLiveRosterAsync(storeId, date);

    /// <inheritdoc />
    public Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date)
        => _attendanceAuditService.GetAttendanceHistoryAsync(storeId, date);

    /// <inheritdoc />
    public Task<ApiResponse<MyWeeklyScheduleDto>> GetMyWeeklyScheduleAsync(ulong userId, DateOnly weekStart)
        => _attendanceSelfService.GetMyWeeklyScheduleAsync(userId, weekStart);

    /// <inheritdoc />
    public Task<ApiResponse<MyAttendanceHistoryDto>> GetMyAttendanceHistoryAsync(ulong userId, int month, int year)
        => _attendanceSelfService.GetMyAttendanceHistoryAsync(userId, month, year);
}
