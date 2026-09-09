using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RWFM.Shared.Data;
using RWFM.Shared.Security;

namespace RWFM.Shared;

public static class SharedModuleExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Database Context
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";
        var sqlServerConn = configuration.GetConnectionString("SqlServerConnection") 
            ?? "Server=localhost;Database=R_WFM_DB;Trusted_Connection=True;TrustServerCertificate=True;";
        var sqliteConn = configuration.GetConnectionString("SqliteConnection") 
            ?? "Data Source=rwfm_local.db";

        services.AddDbContext<RWFMDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(sqliteConn);
            }
            else
            {
                options.UseSqlServer(sqlServerConn);
            }
        });

        // 2. JWT Token Service
        services.AddSingleton<JwtTokenService>();

        // 3. JWT Bearer Authentication
        var secretKey = configuration["Jwt:Key"] ?? "RetailWorkforceManagementSecretKey_FPT_SWP391_2026_KeyMustBeLongEnough!";
        var issuer = configuration["Jwt:Issuer"] ?? "RWFM_API";
        var audience = configuration["Jwt:Audience"] ?? "RWFM_CLIENT";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
