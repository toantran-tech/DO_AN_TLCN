# Render + Neon staging — tiến độ 14/11/2026

## Database đã chuyển

- Nguồn được người dùng chọn: `backup_toan_new.sql`, thực tế là PostgreSQL custom archive (PGDMP), tạo ngày 14/11/2026 từ `washgo_main`. Không dán file này vào SQL editor; đọc bằng pg_restore 18.
- Restore lên Neon `neondb` tại Singapore đã thành công, bằng một transaction, không dùng clean/create database và không sửa DB local. Trước restore đã xác nhận Neon trống và lưu backup DB đích ở Temp ngoài repo.
- Đối chiếu số dòng của đủ 45 bảng có dữ liệu dump khớp nguồn: 10 users, 8 orders, 1 rating, 60 migration records. Migration mới nhất `20261114061302_AddPublicCustomerReviewApproval`.
- Tài khoản Admin người dùng yêu cầu đã có, active và giữ nguyên password hash từ backup; không cần bootstrap Admin khác hoặc reset mật khẩu.
- Import database không bao gồm bytes ảnh/video/giấy tờ trong volume uploads/private-documents; các file đó cần chuyển riêng sang storage bền vững. Không coi đường dẫn/metadata file trong DB là file đã được chuyển.

File `.env.staging.local` chứa thông tin kết nối riêng và đã được Git ignore. Không commit, chụp ảnh hoặc dán password/connection string vào chat. Mật khẩu đã chia sẻ cần thay lại trước khi public, rồi cập nhật Render và file local riêng.

## Seed khi khởi động

Host vẫn áp EF migrations ở mọi môi trường. `Seeding:Enabled` mặc định true chỉ khi Development; Staging/Production mặc định false. Có thể đặt rõ `Seeding__Enabled=false` trên Render để dùng DB đã import, không chạy bộ demo seed. Không xóa account hoặc dữ liệu đã restore. Thay đổi này phải được commit/CI/merge vào nhánh Render deploy trước khi có hiệu lực; theo yêu cầu người dùng, không tự build Docker/chạy bộ test local.

## Form Render

| Mục | Giá trị |
|---|---|
| Name | washgo-be-staging |
| Language | Docker |
| Branch | Nhánh đã merge bản cập nhật; staging nên theo develop, main cũng dùng được nếu đã có commit mới |
| Region | Singapore |
| Root Directory | washgo-be |
| Dockerfile Path nếu hiển thị | ./Dockerfile |
| Instance | Free, xác nhận $0/month |

Environment Variables:

```text
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_HTTP_PORTS=8080
PORT=8080
Seeding__Enabled=false
Jwt__SecretKey=<Generate khóa riêng, không dùng khóa đã hiện trên ảnh>
ConnectionStrings__Default=Host=<Neon host trực tiếp>;Port=5432;Database=neondb;Username=neondb_owner;Password=<mật khẩu riêng>;SSL Mode=Require;
App__CorsOrigins=<origin FE được phép, phân tách bằng dấu phẩy>
```

Nhập trực tiếp secrets trên Render; Render không đọc file `.env` local hay dùng docker-compose để tự chạy DB. Giữ Neon làm DB staging riêng, không trỏ vào PostgreSQL local.

## Chưa hoàn tất

- Commit/CI/merge seed guard, deploy Render và xác minh migration/seed logs cùng đăng nhập Admin trên URL public. Chưa coi DB restore là web service đã deploy.
- Render Free chặn SMTP 25/465/587, nên cấu hình Gmail SMTP hiện tại không đủ chạy OTP; cần adapter mail HTTPS và cấu hình sender được xác minh.
- Render Free không có persistent disk; cần external storage cho ảnh/tài liệu, giữ private documents riêng tư. Chưa thay backend lưu file hiện tại.
- Cấu hình Google client ID/origin FE, Gemini key và callback/IPN VNPay/MoMo dùng URL staging thật; không giữ URL localhost/tunnel cũ hoặc tự đổi sang Mock trên app public.
- Kiểm tra reverse proxy/HTTPS, health check, RAM và SignalR reconnect. Free service có thể ngủ, worker không có cam kết chạy liên tục; CI chưa phải CD/staging tự động.
