using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RWFM.API.Middlewares;
using RWFM.Application.Interfaces;
using RWFM.Infrastructure.Data;
using RWFM.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Serilog Structured Logging (Console + File)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "rwfm-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Cấu hình Entity Framework Core (Hỗ trợ SQL Server R_WFM_DB với cơ chế tự động)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var sqlServerConn = builder.Configuration.GetConnectionString("SqlServerConnection");
var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=rwfm.db";

builder.Services.AddDbContext<RWFMDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(sqlServerConn))
    {
        options.UseSqlServer(sqlServerConn);
    }
    else
    {
        options.UseSqlite(sqliteConn);
    }
});

// 3. Đăng ký Application & Infrastructure Services
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddScoped<IHandoverService, HandoverService>();

// 4. Cấu hình JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "RetailWorkforceManagementSecretKey_FPT_SWP391_2026_KeyMustBeLongEnough!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RWFM_API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RWFM_CLIENT";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Cấu hình Controllers và Chuyển đổi Enum sang String
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 6. Cấu hình CORS cho React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 7. Cấu hình Swagger / OpenAPI kèm nút Authorize Bearer Token
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "R-WFM Platform API (Retail Workforce Management)",
        Version = "v1",
        Description = "Nền tảng Quản trị Nhân sự Vận hành Chuỗi Siêu thị Tiện lợi (Môn SWP - Kỳ Fall 2026 Đại học FPT)"
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

// 8. Tự động kiểm tra Database & Nạp Dữ liệu Mẫu (Seed Data)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<RWFMDbContext>();
        await DbInitializer.InitializeAsync(context);
        Log.Information(">>> Khởi tạo cơ sở dữ liệu và nạp dữ liệu mẫu R-WFM thành công!");
    }
    catch (Exception ex)
    {
        Log.Error(ex, ">>> Có lỗi xảy ra khi khởi tạo Database / Seed Data. Vui lòng kiểm tra chuỗi kết nối!");
    }
}

// 9. Pipeline xử lý Request & Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "R-WFM API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Ứng dụng R-WFM Backend API đang khởi động...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng R-WFM dừng đột ngột!");
}
finally
{
    Log.CloseAndFlush();
}
