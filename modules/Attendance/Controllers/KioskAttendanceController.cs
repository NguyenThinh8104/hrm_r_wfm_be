using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Attendance.DTOs;
using Modules.Attendance.Interfaces;
using Shared.Common;
using Shared.Common.Constants;
using Shared.Services;

namespace Modules.Attendance.Controllers;

/// <summary>
/// API Controller xử lý nghiệp vụ điểm danh trực tiếp trên thiết bị trạm Kiosk tại quầy.
/// </summary>
[ApiController]
[Route("api/kiosk/attendance")]
public class KioskAttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IS3StorageService _s3StorageService;

    public KioskAttendanceController(IAttendanceService attendanceService, IS3StorageService s3StorageService)
    {
        _attendanceService = attendanceService;
        _s3StorageService = s3StorageService;
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

    /// <summary>
    /// [Quy trình V3 điểm danh thuần túy] Check-in với OTP 60s & chụp/upload ảnh S3 tự động.
    /// </summary>
    [HttpPost("v3/check-in")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckInV3([FromBody] KioskCheckInV3Dto request)
    {
        var result = await _attendanceService.CheckInV3Async(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Quy trình V3 điểm danh thuần túy] Check-out với OTP 60s & chụp/upload ảnh S3 tự động.
    /// </summary>
    [HttpPost("v3/check-out")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AttendanceRecordDto>>> CheckOutV3([FromBody] KioskCheckOutV3Dto request)
    {
        var result = await _attendanceService.CheckOutV3Async(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// [Bước 1 - Kiosk] Tìm kiếm danh sách nhân viên của cửa hàng phục vụ gợi ý khi điểm danh.
    /// </summary>
    /// <param name="storeId">Mã ID cửa hàng</param>
    /// <param name="query">Từ khóa tìm kiếm theo Mã NV hoặc Tên</param>
    /// <returns>Danh sách nhân viên thỏa điều kiện</returns>
    [HttpGet("search-employees")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<KioskEmployeeSearchDto>>>> SearchEmployees(
        [FromQuery] int storeId,
        [FromQuery] string? query = null)
    {
        var result = await _attendanceService.SearchStoreEmployeesAsync(storeId, query);
        return Ok(result);
    }

    /// <summary>
    /// [API 1 - Storage S3] Tải tệp tin ảnh chân dung từ Kiosk lên S3 theo dạng multipart/form-data và trả về chuỗi photoKey.
    /// Hỗ trợ phân tách thư mục theo ca: attendance/checkin hoặc attendance/checkout.
    /// </summary>
    /// <param name="file">Tệp tin hình ảnh tải lên từ Kiosk dạng multipart/form-data</param>
    /// <param name="folder">Tên thư mục lưu trữ (mặc định là attendance/checkin)</param>
    /// <returns>ApiResponse chứa thông tin PhotoKey và PresignedUrl sinh tự động</returns>
    [HttpPost("upload-photo")]
    [Consumes("multipart/form-data")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<UploadPhotoResponseDto>>> UploadPhoto([FromForm] UploadPhotoRequestDto request)
    {
        if (request?.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse<UploadPhotoResponseDto>.Fail(AttendanceMessages.UploadPhotoFailed));
        }

        var folderName = string.IsNullOrWhiteSpace(request.Folder) ? "attendance/checkin" : request.Folder.Trim();
        var photoKey = await _s3StorageService.UploadFileAsync(request.File, folderName);
        var presignedUrl = _s3StorageService.GetPresignedUrl(photoKey);

        return Ok(ApiResponse<UploadPhotoResponseDto>.Ok(new UploadPhotoResponseDto
        {
            PhotoKey = photoKey,
            PresignedUrl = presignedUrl
        }, AttendanceMessages.UploadPhotoSuccess));
    }

    /// <summary>
    /// [API 2 - Storage S3] Nhận mã photoKey từ cơ sở dữ liệu và sinh liên kết đường dẫn tạm thời (Presigned URL) có thời hạn để xem ảnh.
    /// </summary>
    /// <param name="key">Mã định danh đối tượng S3 (PhotoKey lưu trong DB)</param>
    /// <param name="expirationMinutes">Thời gian tồn tại của đường dẫn tạm thời tính bằng phút (mặc định 30 phút)</param>
    /// <returns>ApiResponse chứa đường dẫn Presigned URL có chữ ký bảo mật</returns>
    [HttpGet("presigned-url")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<PresignedUrlResponseDto>> GetPresignedUrl(
        [FromQuery] string key,
        [FromQuery] int expirationMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponse<PresignedUrlResponseDto>.Fail(AttendanceMessages.InvalidS3Key));
        }

        var presignedUrl = _s3StorageService.GetPresignedUrl(key.Trim(), expirationMinutes);
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        return Ok(ApiResponse<PresignedUrlResponseDto>.Ok(new PresignedUrlResponseDto
        {
            PhotoKey = key.Trim(),
            PresignedUrl = presignedUrl,
            ExpiresAt = expiresAt
        }, AttendanceMessages.GetPresignedUrlSuccess));
    }
}


