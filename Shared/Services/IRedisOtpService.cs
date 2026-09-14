namespace Shared.Services;

public interface IRedisOtpService
{
    Task<string> GenerateAndSaveOtpAsync(ulong userId, string otpType, int ttlSeconds = 60);
    Task<bool> VerifyAndConsumeOtpAsync(ulong userId, string otpCode, string otpType);
}
