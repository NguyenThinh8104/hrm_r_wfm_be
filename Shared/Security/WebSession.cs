using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Shared.Security;

public sealed record WebSessionIdentity(ulong UserId, string PasswordStamp)
{
    public static WebSessionIdentity FromUser(User user) =>
        new(user.Id, Digest(user.PasswordHash));

    internal static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class WebSession(IDistributedCache cache, IConfiguration configuration, IHostEnvironment environment)
{
    public const string Scheme = "WebSession";
    public const string CookieName = "rwfm.session";
    public const string RequestHeader = "X-RWFM-Request";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task StartAsync(HttpContext context, WebSessionIdentity identity)
    {
        await RemoveAsync(context);
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = DateTimeOffset.UtcNow.Add(Lifetime);
        await cache.SetStringAsync(CacheKey(sessionId), JsonSerializer.Serialize(identity),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expires }, context.RequestAborted);

        var options = CookieOptionsFor(context);
        options.Expires = expires;
        context.Response.Cookies.Append(CookieName, sessionId, options);
    }

    public async Task<WebSessionIdentity?> ReadAsync(HttpContext context)
    {
        if (context.Request.Cookies[CookieName] is not { Length: 64 } sessionId)
            return null;

        var value = await cache.GetStringAsync(CacheKey(sessionId), context.RequestAborted);
        return value == null ? null : JsonSerializer.Deserialize<WebSessionIdentity>(value);
    }

    public async Task EndAsync(HttpContext context)
    {
        await RemoveAsync(context);
        context.Response.Cookies.Delete(CookieName, CookieOptionsFor(context));
    }

    private async Task RemoveAsync(HttpContext context)
    {
        if (context.Request.Cookies[CookieName] is { Length: 64 } sessionId)
            await cache.RemoveAsync(CacheKey(sessionId), context.RequestAborted);
    }

    private CookieOptions CookieOptionsFor(HttpContext context)
    {
        var crossSite = configuration.GetValue<bool>("WebSession:CrossSite");
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = crossSite || !environment.IsDevelopment() || context.Request.IsHttps,
            SameSite = crossSite ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        };
    }

    private static string CacheKey(string sessionId) => $"web-session:{WebSessionIdentity.Digest(sessionId)}";
}
