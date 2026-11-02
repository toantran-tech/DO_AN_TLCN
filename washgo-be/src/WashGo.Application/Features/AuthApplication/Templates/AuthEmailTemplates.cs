namespace WashGo.Application.Features.AuthApplication.Templates
{
  /// <summary>
  /// Chứa các template HTML email được chuẩn hóa theo bảng màu thương hiệu WashGo.
  /// </summary>
  public static class AuthEmailTemplates
  {
    /// <summary>
    /// Tạo giao diện HTML cho email gửi mã OTP (Đăng ký, Quên mật khẩu, Bảo mật).
    /// </summary>
    public static string RenderOtpEmail(string title, string description, string code)
    {
      return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{title}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #fffcfa; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #25324a; -webkit-font-smoothing: antialiased;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #fffcfa; padding: 40px 16px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width: 520px; background-color: #ffffff; border-radius: 14px; border: 1px solid #f0eee9; box-shadow: 0 4px 24px rgba(37, 50, 74, 0.06); overflow: hidden;"">
          <!-- Header Banner WashGo (Màu chính: #ff7a45) -->
          <tr>
            <td style=""background-color: #ff7a45; padding: 26px 32px; text-align: center;"">
              <span style=""font-size: 26px; font-weight: 800; color: #ffffff; letter-spacing: 1.5px; display: inline-block;"">WASH<span style=""color: #ffe8dc;"">GO</span></span>
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
                    <div style=""background-color: #FFF7ED; border: 2px dashed #ff7a45; border-radius: 12px; padding: 18px 28px; display: inline-block; min-width: 240px; text-align: center;"">
                      <span style=""font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1.5px; color: #ff7a45; display: block; margin-bottom: 6px;"">MÃ XÁC THỰC CỦA BẠN</span>
                      <span style=""font-family: 'Courier New', Courier, monospace, sans-serif; font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #ff7a45; line-height: 1.2;"">{code}</span>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Khung lưu ý cảnh báo (Màu cảnh báo WashGo) -->
              <div style=""background-color: #FFF7ED; border-left: 4px solid #FED7AA; border-radius: 8px; padding: 12px 16px; margin-bottom: 24px;"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                  <tr>
                    <td style=""font-size: 13px; line-height: 1.5; color: #9a4c00;"">
                      Mã này có hiệu lực trong vòng <strong>5 phút</strong>.<br>
                      <strong>Tuyệt đối không chia sẻ mã này</strong> cho bất kỳ ai, kể cả nhân viên WashGo.
                    </td>
                  </tr>
                </table>
              </div>

              <p style=""margin: 0; font-size: 12px; line-height: 1.6; color: #8e99a8; text-align: center;"">
                Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này để bảo vệ tài khoản.
              </p>
            </td>
          </tr>

          <!-- Chân trang -->
          <tr>
            <td style=""background-color: #faf9f6; padding: 18px 32px; text-align: center; border-top: 1px solid #f0eee9;"">
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
