using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Common;
using Shared.Data;
using Shared.Security;

namespace Shared;

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
        var mySqlConn = configuration.GetConnectionString("MySqlConnection") 
            ?? "Server=localhost;Port=3306;Database=rwfm_db;Uid=root;Pwd=;";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(sqliteConn);
            }
            else if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMySql(mySqlConn, ServerVersion.AutoDetect(mySqlConn));
            }
            else
            {
                options.UseSqlServer(sqlServerConn);
            }
        });

        // 2. Security & Time Services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IKioskContext, DefaultKioskContext>();

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

