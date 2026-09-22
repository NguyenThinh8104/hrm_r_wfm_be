using Microsoft.Extensions.Caching.Distributed;

namespace Shared.Services;

public class RedisOtpService : IRedisOtpService
{
    private readonly IDistributedCache _cache;
    private static readonly Random _random = new Random();

    public RedisOtpService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> GenerateAndSaveOtpAsync(ulong userId, string otpType, int ttlSeconds = 60)
    {
        var otpCode = _random.Next(100000, 999999).ToString();
        var cacheKey = GetCacheKey(userId, otpType);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
        };

        await _cache.SetStringAsync(cacheKey, otpCode, options);
        return otpCode;
    }

    public async Task<bool> VerifyAndConsumeOtpAsync(ulong userId, string otpCode, string otpType)
    {
        if (string.IsNullOrWhiteSpace(otpCode)) return false;

        var cacheKey = GetCacheKey(userId, otpType);
        var storedOtp = await _cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(storedOtp))
        {
            return false;
        }

        if (storedOtp.Trim() == otpCode.Trim())
        {
            // Consumed - remove from Redis immediately so it cannot be reused
            await _cache.RemoveAsync(cacheKey);
            return true;
        }

        return false;
    }

    public async Task<bool> VerifyOtpAsync(ulong userId, string otpCode, bool consume = false)
    {
        if (string.IsNullOrWhiteSpace(otpCode)) return false;
        var code = otpCode.Trim();

        var types = new[] { "CHECK_IN", "CHECK_OUT" };
        foreach (var type in types)
        {
            var key = GetCacheKey(userId, type);
            var storedOtp = await _cache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(storedOtp) && storedOtp.Trim() == code)
            {
                if (consume)
                {
                    await _cache.RemoveAsync(key);
                }
                return true;
            }
        }

        return false;
    }

    private static string GetCacheKey(ulong userId, string otpType)
    {
        return $"attendance:otp:{userId}:{otpType.ToUpper()}";
    }

    /// <summary>
    /// Sinh OTP mới, Redis key = "attendance:otp:{otpCode}", value = "{userId}:{otpType}", TTL configurable.
    /// </summary>
    public async Task<string> GenerateAttendanceOtpAsync(ulong userId, string otpType, int ttlSeconds = 60)
    {
        string otpCode;
        string cacheKey;

        // Sinh OTP 6 số, đảm bảo key unique trong Redis
        do
        {
            otpCode = _random.Next(100000, 999999).ToString();
            cacheKey = GetAttendanceCacheKey(otpCode);
        }
        while (await _cache.GetStringAsync(cacheKey) != null);

        var value = $"{userId}:{otpType.ToUpper()}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
        };

        await _cache.SetStringAsync(cacheKey, value, options);
        return otpCode;
    }

    /// <summary>
    /// Lookup OTP code → trả về (userId, otpType) nếu hợp lệ, consume (xóa key) sau khi tìm thấy.
    /// </summary>
    public async Task<(ulong UserId, string OtpType)?> VerifyAndConsumeAttendanceOtpAsync(string otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode)) return null;

        var cacheKey = GetAttendanceCacheKey(otpCode.Trim());
        var storedValue = await _cache.GetStringAsync(cacheKey);

        if (string.IsNullOrEmpty(storedValue)) return null;

        // Parse value format: "{userId}:{otpType}"
        var parts = storedValue.Split(':');
        if (parts.Length != 2 || !ulong.TryParse(parts[0], out var userId))
        {
            return null;
        }

        // Consume — xóa key ngay lập tức để không thể tái sử dụng
        await _cache.RemoveAsync(cacheKey);
        return (userId, parts[1]);
    }

    private static string GetAttendanceCacheKey(string otpCode)
    {
        return $"attendance:otp:{otpCode}";
    }
}

