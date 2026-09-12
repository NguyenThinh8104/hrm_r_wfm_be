using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Modules.Attendance;
using Modules.Auth;
using Modules.Dispatch;
using Modules.Handovers;
using Modules.Shifts;
using Modules.Stores;
using Shared;
using Shared.Data;
using Shared.Middlewares;
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

// 2. Cấu hình CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 3. Đăng ký Shared Infrastructure (Database, JWT, Security)
builder.Services.AddSharedInfrastructure(builder.Configuration);

// 4. Đăng ký các Module Nghiệp vụ (Modular Architecture)
builder.Services
    .AddAuthModule()
    .AddStoresModule()
    .AddShiftsModule()
    .AddAttendanceModule()
    .AddDispatchModule()
    .AddHandoversModule();

// 5. Đăng ký Controllers từ các Module
builder.Services.AddControllers();

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

// 7. Tự động kiểm tra Database & Nạp Dữ liệu Mẫu (Seed Data)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await DbInitializer.InitializeAsync(context);
        Log.Information(">>> Khởi tạo cơ sở dữ liệu và nạp dữ liệu mẫu R-WFM thành công!");
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

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Log.Information("Ứng dụng R-WFM Backend API (Modular Architecture) đang khởi động...");
app.Run();

