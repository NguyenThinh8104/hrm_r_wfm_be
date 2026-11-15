namespace Modules.Auth.DTOs;

public class GoogleLoginDTOs
{
    public string IdToken { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
