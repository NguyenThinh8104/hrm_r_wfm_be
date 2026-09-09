namespace RWFM.Application.DTOs;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class KioskLoginRequestDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserSummaryDto User { get; set; } = null!;
}

public class UserSummaryDto
{
    public int UserId { get; set; }
    public int? EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string? PositionName { get; set; }
}
