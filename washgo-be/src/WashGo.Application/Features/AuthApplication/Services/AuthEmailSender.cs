using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services
{
    /// <summary>
    /// Sends OTP email through SMTP.
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
            var subject = purpose switch
            {
                OtpPurpose.Register => "Mã xác thực đăng ký tài khoản WashGo",
                OtpPurpose.ForgotPassword => "Mã xác thực đặt lại mật khẩu WashGo",
                _ => "Mã xác thực WashGo"
            };
            return SendAsync(email, subject,
                $"Mã xác thực của bạn là: {code}. Mã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.");
        }

        public Task<bool> SendAccountCredentialsAsync(string email, string fullName, string temporaryPassword) =>
            SendAsync(email, "Tài khoản Shipper WashGo của bạn",
                $"Xin chào {fullName},\n\nTài khoản Shipper WashGo của bạn đã được tạo.\n" +
                $"Email đăng nhập: {email}\nMật khẩu tạm thời: {temporaryPassword}\n\n" +
                "Vui lòng đăng nhập và đổi mật khẩu bằng chức năng Quên mật khẩu. Không chia sẻ thông tin này cho bất kỳ ai.");

        private async Task<bool> SendAsync(string email, string subject, string body)
        {

            var host = _configuration["Email:Smtp:Host"];
            var senderAddress = _configuration["Email:Smtp:SenderAddress"];
            var senderName = _configuration["Email:Smtp:SenderName"] ?? "WashGo";
            var username = _configuration["Email:Smtp:Username"];
            var password = _configuration["Email:Smtp:Password"];
            var port = int.TryParse(_configuration["Email:Smtp:Port"], out var configuredPort)
                ? configuredPort
                : 587;
            var enableSsl = !bool.TryParse(_configuration["Email:Smtp:EnableSsl"], out var configuredSsl) || configuredSsl;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
            {
                _logger.LogError("SMTP is not configured. Set Email:Smtp:Host and Email:Smtp:SenderAddress.");
                return false;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(senderAddress, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
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
