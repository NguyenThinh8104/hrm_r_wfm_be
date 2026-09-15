using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPassResetOtpEmailAsync(string recipientEmail, string recipientName, string otpCode, int expiryMinutes)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "R-WFM Platform HR";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[R-WFM] Mã xác thực OTP đặt lại mật khẩu: {otpCode}";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 550px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                <div style='text-align: center; margin-bottom: 20px;'>
                    <h2 style='color: #1e293b; margin: 0;'>R-WFM Retail Platform</h2>
                    <p style='color: #64748b; font-size: 14px;'>Hệ thống Quản trị Nhân sự Vận hành Chuỗi Siêu thị</p>
                </div>
                <p>Xin chào <strong>{recipientName}</strong>,</p>
                <p>Bạn vừa yêu cầu đặt lại mật khẩu cho tài khoản nhân sự của mình.</p>
                <div style='background-color: #f8fafc; border: 1px dashed #cbd5e1; padding: 16px; border-radius: 6px; text-align: center; margin: 20px 0;'>
                    <span style='font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #dc2626;'>{otpCode}</span>
                </div>
                <p style='color: #475569; font-size: 13px; line-height: 1.6;'>
                    • Mã OTP này có hiệu lực chính xác trong <strong>{expiryMinutes} phút</strong>.<br>
                    • Tuyệt đối không chia sẻ mã xác thực này cho bất kỳ ai để đảm bảo an toàn tài khoản.
                </p>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Nếu bạn không yêu cầu mã này, vui lòng liên hệ ngay Quản lý Cửa hàng.</p>
            </div>";

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000 // 10s timeout chống nghẽn
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Gửi email OTP thành công tới: {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email OTP tới: {Email}", recipientEmail);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string roleName, string? branchName, string initialPassword)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "R-WFM Platform HR";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = _configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/login";

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[R-WFM] Chào mừng bạn gia nhập - Thông tin tài khoản nhân sự ({employeeCode})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.Normal;

            var assignedBranch = string.IsNullOrWhiteSpace(branchName) ? "Chi nhánh Cửa hàng" : branchName;
            var safePassword = System.Net.WebUtility.HtmlEncode(initialPassword);

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 28px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 20px; border-bottom: 2px solid #3b82f6;'>
                    <h2 style='color: #1e3a8a; margin: 0; font-size: 24px;'>R-WFM RETAIL PLATFORM</h2>
                    <p style='color: #64748b; font-size: 14px; margin-top: 5px;'>Hệ thống Quản trị Nhân sự & Lập lịch Vận hành Chuỗi</p>
                </div>
                
                <div style='margin-top: 24px;'>
                    <p style='font-size: 16px; color: #1e293b;'>Xin chào <strong>{recipientName}</strong>,</p>
                    <p style='color: #475569; line-height: 1.6;'>
                        Chào mừng bạn đã chính thức gia nhập đội ngũ nhân sự của chuỗi cửa hàng! Hồ sơ của bạn đã được khai báo và cấp tài khoản thành công trên nền tảng quản trị vận hành.
                    </p>
                </div>

                <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin: 24px 0;'>
                    <h3 style='margin-top: 0; color: #0f172a; font-size: 16px; border-bottom: 1px solid #cbd5e1; padding-bottom: 8px;'>THÔNG TIN TÀI KHOẢN ĐĂNG NHẬP</h3>
                    <table style='width: 100%; border-collapse: collapse; font-size: 14px; color: #334155;'>
                        <tr>
                            <td style='padding: 8px 0; font-weight: bold; width: 40%;'>Tên đăng nhập / Mã NV:</td>
                            <td style='padding: 8px 0; color: #2563eb; font-weight: bold;'>{employeeCode}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px 0; font-weight: bold;'>Email đăng ký:</td>
                            <td style='padding: 8px 0;'>{recipientEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px 0; font-weight: bold;'>Vị trí / Chức danh:</td>
                            <td style='padding: 8px 0; font-weight: 600; color: #059669;'>{roleName}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px 0; font-weight: bold;'>Chi nhánh làm việc:</td>
                            <td style='padding: 8px 0;'>{assignedBranch}</td>
                        </tr>
                        <tr>
                            <td style='padding: 8px 0; font-weight: bold;'>Mật khẩu khởi tạo:</td>
                            <td style='padding: 8px 0;'><code style='background-color: #f1f5f9; color: #dc2626; padding: 4px 8px; border-radius: 4px; font-size: 15px; font-weight: bold;'>{safePassword}</code></td>
                        </tr>
                    </table>
                </div>

                <div style='text-align: center; margin: 28px 0;'>
                    <a href='{loginUrl}' style='background-color: #2563eb; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Đăng Nhập Vào Hệ Thống</a>
                    <p style='margin-top: 10px; font-size: 13px; color: #64748b;'>Hoặc truy cập trực tiếp: <a href='{loginUrl}' style='color: #2563eb; text-decoration: underline;'>{loginUrl}</a></p>
                </div>

                <div style='background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; border-radius: 4px; font-size: 13px; color: #1e40af;'>
                    <strong>Lưu ý bảo mật:</strong> Để đảm bảo an toàn tài khoản, vui lòng đăng nhập và đổi sang mật khẩu cá nhân của riêng bạn trong lần đầu tiên truy cập.
                </div>

                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center; margin: 0;'>
                    Email này được gửi tự động từ Hệ thống Quản trị R-WFM. Mọi thắc mắc vui lòng liên hệ Bộ phận Nhân sự hoặc Quản lý Chi nhánh.
                </p>
            </div>";

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            await client.SendMailAsync(message);
            _logger.LogInformation(">>> [SUCCESS] Đã gửi Welcome Email thành công tới: {Email} ({EmployeeCode})", recipientEmail, employeeCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [ERROR] Lỗi khi gửi Welcome Email tới: {Email} | Chi tiết: {Message}", recipientEmail, ex.Message);
            return false;
        }
    }
}
