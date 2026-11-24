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
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[RWFM Enterprise] Mã xác thực OTP đặt lại mật khẩu: {otpCode}";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 550px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                <div style='text-align: center; margin-bottom: 20px;'>
                    <h2 style='color: #1e293b; margin: 0;'>RWFM Enterprise</h2>
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
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Gửi email OTP thành công tới: {Email}", recipientEmail);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, ">>> [SMTP ERROR] Lỗi SMTP ({StatusCode}) khi gửi email OTP tới {Email}: {Message}", ex.StatusCode, recipientEmail, ex.Message);
            return false;
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
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = _configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/login";

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[RWFM Enterprise] Chào mừng bạn gia nhập - Thông tin tài khoản nhân sự ({employeeCode})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.Normal;

            var assignedBranch = string.IsNullOrWhiteSpace(branchName) ? "Chi nhánh Cửa hàng" : branchName;
            var safePassword = System.Net.WebUtility.HtmlEncode(initialPassword);

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 28px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 20px; border-bottom: 2px solid #3b82f6;'>
                    <h2 style='color: #1e3a8a; margin: 0; font-size: 24px;'>RWFM Enterprise</h2>
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
                    Email này được gửi tự động từ Hệ thống Quản trị RWFM Enterprise. Mọi thắc mắc vui lòng liên hệ Bộ phận Nhân sự hoặc Quản lý Chi nhánh.
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

    public async Task<bool> SendBulkImportWelcomeEmailAsync(IReadOnlyCollection<string> recipientEmails)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = new Uri(_configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/login");
            var resetUrl = WebUtility.HtmlEncode(new Uri(loginUrl, "/forgot-password").AbsoluteUri);

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "[RWFM Enterprise] Tài khoản nhân viên đã được tạo",
                IsBodyHtml = true,
                Body = $"""
                    <h2>Chào mừng bạn đến với RWFM Enterprise</h2>
                    <p>Tài khoản nhân viên của bạn đã được tạo.</p>
                    <p>Để đặt mật khẩu lần đầu, hãy mở trang <a href="{resetUrl}">Khôi phục mật khẩu</a>, nhập email cá nhân đã đăng ký và xác thực bằng mã OTP được gửi tới hộp thư của bạn.</p>
                    <p>Sau khi đặt mật khẩu, bạn có thể <a href="{WebUtility.HtmlEncode(loginUrl.AbsoluteUri)}">đăng nhập</a> bằng email đó.</p>
                    <p>Nếu cần hỗ trợ, vui lòng liên hệ bộ phận Nhân sự.</p>
                    """
            };

            foreach (var email in recipientEmails)
            {
                message.Bcc.Add(new MailAddress(email));
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
            _logger.LogInformation("Đã gửi email chào mừng sau khi import cho {RecipientCount} nhân viên", message.Bcc.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email chào mừng sau khi import {RecipientCount} nhân viên", recipientEmails.Count);
            return false;
        }
    }

    /// <summary>
    /// Hàng đợi gửi email cá nhân hóa (chứa đầy đủ Họ tên, Mã NV, Chức danh, Chi nhánh, Mật khẩu khởi tạo) cho nhân viên sau khi import.
    /// Chạy ngầm trong Background Task (fire-and-forget), áp dụng cơ chế điều tiết (Throttling Delay 5000ms / 5s) để không bị Google SMTP khóa tài khoản.
    /// </summary>
    public void QueueBulkImportPersonalizedWelcomeEmails(IReadOnlyCollection<BulkImportWelcomeEmailItem> items)
    {
        if (items == null || items.Count == 0) return;

        // Tách ra chạy nền trên luồng riêng, không làm block response của API Import
        _ = Task.Run(async () =>
        {
            _logger.LogInformation(">>> [QUEUE BULK EMAIL] Bắt đầu hàng đợi gửi Welcome Email cá nhân hóa cho {Count} nhân sự mới...", items.Count);

            int index = 0;
            int successCount = 0;
            int failCount = 0;

            foreach (var item in items)
            {
                index++;
                try
                {
                    _logger.LogInformation(">>> [BULK EMAIL] [{Index}/{Total}] Đang gửi email cho: {FullName} ({EmployeeCode}) - {Email}...",
                        index, items.Count, item.FullName, item.EmployeeCode, item.Email);

                    var sent = await SendWelcomeEmailAsync(
                        item.Email,
                        item.FullName,
                        item.EmployeeCode,
                        item.RoleName,
                        item.BranchName,
                        item.PlainPassword
                    );

                    if (sent)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex, ">>> [BULK EMAIL ERROR] Lỗi không mong muốn khi gửi email cho {Email} ({EmployeeCode})", item.Email, item.EmployeeCode);
                }

                // CƠ CHẾ ĐIỀU TIẾT TỐC ĐỘ (RATE LIMITING THROTTLING):
                // Nghỉ an toàn 5 giây (5000ms) giữa mỗi email để tránh kích hoạt bộ lọc spam burst limit của Gmail SMTP
                if (index < items.Count)
                {
                    await Task.Delay(5000);
                }
            }

            _logger.LogInformation(">>> [QUEUE BULK EMAIL COMPLETED] Đã hoàn tất gửi {Success}/{Total} email chào mừng (Thất bại: {FailCount})",
                successCount, items.Count, failCount);
        });
    }

    public async Task<bool> SendShiftChangeNotificationEmailAsync(string recipientEmail, string recipientName, string changeType, string shiftDetails, string effectiveDate, string note, string? status = null)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = _configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/employee/my-calendar";

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            
            var displayStatus = status ?? "ĐÃ CẬP NHẬT";
            message.Subject = $"[RWFM Enterprise] Thông báo thay đổi lịch làm việc: {changeType} ({displayStatus})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            var statusColor = displayStatus.Contains("DUYỆT") || displayStatus.Contains("APPROVED") || displayStatus.Contains("THÀNH CÔNG")
                ? "#10B981"
                : displayStatus.Contains("TỪ CHỐI") || displayStatus.Contains("REJECTED")
                    ? "#EF4444"
                    : "#3B82F6";

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 28px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 20px; border-bottom: 2px solid {statusColor};'>
                    <h2 style='color: #0f172a; margin: 0; font-size: 22px;'>RWFM Enterprise</h2>
                    <p style='color: #64748b; font-size: 13px; margin-top: 5px;'>Hệ thống Thông báo Lịch Trình Nhân sự Tự động</p>
                </div>
                
                <div style='margin-top: 24px;'>
                    <p style='font-size: 15px; color: #1e293b;'>Xin chào <strong>{recipientName}</strong>,</p>
                    <p style='color: #475569; line-height: 1.6;'>
                        Hệ thống ghi nhận có cập nhật mới về lịch làm việc / yêu cầu đổi lịch của bạn:
                    </p>
                </div>

                <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 20px; margin: 20px 0;'>
                    <div style='display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #cbd5e1; padding-bottom: 10px; margin-bottom: 12px;'>
                        <span style='font-weight: bold; color: #0f172a; font-size: 15px;'>{changeType}</span>
                        <span style='background-color: {statusColor}15; color: {statusColor}; border: 1px solid {statusColor}; padding: 4px 10px; border-radius: 20px; font-weight: bold; font-size: 12px;'>
                            {displayStatus}
                        </span>
                    </div>

                    <table style='width: 100%; border-collapse: collapse; font-size: 13.5px; color: #334155;'>
                        <tr>
                            <td style='padding: 6px 0; font-weight: 600; width: 35%;'>Ngày áp dụng:</td>
                            <td style='padding: 6px 0; color: #0f172a; font-weight: bold;'>{effectiveDate}</td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: 600;'>Chi tiết ca làm việc:</td>
                            <td style='padding: 6px 0; color: #10B981; font-weight: 600;'>{shiftDetails}</td>
                        </tr>
                        {(string.IsNullOrWhiteSpace(note) ? "" : $@"
                        <tr>
                            <td style='padding: 6px 0; font-weight: 600;'>Ghi chú / Lý do:</td>
                            <td style='padding: 6px 0; color: #64748b; font-style: italic;'>{note}</td>
                        </tr>")}
                    </table>
                </div>

                <div style='text-align: center; margin: 26px 0;'>
                    <a href='{loginUrl}' style='background-color: #10B981; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Xem Lịch Cá Nhân Trên Hệ Thống</a>
                </div>

                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center; margin: 0;'>
                    Email này được gửi tự động từ Hệ thống Quản trị RWFM Enterprise. Mọi thắc mắc vui lòng liên hệ Quản lý Cửa hàng để được hỗ trợ.
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
            _logger.LogInformation(">>> [SUCCESS] Đã gửi Email thông báo thay đổi lịch thành công tới: {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [ERROR] Lỗi khi gửi Email thông báo đổi lịch tới: {Email} | Chi tiết: {Message}", recipientEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendAccountStatusChangeEmailAsync(string recipientEmail, string recipientName, string employeeCode, string status, string reason)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            bool isLocked = status == "INACTIVE";
            message.Subject = isLocked 
                ? $"[RWFM Enterprise] Thông báo khóa tài khoản nhân sự ({employeeCode})"
                : $"[RWFM Enterprise] Thông báo kích hoạt lại tài khoản nhân sự ({employeeCode})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            var statusBadge = isLocked 
                ? "<span style='background-color: #fee2e2; color: #dc2626; padding: 4px 10px; border-radius: 4px; font-weight: bold;'>ĐÃ KHÓA (TẠM NGỪNG)</span>"
                : "<span style='background-color: #dcfce7; color: #16a34a; padding: 4px 10px; border-radius: 4px; font-weight: bold;'>ĐANG HOẠT ĐỘNG</span>";

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 580px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 16px; border-bottom: 2px solid {(isLocked ? "#dc2626" : "#16a34a")};'>
                    <h2 style='color: #1e3a8a; margin: 0; font-size: 22px;'>RWFM Enterprise</h2>
                    <p style='color: #64748b; font-size: 13px; margin-top: 4px;'>Hệ thống Quản trị Nhân sự & Lập lịch Vận hành Chuỗi</p>
                </div>
                
                <div style='margin-top: 20px;'>
                    <p style='font-size: 15px; color: #1e293b;'>Kính gửi <strong>{recipientName}</strong> (Mã NV: <strong>{employeeCode}</strong>),</p>
                    <p style='color: #475569; line-height: 1.6;'>
                        Hệ thống xin thông báo về việc thay đổi trạng thái hoạt động tài khoản nhân sự của bạn:
                    </p>
                </div>

                <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 18px 0;'>
                    <table style='width: 100%; border-collapse: collapse; font-size: 13.5px; color: #334155;'>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold; width: 35%;'>Trạng thái tài khoản:</td>
                            <td style='padding: 6px 0;'>{statusBadge}</td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold;'>Lý do thực hiện:</td>
                            <td style='padding: 6px 0; color: #b91c1c; font-weight: 600;'>{System.Net.WebUtility.HtmlEncode(reason)}</td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold;'>Thời gian cập nhật:</td>
                            <td style='padding: 6px 0;'>{DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (Giờ VN)</td>
                        </tr>
                    </table>
                </div>

                <p style='font-size: 13px; color: #64748b; line-height: 1.5;'>
                    {(isLocked ? "Tài khoản của bạn hiện không thể đăng nhập hoặc điểm danh. Nếu có bất kỳ thắc mắc hoặc cần khiếu nại, vui lòng liên hệ ngay Quản lý Chi nhánh hoặc Bộ phận Nhân sự." : "Tài khoản của bạn đã được mở khóa bình thường. Bạn có thể đăng nhập vào ứng dụng để xem lịch ca làm việc.")}
                </p>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Email được gửi tự động từ Hệ thống Quản trị Nhân sự RWFM Enterprise. Vui lòng không phản hồi thư này.</p>
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
            _logger.LogInformation("Gửi email thay đổi trạng thái thành công tới {Email}", recipientEmail);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, ">>> [SMTP ERROR] Lỗi SMTP ({StatusCode}) khi gửi email đổi trạng thái tới {Email}: {Message}", ex.StatusCode, recipientEmail, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email thay đổi trạng thái tới {Email}", recipientEmail);
            return false;
        }
    }

    public async Task<bool> SendPasswordResetNotificationEmailAsync(string recipientEmail, string recipientName, string employeeCode, string newPassword, string reason)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = _configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/login";

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[RWFM Enterprise] Thông báo cấp lại mật khẩu tài khoản ({employeeCode})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            var safePassword = System.Net.WebUtility.HtmlEncode(newPassword);

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 580px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 16px; border-bottom: 2px solid #3b82f6;'>
                    <h2 style='color: #1e3a8a; margin: 0; font-size: 22px;'>RWFM Enterprise</h2>
                    <p style='color: #64748b; font-size: 13px; margin-top: 4px;'>Hệ thống Quản trị Nhân sự & Lập lịch Vận hành Chuỗi</p>
                </div>
                
                <div style='margin-top: 20px;'>
                    <p style='font-size: 15px; color: #1e293b;'>Kính gửi <strong>{recipientName}</strong> (Mã NV: <strong>{employeeCode}</strong>),</p>
                    <p style='color: #475569; line-height: 1.6;'>
                        Mật khẩu tài khoản nhân sự của bạn vừa được Quản trị viên cấp lại. Chi tiết thông tin như sau:
                    </p>
                </div>

                <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 18px 0;'>
                    <table style='width: 100%; border-collapse: collapse; font-size: 13.5px; color: #334155;'>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold; width: 35%;'>Tài khoản đăng nhập:</td>
                            <td style='padding: 6px 0; font-weight: bold; color: #2563eb;'>{employeeCode}</td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold;'>Mật khẩu mới:</td>
                            <td style='padding: 6px 0;'><code style='background-color: #e2e8f0; color: #dc2626; padding: 3px 8px; border-radius: 4px; font-size: 15px; font-weight: bold;'>{safePassword}</code></td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold;'>Lý do cấp lại:</td>
                            <td style='padding: 6px 0; color: #334155;'>{System.Net.WebUtility.HtmlEncode(reason)}</td>
                        </tr>
                        <tr>
                            <td style='padding: 6px 0; font-weight: bold;'>Thời gian cấp lại:</td>
                            <td style='padding: 6px 0;'>{DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (Giờ VN)</td>
                        </tr>
                    </table>
                </div>

                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{loginUrl}' style='background-color: #2563eb; color: #ffffff; padding: 10px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Đăng Nhập Lại</a>
                </div>

                <p style='font-size: 12.5px; color: #dc2626; line-height: 1.5;'>
                    ⚠️ <strong>Lưu ý bảo mật:</strong> Để đảm bảo an toàn, vui lòng đăng nhập và thực hiện đổi mật khẩu cá nhân ngay sau khi nhận được thông báo này.
                </p>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Email được gửi tự động từ Hệ thống Quản trị Nhân sự RWFM Enterprise.</p>
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
            _logger.LogInformation("Gửi email cấp lại mật khẩu thành công tới {Email}", recipientEmail);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, ">>> [SMTP ERROR] Lỗi SMTP ({StatusCode}) khi gửi email cấp lại mật khẩu tới {Email}: {Message}", ex.StatusCode, recipientEmail, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email cấp lại mật khẩu tới {Email}", recipientEmail);
            return false;
        }
    }

    public async Task<bool> SendProfileUpdateNotificationEmailAsync(
        string recipientEmail, 
        string recipientName, 
        string employeeCode, 
        IReadOnlyDictionary<string, (string OldVal, string NewVal)> changedFields, 
        string reason)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RWFM Enterprise";
            var senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var loginUrl = _configuration["ClientApp:LoginUrl"] ?? "http://localhost:5173/login";

            using var message = new MailMessage();
            message.From = new MailAddress(senderEmail, senderName);
            message.To.Add(new MailAddress(recipientEmail, recipientName));
            message.Subject = $"[RWFM Enterprise] Thông báo điều chỉnh thông tin hồ sơ nhân sự ({employeeCode})";
            message.IsBodyHtml = true;
            message.Priority = MailPriority.High;

            var rowsHtml = new System.Text.StringBuilder();
            foreach (var kvp in changedFields)
            {
                var fieldName = System.Net.WebUtility.HtmlEncode(kvp.Key);
                var oldVal = System.Net.WebUtility.HtmlEncode(kvp.Value.OldVal ?? "—");
                var newVal = System.Net.WebUtility.HtmlEncode(kvp.Value.NewVal ?? "—");
                rowsHtml.Append($@"
                    <tr style='border-bottom: 1px solid #e2e8f0;'>
                        <td style='padding: 8px 12px; font-weight: 600; color: #1e293b;'>{fieldName}</td>
                        <td style='padding: 8px 12px; color: #64748b; text-decoration: line-through;'>{oldVal}</td>
                        <td style='padding: 8px 12px; color: #0d9488; font-weight: bold;'>{newVal}</td>
                    </tr>");
            }

            message.Body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff;'>
                <div style='text-align: center; padding-bottom: 16px; border-bottom: 2px solid #2563eb;'>
                    <h2 style='color: #1e3a8a; margin: 0; font-size: 22px;'>RWFM Enterprise</h2>
                    <p style='color: #64748b; font-size: 13px; margin-top: 4px;'>Hệ thống Quản trị Nhân sự & Lập lịch Vận hành Chuỗi</p>
                </div>
                
                <div style='margin-top: 20px;'>
                    <p style='font-size: 15px; color: #1e293b;'>Kính gửi <strong>{recipientName}</strong> (Mã NV: <strong>{employeeCode}</strong>),</p>
                    <p style='color: #475569; line-height: 1.6;'>
                        Hệ thống xin thông báo: Thông tin hồ sơ nhân sự của bạn vừa được Quản trị viên cập nhật thành công trên hệ thống. Chi tiết thay đổi như sau:
                    </p>
                </div>

                <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 18px 0;'>
                    <table style='width: 100%; border-collapse: collapse; font-size: 13.5px;'>
                        <thead>
                            <tr style='background-color: #f1f5f9; text-align: left;'>
                                <th style='padding: 8px 12px; color: #475569;'>Trường Thông Tin</th>
                                <th style='padding: 8px 12px; color: #475569;'>Giá Trị Cũ</th>
                                <th style='padding: 8px 12px; color: #475569;'>Giá Trị Mới</th>
                            </tr>
                        </thead>
                        <tbody>
                            {rowsHtml}
                        </tbody>
                    </table>
                </div>

                <div style='background-color: #eff6ff; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 18px 0; border-radius: 4px;'>
                    <p style='margin: 0; font-size: 13.5px; color: #1e40af;'>
                        <strong>Lý do điều chỉnh:</strong> {System.Net.WebUtility.HtmlEncode(reason)}
                    </p>
                    <p style='margin: 4px 0 0 0; font-size: 12px; color: #64748b;'>
                        Thời gian ghi nhận: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (Giờ VN)
                    </p>
                </div>

                <div style='text-align: center; margin: 20px 0;'>
                    <a href='{loginUrl}' style='background-color: #2563eb; color: #ffffff; padding: 10px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Kiểm Tra Hồ Sơ</a>
                </div>

                <p style='font-size: 12.5px; color: #64748b; line-height: 1.5;'>
                    Nếu có bất kỳ thắc mắc nào liên quan đến thông tin được điều chỉnh, bạn vui lòng liên hệ ngay với Quản trị viên hoặc Bộ phận Nhân sự.
                </p>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center;'>Email được gửi tự động từ Hệ thống Quản trị Nhân sự RWFM Enterprise.</p>
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
            _logger.LogInformation("Gửi email thông báo cập nhật hồ sơ thành công tới {Email}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email thông báo cập nhật hồ sơ tới {Email}", recipientEmail);
            return false;
        }
    }
}
