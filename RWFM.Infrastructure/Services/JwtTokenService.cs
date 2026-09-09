using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RWFM.Domain.Entities;

namespace RWFM.Infrastructure.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiresAt) GenerateToken(User user, Employee? employee = null)
    {
        var secretKey = _configuration["Jwt:Key"] ?? "RetailWorkforceManagementSecretKey_FPT_SWP391_2026_KeyMustBeLongEnough!";
        var issuer = _configuration["Jwt:Issuer"] ?? "RWFM_API";
        var audience = _configuration["Jwt:Audience"] ?? "RWFM_CLIENT";
        var expirationHours = int.TryParse(_configuration["Jwt:ExpiresInHours"], out var hours) ? hours : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, employee?.FullName ?? user.Username),
            new(ClaimTypes.Role, user.Role),
            new("Username", user.Username),
            new("EmployeeId", employee?.EmployeeId.ToString() ?? ""),
            new("EmployeeCode", employee?.EmployeeCode ?? ""),
            new("StoreId", employee?.PrimaryStoreId.ToString() ?? "")
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(tokenDescriptor);

        return (tokenString, expiresAt);
    }
}
