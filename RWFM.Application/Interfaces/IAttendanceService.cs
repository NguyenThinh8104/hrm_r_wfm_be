using RWFM.Application.Common;
using RWFM.Application.DTOs;

namespace RWFM.Application.Interfaces;

public interface IAttendanceService
{
    Task<ApiResponse<List<KioskEmployeeRosterDto>>> GetKioskRosterAsync(int storeId, DateOnly date);
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckInAsync(KioskPinCheckInDto request);
    Task<ApiResponse<AttendanceRecordDto>> KioskCheckOutAsync(KioskPinCheckOutDto request);
    Task<ApiResponse<bool>> ReportFraudAsync(int leaderEmployeeId, ReportAttendanceFraudDto request);
    Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date);
}
