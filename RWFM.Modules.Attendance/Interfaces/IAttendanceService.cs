using RWFM.Modules.Attendance.DTOs;
using RWFM.Shared.Common;

namespace RWFM.Modules.Attendance.Interfaces;

public interface IAttendanceService
{
    Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date);
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckInAsync(KioskPinCheckInDto request);
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request);
    Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request);
    Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date);
}
