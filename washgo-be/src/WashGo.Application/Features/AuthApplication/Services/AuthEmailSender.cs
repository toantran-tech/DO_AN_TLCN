using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using WashGo.Application.Features.AuthApplication.Templates;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services
{
    /// <summary>
    /// Sends OTP and credential emails through SMTP.
    /// </summary>
    public class AuthEmailSender : IAuthEmailSender, ITransientDependency
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthEmailSender> _logger;

        public AuthEmailSender(
            IConfiguration configuration,
            ILogger<AuthEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task<bool> SendOtpAsync(string email, string code, OtpPurpose purpose)
        {
            var (subject, title, description) = purpose switch
            {
                OtpPurpose.Register => (
                    "Mã xác thực đăng ký tài khoản WashGo",
                    "Xác thực đăng ký tài khoản",
                    "Cảm ơn bạn đã lựa chọn WashGo. Vui lòng sử dụng mã OTP dưới đây để hoàn tất quá trình đăng ký tài khoản."
                ),
                OtpPurpose.ForgotPassword => (
                    "Mã xác thực đặt lại mật khẩu WashGo",
                    "Yêu cầu đặt lại mật khẩu",
                    "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản WashGo của bạn. Dưới đây là mã OTP xác thực của bạn."
                ),
                _ => (
                    "Mã xác thực WashGo",
                    "Mã xác thực bảo mật",
                    "Vui lòng sử dụng mã OTP dưới đây để tiếp tục thao tác trên hệ thống WashGo."
                )
            };

            var htmlBody = AuthEmailTemplates.RenderOtpEmail(title, description, code);
            return SendAsync(email, subject, htmlBody, isHtml: true);
        }

        public Task<bool> SendAccountCredentialsAsync(string email, string fullName, string temporaryPassword) =>
            SendAsync(email, "Tài khoản Shipper WashGo của bạn",
                $"Xin chào {fullName},\n\nTài khoản Shipper WashGo của bạn đã được tạo.\n" +
                $"Email đăng nhập: {email}\nMật khẩu tạm thời: {temporaryPassword}\n\n" +
                "Vui lòng đăng nhập và đổi mật khẩu bằng chức năng Quên mật khẩu. Không chia sẻ thông tin này cho bất kỳ ai.");

        private async Task<bool> SendAsync(string email, string subject, string body, bool isHtml = false)
        {
            var host = _configuration["Email:Smtp:Host"] ?? _configuration["SMTP_HOST"] ?? "smtp.gmail.com";
            var senderAddress = _configuration["Email:Smtp:SenderAddress"] ?? _configuration["SMTP_SENDER_ADDRESS"];
            var senderName = _configuration["Email:Smtp:SenderName"] ?? _configuration["SMTP_SENDER_NAME"] ?? "Hệ thống WashGo";
            var username = _configuration["Email:Smtp:Username"] ?? _configuration["SMTP_USERNAME"];
            var password = _configuration["Email:Smtp:Password"] ?? _configuration["SMTP_PASSWORD"];
            var portStr = _configuration["Email:Smtp:Port"] ?? _configuration["SMTP_PORT"];
            var port = int.TryParse(portStr, out var configuredPort)
                ? configuredPort
                : 587;
            var sslStr = _configuration["Email:Smtp:EnableSsl"] ?? _configuration["SMTP_ENABLE_SSL"];
            var enableSsl = !bool.TryParse(sslStr, out var configuredSsl) || configuredSsl;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
            {
                _logger.LogError("SMTP is not configured. Set Email:Smtp:Host and Email:Smtp:SenderAddress (or SMTP_HOST / SMTP_SENDER_ADDRESS).");
                return false;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(senderAddress, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };
                message.To.Add(email);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl
                };

                if (!string.IsNullOrWhiteSpace(username))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(username, password);
                }

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email '{Subject}' to {Email}", subject, email);
                return false;
            }
        }
    }
}
