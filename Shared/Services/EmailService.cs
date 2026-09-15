using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

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
            message.Subject = $"[RWFM Support] Mã xác thực OTP đặt lại mật khẩu: {otpCode}";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 550px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                <div style='text-align: center; margin-bottom: 20px;'>
                    <h2 style='color: #1e293b; margin: 0;'>RWFM Retail Platform</h2>
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
}

