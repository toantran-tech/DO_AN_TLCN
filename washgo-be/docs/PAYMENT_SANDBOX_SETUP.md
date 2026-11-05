# Kết nối sandbox VNPay và MoMo

`PAYMENT_PROVIDER=Live` nghĩa là gọi cổng thanh toán thật của nhà cung cấp.
Môi trường test hay production do URL và bộ khóa của cổng quyết định.
Cấu hình hiện tại dùng sandbox chính thức, không thu tiền thật và không dùng Mock.

## Điền thông tin kết nối

Sửa `.env` ở thư mục `DO_AN`, không đưa khóa bí mật lên Git.

| Biến | Thông tin cần điền |
| --- | --- |
| VNPAY_TMN_CODE | Terminal Code của website trên VNPay sandbox |
| VNPAY_HASH_SECRET | Hash Secret được VNPay cấp cho đúng Terminal |
| VNPAY_IPN_URL | URL HTTPS công khai kết thúc bằng `/payment/vnpay-ipn` |
| MOMO_PARTNER_CODE | Partner Code của tài khoản MoMo test riêng |
| MOMO_ACCESS_KEY | Access Key của tài khoản MoMo test |
| MOMO_SECRET_KEY | Secret Key của tài khoản MoMo test |
| MOMO_IPN_URL | URL HTTPS công khai kết thúc bằng `/payment/momo-ipn` |

Đăng ký VNPay sandbox tại https://sandbox.vnpayment.vn/devreg/ .
Lấy thông tin MoMo theo https://developers.momo.vn/v3/docs/payment/onboarding/integration-process/ .
Không dùng bộ khóa minh họa/công khai làm bằng chứng tài khoản riêng đã kết nối thành công.

`localhost` không nhận được IPN từ máy chủ của nhà cung cấp. Dùng backend HTTPS đã deploy
hoặc tunnel công khai khi test. Với VNPay, khai báo URL IPN trong portal sandbox của đúng Terminal;
`VNPAY_IPN_URL` là ghi chú cấu hình, không tự đăng ký URL đó với VNPay.
Với MoMo, backend tự gửi `MOMO_IPN_URL` trong yêu cầu tạo giao dịch.

Tunnel giới hạn route đã chuẩn bị tại `tools/payment-tunnel/compose.yml`.
Tại DO_AN, bật bằng `docker compose -f washgo-be/tools/payment-tunnel/compose.yml up -d`,
xem URL bằng `docker logs washgo_payment_tunnel`, tắt bằng
`docker compose -f washgo-be/tools/payment-tunnel/compose.yml down`.
Chỉ dùng sau khi đồng ý chuyển IPN sandbox qua Cloudflare; không expose toàn bộ backend.
Trang gốc và `/payment/return` là trang giới thiệu/điều hướng thử nghiệm;
chỉ `/payment/vnpay-ipn` (GET), `/payment/momo-ipn` (POST) được chuyển tới BE.
URL Quick Tunnel có thể đổi khi tạo lại tiến trình; khi đổi phải sửa `.env`, recreate BE
và cập nhật IPN trên portal VNPay. Tắt máy/Docker thì URL không nhận IPN được.

`VNPAY_RETURN_URL` và `MOMO_REDIRECT_URL` là nơi trình duyệt quay lại sau checkout.
Mặc định mẫu đặt về Swagger; cấu hình local có thể dùng trang `/payment/return` của tunnel.
Redirect không phải bằng chứng đã thanh toán;
chỉ IPN hợp lệ mới xác nhận giao dịch và thay đổi đơn.

## Áp dụng và kiểm thử

Tại thư mục `DO_AN` sau khi điền cấu hình:

```powershell
docker compose up -d washgo-be
```

Đăng nhập Customer trên Swagger, đưa đơn tới trạng thái chờ thanh toán, gọi
`POST /payment/create-url` với `orderId` của mình:

```json
{"orderId":"GUID_DON_HANG","paymentMethod":2}
```

`2` là VNPay, `3` là MoMo. `paymentUrl` phải thuộc trang checkout của nhà cung cấp,
không phải `/payment/mock-checkout`. Mở URL đó và thanh toán bằng tài khoản/thẻ test
do nhà cung cấp hướng dẫn. Sau IPN, xem chi tiết đơn để đối chiếu trạng thái/số tiền.
Không gọi `/payment/callback` để giả lập thành công; Live chặn callback này.

Trước khi chốt hoàn tất: kiểm tra thanh toán thành công, hủy/thất bại, chữ ký/số tiền sai,
IPN lặp không ghi nhận thêm; test cả tiền giặt, phí hủy và phí lưu kho.
Các script `vnpay-sandbox.ps1`/`momo-sandbox.ps1` có phần tự ký IPN để test handler;
script PASS không thay thế việc thanh toán qua trình duyệt và nhận IPN thật từ nhà cung cấp.

Thiếu khóa hoặc IPN chưa công khai: chưa thể kết luận tích hợp end-to-end hoàn tất.
