# UC 1.1.3 — Quên mật khẩu (Backend, 03/11/2026)

Swagger: http://localhost:5000/swagger/index.html. Không cần đăng nhập.

## 1. Màn hình nhập email — POST /auth/send-otp

Email phải đã đăng ký. Purpose `2` là ForgotPassword.

```json
{"email":"toan008888@gmail.com","purpose":2}
```

Thành công: chuyển sang màn hình nhập 6 số OTP, giữ email trong state của luồng.
OTP có hạn 5 phút. Gửi lại phải chờ 60 giây; mã/phiên cũ bị vô hiệu hóa khi gửi lại thành công.

## 2. Nút Tiếp tục tại màn hình OTP — POST /auth/verify-otp

Thay mã ví dụ bằng OTP thật vừa nhận qua email.

```json
{"email":"toan008888@gmail.com","code":"123456","purpose":2}
```

Khi `succeeded = true`, `data` chứa `verified`, `verificationToken`, `expiresAt`.
FE giữ email và token trong state, chuyển sang màn hình nhập mật khẩu mới/xác nhận mật khẩu.
Không gửi lại OTP ở bước đổi mật khẩu. Không dùng accessToken hay token đăng ký thay token này.
Token đặt lại mật khẩu hết hạn sau 10 phút; OTP đã xác minh không thể xác minh lại.
Nếu làm mất token (ví dụ reload trang khi chỉ lưu trong bộ nhớ), bắt đầu lại từ bước gửi OTP.

## 3. Nút Hoàn tất — POST /auth/reset-password

Copy nguyên `data.verificationToken` từ bước 2:

```json
{
  "email":"toan008888@gmail.com",
  "verificationToken":"TOKEN_NHAN_TU_BUOC_2",
  "newPassword":"Toan@123456",
  "confirmNewPassword":"Toan@123456"
}
```

Mật khẩu 8–128 ký tự, có chữ hoa, chữ thường và chữ số; hai ô phải khớp.
Thành công: token bị tiêu thụ, các phiên đăng nhập cũ bị thu hồi, chuyển về màn hình đăng nhập.
Đăng nhập bằng mật khẩu mới thành công; mật khẩu cũ bị từ chối.

## Ca kiểm tra bổ sung

| Ca | Kết quả mong đợi |
| --- | --- |
| OTP sai/hết hạn/đã xác minh | 400; không chuyển màn hình nhập mật khẩu |
| Nhập OTP sai 5 lần | Không cho xác minh tiếp bằng OTP đó |
| Thiếu token, token sai/hết hạn | 400; mật khẩu giữ nguyên |
| Token thuộc email khác hoặc purpose Register | 400 |
| Mật khẩu yếu/hai ô không khớp | 400; vẫn sửa và gửi lại bằng token còn hạn |
| Dùng lại token sau reset thành công | 400 |
| Hai request reset đồng thời dùng cùng token | Chính xác một request thành công |
| JWT phiên cũ sau reset | 401 ở /auth/me |

Smoke test tại thư mục `washgo-be`:

```powershell
./tests/smoke/forgot-password.ps1
```

Script dùng tài khoản riêng `@example.test`, seed OTP dạng hash trong DB local để kiểm thử API,
tự dọn dữ liệu sau test; không đổi mật khẩu tài khoản thật và không gửi email thật.
Việc nhận thư SMTP thực tế cần kiểm tra bằng bước 1–2 trên Swagger với hộp thư của người test.
Test tôn trọng rate limit Auth 10 request/phút; có thể chờ khi nhận 429.

Thay đổi hợp đồng API: `ResetPasswordCommand.code` được thay bằng `verificationToken`.
Client đang dùng luồng cũ phải thêm bước verify-otp trước reset-password.
