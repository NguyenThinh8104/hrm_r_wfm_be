namespace Shared.Services;

public interface IEmailService
{
    Task<bool> SendPassResetOtpEmailAsync(string recipientEmail, string recipientName, string otpCode, int expiryMinutes);
}

