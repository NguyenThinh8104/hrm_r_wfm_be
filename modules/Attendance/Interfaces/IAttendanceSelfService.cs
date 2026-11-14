using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Interface dịch vụ tự phục vụ điểm danh của nhân viên (Xem lịch tuần, lịch sử và thống kê chuyên cần theo tháng).
/// </summary>
public interface IAttendanceSelfService
{
    /// <summary>
    /// Lấy lịch làm việc cá nhân theo tuần (Calendar View) kèm trạng thái điểm danh từng ca.
    /// </summary>
    Task<ApiResponse<MyWeeklyScheduleDto>> GetMyWeeklyScheduleAsync(ulong userId, DateOnly weekStart);

    /// <summary>
    /// Lấy lịch sử chấm công cá nhân theo tháng kèm thống kê tỷ lệ đi làm, số ca vắng và tổng giờ làm thực tế.
    /// </summary>
    Task<ApiResponse<MyAttendanceHistoryDto>> GetMyAttendanceHistoryAsync(ulong userId, int month, int year);
}
