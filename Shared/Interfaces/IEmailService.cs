namespace Shared.Interfaces;

public class BulkImportWelcomeEmailItem
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PlainPassword { get; set; } = "Password@123";
}

public interface IEmailService
{
    Task<bool> SendPassResetOtpEmailAsync(string recipientEmail, string recipientName, string otpCode, int expiryMinutes);
    Task<bool> SendWelcomeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string roleName, string? branchName, string initialPassword);
    Task<bool> SendBulkImportWelcomeEmailAsync(IReadOnlyCollection<string> recipientEmails);
    void QueueBulkImportPersonalizedWelcomeEmails(IReadOnlyCollection<BulkImportWelcomeEmailItem> items);
    Task<bool> SendShiftChangeNotificationEmailAsync(string recipientEmail, string recipientName, string changeType, string shiftDetails, string effectiveDate, string note, string? status = null);
    Task<bool> SendAccountStatusChangeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string status, string reason);
    Task<bool> SendPasswordResetNotificationEmailAsync(string recipientEmail, string recipientName, string employeeCode, string newPassword, string reason);
}
