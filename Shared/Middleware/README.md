# 🔀 Middleware — Request/Response Pipeline

## Mục đích
Chứa các **ASP.NET Core Middleware** custom, xử lý logic xuyên suốt request pipeline (cross-cutting concerns).

## Quy tắc
- Middleware class có constructor nhận `RequestDelegate next`.
- Method chính là `InvokeAsync(HttpContext context)`.
- Đăng ký middleware trong `Program.cs` bằng `app.UseMiddleware<T>()`.
- Thứ tự middleware trong pipeline **RẤT QUAN TRỌNG** — đăng ký theo thứ tự đúng.
- Middleware nên single-responsibility: mỗi middleware xử lý 1 concern.

## Ví dụ file trong folder này
```
Middleware/
├── ExceptionHandlingMiddleware.cs    ← Catch exceptions → HTTP error response
└── RequestLoggingMiddleware.cs       ← Log request/response info
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Shared.Middleware;
```

## Template
```csharp
// ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (UnauthorizedException ex)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Đã xảy ra lỗi hệ thống" });
        }
    }
}
```

## Đăng ký trong Program.cs
```csharp
// Thứ tự middleware pipeline quan trọng:
app.UseMiddleware<ExceptionHandlingMiddleware>();  // Đầu tiên — catch tất cả exceptions
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```
