using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Domain.Entities;

namespace Shared.Security;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiresAt) GenerateToken(User user)
    {
        var secretKey = _configuration["Jwt:Key"] ?? "RetailWorkforceManagementSecretKey_FPT_SWP391_2026_KeyMustBeLongEnough!";
        var issuer = _configuration["Jwt:Issuer"] ?? "API";
        var audience = _configuration["Jwt:Audience"] ?? "CLIENT";
        var expirationHours = int.TryParse(_configuration["Jwt:ExpiresInHours"], out var hours) ? hours : 24;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

        var roleCode = user.Role?.RoleCode ?? "STORE_MANAGER";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, roleCode),
            new("Email", user.Email),
            new("EmployeeId", user.Id.ToString()),
            new("EmployeeCode", user.EmployeeCode),
            new("StoreId", user.HomeBranchId?.ToString() ?? "")
        };

        if (roleCode.Equals("OPERATIONS_ADMIN", StringComparison.OrdinalIgnoreCase) ||
            roleCode.Equals("OperationsAdmin", StringComparison.OrdinalIgnoreCase) ||
            roleCode.Equals("BUSINESS_OWNER", StringComparison.OrdinalIgnoreCase) ||
            roleCode.Equals("BusinessOwner", StringComparison.OrdinalIgnoreCase) ||
            roleCode.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
            roleCode.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permission", "branch.lock"));
        }

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
