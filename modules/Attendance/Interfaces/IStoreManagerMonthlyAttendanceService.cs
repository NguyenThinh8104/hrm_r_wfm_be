using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

public interface IStoreManagerMonthlyAttendanceService
{
    Task<ApiResponse<StoreManagerMonthlyAttendanceResponseDto>> GetMonthlyAttendanceAsync(
        ulong branchId, int month, int year, ulong? employeeId = null);

    Task<ApiResponse<AttendancePhotoUrlResponseDto>> GetAttendancePhotoUrlAsync(
        ulong branchId, ulong attendanceLogId, string photoType);
}
