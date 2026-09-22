namespace Shared.Services;

public interface IRedisOtpService
{
    Task<string> GenerateAndSaveOtpAsync(ulong userId, string otpType, int ttlSeconds = 60);
    Task<bool> VerifyAndConsumeOtpAsync(ulong userId, string otpCode, string otpType);
    Task<bool> VerifyOtpAsync(ulong userId, string otpCode, bool consume = false);

    /// <summary>
    /// Sinh OTP mới với Redis key = "attendance:otp:{otpCode}", value = "{userId}:{otpType}", TTL configurable.
    /// </summary>
    Task<string> GenerateAttendanceOtpAsync(ulong userId, string otpType, int ttlSeconds = 60);

    /// <summary>
    /// Lookup OTP code → trả về (userId, otpType) nếu hợp lệ, consume (xóa key) sau khi tìm thấy.
    /// </summary>
    Task<(ulong UserId, string OtpType)?> VerifyAndConsumeAttendanceOtpAsync(string otpCode);
}
