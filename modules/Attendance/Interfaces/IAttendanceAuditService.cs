using Modules.Attendance.DTOs;
using Shared.Common;

namespace Modules.Attendance.Interfaces;

/// <summary>
/// Interface dịch vụ giám sát quân số cửa hàng và nhật ký điểm danh (Audit & Live Monitoring).
/// </summary>
public interface IAttendanceAuditService
{
    /// <summary>
    /// Lấy quân số theo dõi trực tiếp thời gian thực tại cửa hàng kèm Presigned Temporary URL ảnh S3.
    /// </summary>
    Task<ApiResponse<List<LiveRosterDto>>> GetLiveRosterAsync(ulong storeId, DateOnly? date = null);

    /// <summary>
    /// Lấy lịch sử danh sách các bản ghi điểm danh trong ngày của chi nhánh cửa hàng.
    /// </summary>
    Task<ApiResponse<List<AttendanceRecordDto>>> GetAttendanceHistoryAsync(int storeId, DateOnly date);
}
