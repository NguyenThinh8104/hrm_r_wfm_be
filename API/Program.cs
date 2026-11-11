using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Modules.Attendance;
using Modules.Auth;
using Modules.Dispatch;
using Modules.Shifts;
using Modules.Stores;
using Shared;
using Shared.Data;
using Shared.Middlewares;
using Shared.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Logging chuyên nghiệp (Serilog)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "rwfm-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Credentialed requests are allowed only from configured frontend origins.
var browserOrigins = builder.Configuration.GetSection("WebSession:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment())
{
    browserOrigins = browserOrigins.Concat([
        "http://localhost:5173", "http://localhost:4173", "http://localhost:3001",
        "http://127.0.0.1:5173", "http://127.0.0.1:4173", "http://127.0.0.1:3001"
    ]).Distinct().ToArray();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("TrustedBrowser", policy => policy.WithOrigins(browserOrigins)
        .AllowAnyMethod().AllowAnyHeader().AllowCredentials());
    options.AddPolicy("PublicApi", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 3. Đăng ký Shared Infrastructure (Database, JWT, Security)
builder.Services.AddSharedInfrastructure(builder.Configuration);

// 4. Đăng ký các Module Nghiệp vụ (Modular Architecture)
builder.Services
    .AddAuthModule()
    .AddStoresModule()
    .AddShiftsModule()
    .AddAttendanceModule()
    .AddDispatchModule();

// 5. Đăng ký Controllers từ các Module & Shared
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Shared.Controllers.FilesController).Assembly);

// 6. Cấu hình Swagger UI / OpenAPI (.NET 8)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "R-WFM Platform API (Modular Architecture)",
        Version = "v1",
        Description = "Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi - Kiến trúc Modular Monolith (.NET 8)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Tự động kiểm tra Database & Nạp Dữ liệu Mẫu (Nếu chưa có hoặc có cờ --seed)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        bool shouldReseed = args.Contains("--seed") || args.Contains("--reset-db");
        DbInitializer.Initialize(context, reseed: shouldReseed);

        if (shouldReseed)
        {
            Log.Information(">>> Đã Reset Database và nạp lại dữ liệu mẫu R-WFM thành công!");
        }
        else
        {
            Log.Information(">>> Kết nối cơ sở dữ liệu R-WFM thành công (Giữ nguyên dữ liệu hiện có)!");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, ">>> Có lỗi xảy ra khi khởi tạo Database / Seed Data. Vui lòng kiểm tra chuỗi kết nối!");
    }
}

// 8. Pipeline xử lý Request & Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "R-WFM API v1 (Modular)");
    c.RoutePrefix = "swagger";
});

// Keep cross-origin Bearer clients working without granting them access to browser cookies.
app.UseWhen(context => browserOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal),
    branch => branch.UseCors("TrustedBrowser"));
app.UseWhen(context => !browserOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal),
    branch => branch.UseCors("PublicApi"));

app.Use(async (context, next) =>
{
    var request = context.Request;
    var path = request.Path.Value?.TrimEnd('/');
    var isLogin = string.Equals(path, "/api/Auth/login", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(path, "/api/Auth/google-login", StringComparison.OrdinalIgnoreCase);
    var isLogout = string.Equals(path, "/api/Auth/logout", StringComparison.OrdinalIgnoreCase);
    var usesSession = request.Cookies.ContainsKey(WebSession.CookieName) &&
                      (isLogout || !request.Headers.ContainsKey("Authorization"));
    var isSafeMethod = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
                       HttpMethods.IsOptions(request.Method);

    var origin = request.Headers.Origin.ToString();
    var sameOrigin = string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
    var trustedOrigin = origin.Length == 0 || sameOrigin || request.Headers["Sec-Fetch-Site"] == "same-origin" ||
                        browserOrigins.Contains(origin, StringComparer.Ordinal);

    if (!isSafeMethod && (isLogin || usesSession) &&
        (!trustedOrigin || request.Headers[WebSession.RequestHeader] != "1"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    await next(context);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Log.Information("Ứng dụng R-WFM Backend API (Modular Architecture) đang khởi động...");
app.Run();

