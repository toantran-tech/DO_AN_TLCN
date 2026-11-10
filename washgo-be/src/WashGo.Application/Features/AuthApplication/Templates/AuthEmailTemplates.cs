using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace WashGo.Application.Features.AuthApplication.Templates
{
  /// <summary>
  /// Chứa các template HTML email được chuẩn hóa theo bảng màu thương hiệu WashGo.
  /// </summary>
  public static class AuthEmailTemplates
  {
    public const string LogoContentId = "washgo-logo";

    public static AlternateView CreateOtpView(string html)
    {
      var stream = typeof(AuthEmailTemplates).Assembly.GetManifestResourceStream("WashGo.EmailAssets.logo.png")
        ?? throw new InvalidOperationException("Embedded WashGo email logo is missing.");
      var view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
      view.LinkedResources.Add(new LinkedResource(stream, "image/png")
      {
        ContentId = LogoContentId,
        TransferEncoding = TransferEncoding.Base64
      });
      return view;
    }

    /// <summary>
    /// Tạo giao diện HTML cho email gửi mã OTP (Đăng ký, Quên mật khẩu, Bảo mật).
    /// </summary>
    public static string RenderOtpEmail(string title, string description, string code)
      => RenderEmail(title, description, "MÃ XÁC THỰC CỦA BẠN", WebUtility.HtmlEncode(code),
        "Mã này có hiệu lực trong vòng <strong>5 phút</strong>.<br><strong>Tuyệt đối không chia sẻ mã này</strong> cho bất kỳ ai, kể cả nhân viên WashGo.",
        "Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này để bảo vệ tài khoản.", true);

    public static string RenderCredentialsEmail(string role, string fullName, string email, string temporaryPassword)
      => RenderEmail($"Tài khoản {role} đã được tạo", $"Xin chào {fullName}, tài khoản {role} WashGo của bạn đã sẵn sàng.",
        "THÔNG TIN ĐĂNG NHẬP",
        $"Email: <strong>{WebUtility.HtmlEncode(email)}</strong><br>Mật khẩu tạm thời: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong>",
        "Vui lòng đăng nhập và đổi mật khẩu. Bạn có thể dùng chức năng Quên mật khẩu để đặt mật khẩu mới.<br><strong>Không chia sẻ thông tin đăng nhập cho bất kỳ ai.</strong>",
        "Nếu bạn không nhận ra tài khoản này, vui lòng liên hệ quản trị viên WashGo.");

    public static string RenderGoogleLinkedEmail(string fullName, string googleEmail)
      => RenderEmail("Liên kết Google thành công", $"Xin chào {fullName}, tài khoản WashGo của bạn vừa được liên kết với Google.",
        "TÀI KHOẢN GOOGLE", WebUtility.HtmlEncode(googleEmail),
        "Lần sau bạn có thể chọn <strong>Đăng nhập bằng Google</strong> tại trang đăng nhập WashGo.",
        "Nếu bạn không thực hiện thao tác này, hãy liên hệ quản trị viên WashGo ngay để được hỗ trợ bảo vệ tài khoản.");

    private static string RenderEmail(string title, string description, string label, string content, string notice, string footer, bool isOtp = false)
    {
      title = WebUtility.HtmlEncode(title);
      description = WebUtility.HtmlEncode(description);
      label = WebUtility.HtmlEncode(label);
      return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f8fbff; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #25324a; -webkit-font-smoothing: antialiased;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #f8fbff; padding: 40px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width: 520px; background-color: #ffffff; border-radius: 14px; border: 1px solid #e5e7eb; box-shadow: 0 4px 24px rgba(37, 50, 74, 0.06); overflow: hidden;"">
          <!-- Header Banner WashGo (Màu chính: #2563eb) -->
          <tr>
            <td style=""background-color: #2563eb; padding: 28px 32px 24px 32px; text-align: center;"">
              <!-- Logo WashGo -->
              <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"" style=""margin: 0 auto 14px auto;"">
                <tr>
                  <td align=""center"" valign=""middle"" style=""background-color: #ffffff; width: 68px; height: 68px; border-radius: 22px; text-align: center; box-shadow: 0 4px 12px rgba(0,0,0,0.12);"">
                    <img src=""cid:{LogoContentId}"" alt=""WashGo"" width=""54"" height=""54"" border=""0"" style=""display: block; margin: 0 auto; width: 54px; height: 54px;"">
                  </td>
                </tr>
              </table>
              <span style=""font-size: 26px; font-weight: 800; color: #ffffff; letter-spacing: 1.5px; display: inline-block;"">WASH<span style=""color: #dbeafe;"">GO</span></span>
              <div style=""font-size: 13px; color: rgba(255, 255, 255, 0.92); margin-top: 4px; font-weight: 500;"">Hệ thống Tủ giặt thông minh</div>
            </td>
          </tr>

          <!-- Nội dung chính -->
          <tr>
            <td style=""padding: 36px 32px 28px 32px;"">
              <h2 style=""margin: 0 0 12px 0; font-size: 20px; font-weight: 700; color: #25324a; text-align: center;"">
                {title}
              </h2>
              <p style=""margin: 0 0 24px 0; font-size: 14px; line-height: 1.6; color: #5a6679; text-align: center;"">
                {description}
              </p>

              <!-- Khung hiển thị mã OTP 6 số -->
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin: 0 0 24px 0;"">
                <tr>
                  <td align=""center"">
                    <div style=""background-color: #eff6ff; border: 2px dashed #2563eb; border-radius: 12px; padding: 18px 20px; display: inline-block; max-width: 100%; text-align: center; word-break: break-word;"">
                      <span style=""font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; color: #2563eb; display: block; margin-bottom: 6px;"">{label}</span>
                      <span style=""font-family: 'Courier New', Courier, monospace, sans-serif; font-size: {(isOtp ? "34px" : "14px")}; font-weight: {(isOtp ? "800" : "400")}; letter-spacing: {(isOtp ? "8px" : "normal")}; color: #2563eb; line-height: 1.6;"">{content}</span>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Khung lưu ý cảnh báo (Màu cảnh báo WashGo) -->
              <div style=""background-color: #FFF7ED; border-left: 4px solid #FED7AA; border-radius: 8px; padding: 12px 16px; margin-bottom: 24px;"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                  <tr>
                    <td style=""font-size: 13px; line-height: 1.5; color: #9a4c00;"">
                      {notice}
                    </td>
                  </tr>
                </table>
              </div>

              <p style=""margin: 0; font-size: 12px; line-height: 1.6; color: #8e99a8; text-align: center;"">
                {footer}
              </p>
            </td>
          </tr>

          <!-- Chân trang -->
          <tr>
            <td style=""background-color: #f8fbff; padding: 18px 32px; text-align: center; border-top: 1px solid #e5e7eb;"">
              <p style=""margin: 0; font-size: 12px; color: #8e99a8;"">
                © 2026 WashGo Inc. Dịch vụ giặt ủi thông minh qua Smart Locker.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
  }
}
