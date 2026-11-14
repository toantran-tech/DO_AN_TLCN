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

## Adapter staging đã bổ sung ngày 14/11/2026

Đã viết adapter email **Brevo HTTPS** và storage **Supabase REST**, giữ template email hiện hành. Mặc định local vẫn là `Email:Provider=Smtp`, `Storage:Provider=Local`; khi chọn provider cloud thì không fallback về SMTP/ổ đĩa nếu thiếu key hoặc upstream lỗi. Không thêm bảng/migration.

- Email: API `POST https://api.brevo.com/v3/smtp/email`; chỉ báo Sent khi nhà cung cấp trả messageId. Sent là đã nhận yêu cầu, không đảm bảo inbox. Timeout/lỗi mạng/5xx trả Unknown, không tự retry gửi credentials. Không ghi body thư/key vào log adapter mới.
- Logo: SMTP giữ PNG nhúng CID; Brevo dùng cùng PNG ở `/branding/email-logo.png`, base URL lấy từ `App:PublicBaseUrl` hoặc `RENDER_EXTERNAL_URL`. Không dùng inline SVG.
- File: Supabase dùng hai bucket **private** khác nhau; backend kiểm tra bucket private trước truy cập, dùng service_role ở server. URL FE vẫn `/uploads/{owner}/{file}`; không phát URL bucket/key bí mật. Route media giữ quyền truy cập công khai như URL uploads cũ; **không dùng route này cho giấy tờ riêng tư**. Giấy tờ chỉ tải qua `/files/documents/{id}/download` với quyền Admin/đúng Merchant. Ảnh review công khai vẫn qua alias publicId và kiểm tra phát hành/role như trước.
- Upload ảnh/video kiểm tra signature, giới hạn size trước khi đưa lên cloud; dùng file tạm tự xóa thay vì nạp video 50 MB vào RAM. Download media proxy theo stream có giới hạn. Kiểm tra ảnh sở hữu, liên kết tài liệu và cleanup cũng đi qua cùng storage adapter.
- `GET /health/live`: API sống. `GET /health/ready`: kết nối được DB, trả 503 nếu không được; không lộ lỗi/connection string. Readiness không kiểm tra toàn bộ schema, email, storage hay payment. Cấu hình Render Health Check Path `/health/ready`.
- `GET /payment/return`: thông báo quay lại ứng dụng; không đọc query để đánh dấu trả tiền. Chỉ IPN hợp lệ cập nhật giao dịch. Khi FE có trang kết quả, có thể đặt ReturnUrl/RedirectUrl sang trang FE đó và FE đọc lại trạng thái từ API có JWT.

## Điền secrets sau khi có tài khoản

Mẫu đầy đủ: [render-staging.env.example](render-staging.env.example). Đây là **tên biến .NET trên Render**, không phải biến viết hoa dùng để nội suy docker-compose local. Không nhập nguyên placeholder lên Render. Chỉ bật provider cloud khi đã có key/bucket/sender hợp lệ; không coi code adapter là đã cấu hình dịch vụ ngoài.

1. Brevo: tạo tài khoản, tạo **API key** cho transactional email (không phải SMTP key); xác minh sender/domain. Điền `Email__Provider=Brevo`, `Email__Brevo__ApiKey`, `Email__SenderAddress`, `Email__SenderName`. Điền `App__PublicBaseUrl` bằng origin HTTPS thật của service. Không gửi key lên chat/Git. Kiểm tra quota tài khoản trước UAT.
2. Supabase: tạo project, vào Storage tạo `washgo-uploads` và `washgo-documents`, **tắt Public ở cả hai**. Bucket media chấp nhận JPG/PNG/WEBP/MP4/WEBM/MOV; bucket tài liệu chấp nhận PDF/DOC/DOCX. Cấu hình giới hạn bucket phù hợp 50 MiB media/10 MiB tài liệu nếu gói cho phép. Trong Project Settings lấy project URL và **legacy service_role key**; điền nhóm `Storage__Supabase__*`. Không dùng anon/publishable key, không đưa service_role sang FE, không tạo policy anonymous đọc/list/write. Database nghiệp vụ vẫn ở Neon, không chuyển sang Supabase Postgres.
3. **File cũ cần chuyển riêng:** backup Neon chỉ có metadata. Sao chép thư mục uploads/private-documents từ máy/volume local; upload bytes vào đúng bucket và giữ nguyên key: media `<owner-guid-N>/<file-guid-N>.<ext>`, tài liệu `<document-guid-N>.<ext>`. Không đổi URL/ID trong DB, không xóa nguồn. Đối chiếu số file, size/hash và thử quyền tải trước khi kết luận dữ liệu đầy đủ. Chưa có key nên chưa thực hiện chuyển file.
4. Google: Render `Google__ClientId`; Google Console cho phép origin FE thật (và localhost nếu cần). Gemini: `Chatbot__Gemini__ApiKey`; giữ model đang được dự án cấu hình, không tự mua/bật billing.
5. Thanh toán: `Payment__Provider=Live`, dùng credentials **sandbox chính thức**, URL cổng sandbox theo file mẫu. VNPay đăng ký IPN `https://<host-thật>/payment/vnpay-ipn` ở portal; MoMo điền `Payment__Momo__IpnUrl=https://<host-thật>/payment/momo-ipn`. Return/RedirectUrl trỏ staging/FE thật, bỏ tunnel cũ. Không dùng browser redirect hoặc callback mock để kết luận thanh toán thành công.
6. FE/SignalR: `App__CorsOrigins` là danh sách origin chính xác, không có path/dấu slash cuối. FE dùng HTTPS API base URL và HTTPS hub URL hiện hành; sau khi service ngủ phải reconnect và đọc lại notification qua REST. Không mở CORS `*` cho bearer-token flows.

Tài liệu API nhà cung cấp: [Brevo transactional email](https://developers.brevo.com/docs/send-a-transactional-email), [Supabase upload](https://supabase.com/docs/guides/storage/uploads/standard-uploads), [Supabase access control](https://supabase.com/docs/guides/storage/security/access-control), [Render Free limitations](https://render.com/docs/free).

## Kiểm chứng và việc còn lại

- Đã bổ sung `tests/smoke/EmailLogo/HostedProviderChecks.cs` và gọi từ `EmailLogo/Program.cs`. Workflow Backend CI hiện chạy project EmailLogo trong bước **Mapping, document and email regression suites**, vì vậy PR sẽ chạy thêm hợp đồng Brevo/Supabase với HTTP giả, không gửi email/ghi cloud thật và không cần secrets. Các test HTTP nghiệp vụ local vẫn dùng Local storage.
- **Theo yêu cầu người dùng: chưa chạy test/build local hoặc Docker cho đợt này.** CI trên PR phải xanh rồi mới merge/deploy; không ghi nhận PASS khi chưa chạy.
- Chưa có tài khoản/key Brevo/Supabase nên **chưa gửi email thật, chưa upload/download cloud thật, chưa chuyển bytes file cũ**. Sau khi điền key: test OTP tới email của nhóm, test giấy tờ đúng/sai Merchant, media sau redeploy, kiểm tra file nguồn/hash.
- Render: giữ `Seeding__Enabled=false`, kết nối Neon dạng Npgsql `Host=...;...`, kiểm tra migration logs, đăng nhập Admin, health readiness. Chưa thay đổi secrets hay cài đặt trên dashboard Render trong đợt sửa này.
- Cần kiểm tra Google/Gemini, VNPay/MoMo IPN sandbox, FE CORS và SignalR qua URL public. Reverse proxy/HTTPS và RAM cần đo khi deploy. Render Free có thể ngủ, không có cam kết worker liên tục và không giữ file local qua deploy; không dùng dữ liệu nhạy cảm thật để UAT.
- CI hiện kiểm tra PR; cấu hình auto-deploy sau CI, số lần deploy, rollback và UAT cần hoàn thiện riêng, không coi adapter này là đã đạt toàn bộ tiêu chí CI/CD rubric.

## Chuyển file cũ — cập nhật thực tế 14/11/2026

Người dùng đã cấu hình Brevo và Supabase trên Render, xác nhận gửi OTP, logo email và upload ảnh Swagger 201. Chưa test đủ video/tài liệu/quyền tải, Google/Gemini và thanh toán thật qua sandbox; không đánh dấu hoàn thành toàn bộ staging.

Đã sao chép **36 file / 44.419 byte** từ hai volume local của `washgo_backend` ra thư mục Temp riêng, tạo inventory SHA-256; nguồn không thay đổi. Đây là file tìm thấy trong container hiện tại, **chưa đối chiếu tất cả tham chiếu file trong Neon** nên chưa khẳng định đủ bytes cho toàn DB. Không tự lấy các thư mục test build khác làm dữ liệu nghiệp vụ.

Việc chuyển dữ liệu một lần đã hoàn tất và giữ nguồn local nguyên vẹn; report size/SHA-256 ở Temp ngoài repo. Theo yêu cầu người dùng, không giữ script chuyển file Python hoặc test Python trong source và CI. API runtime vẫn chỉ dùng C#/.NET, không cần cài Python để chạy chức năng storage.

### Kết quả chuyển file đã xác minh

- Đã dùng key từ file local được Git ignore để thực hiện chuyển 36 file / 44.419 byte vào bucket `washgo-uploads` của project Supabase staging đã cấu hình. Đã GET lại từng file, xác minh size và SHA-256 khớp nguồn; 36 trạng thái `copied-and-verified`, tiến trình exit 0.
- `private-documents` trong container nguồn trống; bucket `washgo-documents` đã kiểm tra private nhưng chưa có bằng chứng upload/download tài liệu thực tế hoặc kiểm tra quyền Merchant trên staging.
- Không xóa file nguồn, không sửa metadata Neon hay local DB. Report và bản export nằm ngoài repo trong Temp; giữ bản nguồn Docker/backup để có thể phục hồi. Báo cáo chứa object key/hash, không commit.
- Trong lúc chạy phát hiện Supabase trả HTTP 400 với `code=NoSuchKey`, `statusCode=404`, `error=not_found` khi object chưa tồn tại. ObjectStorage C# đã nhận diện chính xác mẫu này; không nới lỏng mọi lỗi 400. Kiểm tra tồn tại chuyển sang GET info/authenticated để đọc được structured error (HEAD không có body).
- Provider contract tests .NET đã cập nhật cho trường hợp này, vẫn để CI PR thực thi theo yêu cầu. Chưa chạy local regression/build Docker, chưa deploy bản sửa NoSuchKey. Chuyển dữ liệu cloud đã hoàn thành độc lập với việc deploy bản sửa code.
- Chưa đối chiếu toàn bộ URL metadata trong Neon với danh sách file nguồn để xác định các file đã thiếu từ trước. Không tuyên bố dữ liệu file của toàn bộ backup DB đã đầy đủ chỉ từ 36 file tìm thấy.
