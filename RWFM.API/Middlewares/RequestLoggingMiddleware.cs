using System.Diagnostics;

namespace RWFM.API.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestMethod = context.Request.Method;
        var requestPath = context.Request.Path;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                requestMethod, requestPath, statusCode, stopwatch.ElapsedMilliseconds);
        }
    }
}
