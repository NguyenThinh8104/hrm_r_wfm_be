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
}

