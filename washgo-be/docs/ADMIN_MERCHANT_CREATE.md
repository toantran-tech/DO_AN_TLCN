# Tạo Merchant và theo dõi email

POST `/merchant/` chỉ cho Admin. Không sửa hợp đồng version của PUT hồ sơ/status hoặc duyệt yêu cầu trong `ADMIN_MERCHANT_REVIEW.md`.

## Gửi yêu cầu

```json
{
  "clientRequestId": "<GUID mới cho lần tạo này>",
  "email": "merchant@example.test",
  "businessName": "Tên cửa hàng",
  "address": "Địa chỉ",
  "city": "Tỉnh/thành phố",
  "district": "",
  "latitude": 10.77,
  "longitude": 106.69,
  "phone": "0912345678"
}
```

Thay placeholder bằng GUID thật. Các trường tùy chọn ảnh, vùng, MST, hoa hồng và hợp đồng giữ theo Swagger hiện tại. Email chuyển thành chữ thường/trim, MST trim. Tên cửa hàng giữ tối đa 200 ký tự; tên hiển thị tài khoản User lấy tối đa 100 ký tự đầu theo giới hạn riêng của tài khoản.

Tạo thành công trả 201 dù email không gửi được. Trùng email/MST trả 409 với `meta.code=EmailAlreadyUsed|TaxCodeAlreadyUsed`, kể cả vi phạm unique xảy ra giữa kiểm tra và ghi DB. Các lỗi khác không được giả thành lỗi trùng.

## Retry và tra cứu

- Nên sinh clientRequestId trước lần POST đầu và giữ cùng toàn bộ payload khi gửi lại sau timeout. Cùng key/actor/payload trả lại Merchant đã tạo; không tạo tài khoản, audit hoặc gửi mật khẩu lần nữa.
- Cùng key nhưng đổi payload trả 409 OperationIdReused. Nếu lần đầu validation thất bại và chưa commit operation thì có thể sửa dữ liệu để thử lại.
- GET `/merchant/operations/{clientRequestId}` chỉ cho chính Admin đã thực hiện; 404 không chứng minh request đang chạy đã thất bại. Không tự đổi key rồi POST để đoán kết quả.
- Snapshot hồ sơ trong replay/lookup Create giữ theo lần tạo. Riêng credentialDelivery/credentialSentAt đọc trạng thái hiện tại. GET `/merchant/{id}` trả hồ sơ hiện tại, có thể đã được chỉnh sau khi tạo.
- clientRequestId dùng cho tạo; operationId vẫn dùng cho sửa/khóa/duyệt/từ chối. Không đổi kiểu key hoặc fingerprint của các operation cũ.

## Trạng thái email

| credentialDelivery | Ý nghĩa | UI phù hợp |
| --- | --- | --- |
| 0 Unknown | Đang gửi, chưa xác định, mất kết nối/ack hoặc legacy | Tài khoản đã tạo; chưa xác nhận kết quả email |
| 1 Sent | SMTP đã chấp nhận; credentialSentAt có giá trị | Đã gửi tới SMTP; không khẳng định email vào inbox |
| 2 Failed | Lỗi xác định, ví dụ thiếu cấu hình/địa chỉ bị SMTP từ chối | Tài khoản đã tạo; gửi email thất bại |

`credentialEmailSent` chỉ true khi Sent. Không coi false luôn là Failed. Email lỗi không rollback tài khoản; replay không resend, kể cả SMTP đã hoạt động lại. Người dùng có thể dùng luồng Quên mật khẩu để thiết lập lại mật khẩu. Chưa có API tự gửi lại credential email. Mật khẩu tạm chỉ tồn tại trong bộ nhớ lúc gửi, không nằm trong DTO/operation/audit.

Migration `20261108184346_AddMerchantCredentialDelivery` thêm hai cột, mặc định legacy Unknown/null; không thay trạng thái hồ sơ hoặc tự gán Sent cho tài khoản cũ.

## Kiểm thử nội bộ

Dùng Mailpit làm SMTP sink riêng, không forward/relay; cổng UI/API chỉ bind loopback. Tham khảo [Docker Mailpit](https://mailpit.axllent.org/docs/install/docker/) và [Chaos SMTP](https://mailpit.axllent.org/docs/integration/chaos/).

Tại thư mục DO_AN:

```powershell
docker compose -f docker-compose.yml -f washgo-be/tests/smoke/merchant-create.compose.yml up -d --build washgo-be mailpit-merchant
powershell -NoProfile -ExecutionPolicy Bypass -File washgo-be/tests/smoke/merchant-create-safety.ps1
```

Script từ chối chạy nếu container BE không dùng đúng SMTP sink, dùng fixture riêng và dọn trong finally. Có tạo trigger tạm chỉ tác động đúng dữ liệu fixture để ép unique violation thật; không chạy trên DB production. Mailpit `messages_count` là tổng kết quả tìm kiếm, `total` là toàn hộp thư nên không dùng để đếm thư theo người nhận.

Khôi phục SMTP bình thường và dọn sink sau test, kể cả test lỗi:

```powershell
docker compose -f docker-compose.yml up -d washgo-be
docker rm -f washgo_merchant_mailpit
```

Không dùng `--remove-orphans` hoặc xóa dữ liệu PostgreSQL. Tệp compose test không dùng cho chạy đồ án bình thường. Kết quả ngày 09/11: smoke create safety và hồi quy profile guards, merchant-admin, merchant-profile-legal PASS; không gửi email ra ngoài.
