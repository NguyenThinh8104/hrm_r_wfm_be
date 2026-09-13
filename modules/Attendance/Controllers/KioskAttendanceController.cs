using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;

namespace Modules.Attendance.Controllers;

/// <summary>
/// API Controller xử lý nghiệp vụ điểm danh trực tiếp trên thiết bị trạm Kiosk tại quầy.
/// </summary>
[ApiController]
[Route("api/kiosk/attendance")]
public class KioskAttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public KioskAttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// [Bước 1 - Kiosk] Xác thực mã PIN nhân viên và trả về thông tin ca làm việc hôm nay.
    /// </summary>
    /// <param name="request">DTO chứa mã nhân viên (EmployeeCode), mã PIN và ID cửa hàng (StoreId)</param>
    /// <returns>Thông tin xác thực nhân viên, chức vụ, ca trực hôm nay và lịch sử check-in/check-out</returns>
    [HttpPost("validate-pin")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ValidatePinResponseDto>>> ValidatePin([FromBody] ValidatePinRequestDto request)
    {
        var result = await _attendanceService.ValidatePinAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Kiosk Roster] Lấy danh sách ca làm việc xếp lịch hôm nay tại cửa hàng.
    /// </summary>
    /// <param name="storeId">Mã ID chi nhánh cửa hàng</param>
    /// <param name="date">Ngày cần lấy danh sách (định dạng YYYY-MM-DD, mặc định là hôm nay)</param>
    /// <returns>Danh sách nhân viên có ca trực kèm thời gian và trạng thái điểm danh</returns>
    [HttpGet("roster")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<KioskEmployeeRosterDto>>>> GetKioskRoster(
        [FromQuery] int storeId,
        [FromQuery] string? date)
    {
        var targetDate = string.IsNullOrEmpty(date) || !DateOnly.TryParse(date, out var parsedDate)
            ? DateOnly.FromDateTime(DateTime.Now)
            : parsedDate;

        var result = await _attendanceService.GetKioskRosterAsync(storeId, targetDate);
        return Ok(result);
    }

    /// <summary>
    /// [Bước 2 - Kiosk] Đánh dấu thời điểm bắt đầu ca trực (Check-in) qua mã PIN.
    /// </summary>
    /// <param name="request">DTO chứa EmployeeId, StoreId, PinCode, KioskId và số tiền lẻ đầu ca (nếu là Thu ngân)</param>
    /// <returns>Bản ghi điểm danh ghi nhận thời gian vào ca và trạng thái Present/Late</returns>
    [HttpPost("check-in")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckIn([FromBody] KioskPinCheckInDto request)
    {
        var result = await _attendanceService.KioskCheckInAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Bước 2 - Kiosk] Đánh dấu thời điểm kết thúc ca trực (Check-out) qua mã PIN.
    /// </summary>
    /// <param name="request">DTO chứa EmployeeId, StoreId, PinCode và KioskId</param>
    /// <returns>Bản ghi điểm danh hoàn tất cập nhật thời gian ra ca</returns>
    [HttpPost("check-out")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckOut([FromBody] KioskPinCheckOutDto request)
    {
        var result = await _attendanceService.KioskCheckOutAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Quy trình mới] Điểm danh Check-in đầu ca tại quầy Kiosk (Sử dụng IKioskContext & TimeProvider).
    /// </summary>
    /// <param name="request">DTO chứa UserId và Mã PIN 6 chữ số</param>
    /// <returns>ApiResponse chứa thông tin bản ghi Check-in thành công</returns>
    [HttpPost("v2/check-in")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceLogDto>>> CheckInV2([FromBody] CheckInRequestDto request)
    {
        var result = await _attendanceService.CheckInAsync(request.UserId, request.Pin);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Quy trình mới] Điểm danh Check-out kết thúc ca theo 3 bước nghiệp vụ (Sử dụng IKioskContext & TimeProvider).
    /// </summary>
    /// <param name="request">DTO chứa UserId và Mã PIN 6 chữ số</param>
    /// <returns>ApiResponse chứa kết quả Check-out và tổng số phút làm việc</returns>
    [HttpPost("v2/check-out")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceCheckOutResultDto>>> CheckOutV2([FromBody] CheckOutRequestDto request)
    {
        var result = await _attendanceService.CheckOutAsync(request.UserId, request.Pin);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

