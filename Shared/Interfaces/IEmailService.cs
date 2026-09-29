namespace Shared.Interfaces;

public interface IEmailService
{
    Task<bool> SendPassResetOtpEmailAsync(string recipientEmail, string recipientName, string otpCode, int expiryMinutes);
    Task<bool> SendWelcomeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string roleName, string? branchName, string initialPassword);
    Task<bool> SendShiftChangeNotificationEmailAsync(string recipientEmail, string recipientName, string changeType, string shiftDetails, string effectiveDate, string note, string? status = null);
    Task<bool> SendAccountStatusChangeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string status, string reason);
    Task<bool> SendPasswordResetNotificationEmailAsync(string recipientEmail, string recipientName, string employeeCode, string newPassword, string reason);
}
