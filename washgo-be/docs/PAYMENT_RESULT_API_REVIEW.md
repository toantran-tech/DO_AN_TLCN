# Bàn giao FE — URL kết quả thanh toán VNPay và MoMo

Ngày 23/11/2026. Thực hiện `Docs/cần làm/BACKEND_PAYMENT.md`. Áp dụng cho cả VNPay và MoMo, purpose 1 (tiền giặt), 2 (phí quá hạn/lưu kho), 3 (phí hủy).

> Luồng checkout gộp và các route mới dùng hợp đồng tại [PAYMENT_COMBINED_API_REVIEW.md](PAYMENT_COMBINED_API_REVIEW.md). Các mục dưới đây lưu lại hợp đồng trước thay đổi để đối chiếu tích hợp cũ.

## 1. Luồng quay về (BE trung gian → FE)

| Cổng | Cấu hình trình duyệt quay về | Endpoint BE |
| --- | --- | --- |
| VNPay | `Payment:VnPay:ReturnUrl` (`VNPAY_RETURN_URL`) | `GET /payment/vnpay-return` |
| MoMo | `Payment:Momo:RedirectUrl` (`MOMO_REDIRECT_URL`) | `GET /payment/momo-return` |

`docker-compose.yml` mặc định trỏ hai biến này về `http://localhost:5000/payment/vnpay-return` và `/payment/momo-return`. Nếu file `.env` đang tự đặt `VNPAY_RETURN_URL`/`MOMO_REDIRECT_URL` thì phải đổi theo. IPN giữ nguyên: `/payment/vnpay-ipn`, `/payment/momo-ipn`. IPN phải là địa chỉ HTTPS công khai; `localhost` chỉ dùng cho trình duyệt.

BE trả **303 See Other** tới trang FE:

| Trang FE | Khi nào |
| --- | --- |
| `/payment/success?orderId=&purpose=&sessionId=` | Kết quả đã được xác minh (IPN hoặc query) và đã ghi đúng nghĩa vụ. Với purpose 1, đơn đã Paid. |
| `/payment/failed?…` | Thất bại cuối cùng đã xác minh. Phiên tạo bị cổng từ chối cũng vào đây. |
| `/payment/cancelled?…` | Khách hủy tại cổng, đã xác minh: VNPay `24`, MoMo `1006`. |
| `/payment/pending?…` | Chưa có kết luận: IPN chưa đến, chờ xác minh. BE xếp **một** yêu cầu truy vấn cổng; không ghi nhận gì từ dữ liệu trình duyệt. |
| `/payment/review?…` | Cần đối soát: tiền thu chưa áp dụng được, trùng thu, số tiền trên URL quay về không khớp, đã hoàn tiền. |
| `/payment/pending` (không tham số) | Chữ ký sai/thiếu hoặc không nhận ra giao dịch. Không lộ đơn nào; FE hướng khách mở danh sách đơn. |

- `orderId` là ID đơn WashGo, không phải MoMo `orderId`. `sessionId` là ID mờ của **lần thanh toán đó**.
- URL kết quả không chứa chữ ký, token, reference hay dữ liệu cổng.
- Mở hoặc sửa tay URL kết quả không đổi DB. FE phải đọc API kết quả (mục 2) trước khi hiển thị.
- Origin FE lấy từ cấu hình, không bao giờ lấy từ query:
  - `Payment:Frontend:BaseUrl` (`PAYMENT_FRONTEND_URL`, mặc định `http://localhost:5173`);
  - `Payment:Frontend:AllowedOrigins` (`PAYMENT_FRONTEND_ALLOWED_ORIGINS`).

  BaseUrl phải là origin tuyệt đối, không có path/query, HTTPS (trừ loopback) và nằm trong allowlist nếu allowlist được đặt. Sai cấu hình thì endpoint trả 503 kèm câu hướng dẫn, không redirect đi đâu cả.
- Rate limit `payment-return`: 30 lần/phút/IP.
- `/payment/return` cũ giữ nguyên (trang chữ). FE route `/payment/return` không cần tự chuyển hướng nữa; tránh chạy hai cơ chế cùng lúc.

## 2. API kết quả cho Customer

`GET /payment/orders/{orderId}/result?purpose=1|2|3[&sessionId=]`

- Quyền: CustomerOnly, chỉ chủ đơn.
- `no-store`, rate limit `payment-result` (60 lần/phút/người).
- Chỉ đọc.
- Có `sessionId`: trả đúng lần thanh toán đó. Không thuộc đơn/purpose hoặc của khách khác thì trả 404.
- Không có `sessionId`: trả lần mới nhất của đơn/purpose, hoặc `NotStarted` nếu chưa có lần nào.

| Trường | Ý nghĩa |
| --- | --- |
| `orderId`, `orderCode`, `purpose`, `sessionId`, `paymentMethod` | Định danh lần thanh toán |
| `status` | `Succeeded`, `Failed`, `Cancelled`, `Pending`, `RequiresReview`, `Refunded`, `NotStarted` |
| `resultPage` | `success`, `failed`, `cancelled`, `pending`, `review` (null khi NotStarted) |
| `reasonCode` | Xem bảng dưới |
| `verified` | Kết quả đến từ IPN/query đã kiểm chữ ký, không phải từ redirect |
| `amount`, `pointsRedeemed` | Tiền qua cổng của lần này; điểm chỉ tính khi thành công |
| `obligationSettled` | Nghĩa vụ của purpose đã xong: purpose 1 = đơn Paid; purpose 2 = không còn phí lưu kho; purpose 3 = đã trả đủ phí hủy |
| `recordedAt` | Lúc ghi nhận kết quả cuối |
| `serverNow`, `retryAfterSeconds` | Thăm dò khi Pending theo `retryAfterSeconds` |
| `supersededByNewerSession` | Đã có lần thanh toán mới hơn; lần cũ vẫn trả đúng kết quả của nó |
| `verificationOperationId`, `verificationState` | Yêu cầu xác minh gần nhất của lần này |
| `capabilities.canVerify` | Còn Pending và chưa có xác minh đang chạy → được `POST verify-session` (operationId UUID) |
| `capabilities.canContinue` | Phiên cũ còn dùng được: lấy URL qua `active-session` |
| `capabilities.canRetry` | Lần này đã đóng (Failed/Cancelled), không còn phiên mở/đối soát và nghĩa vụ còn phải trả → được tạo thanh toán mới |
| `capabilities.canViewOrder` | Luôn true |

`reasonCode`:

| status | reasonCode |
| --- | --- |
| Succeeded | `PaymentRecorded` |
| Failed | `GatewayDeclined`, `SessionNotCreated` |
| Cancelled | `CustomerCancelled` |
| Pending | `AwaitingGatewayResult`, `PaymentVerificationRequired`, `PaymentSessionReady`, `PaymentInitializing`, `PaymentExpiredRequiresVerification` |
| RequiresReview | `PaymentRequiresReview`, `ObligationNotApplied`, hoặc lý do đối soát đã lưu như `OrderAlreadyPaid`, `LateReceiptAfterClosedSession`, `CancellationFeeNoLongerDue` |
| Refunded | `PaymentRefunded` |
| NotStarted | `NoPaymentAttempt` |

Lỗi:

- 400 `meta.code = InvalidPurpose`.
- 401 khi chưa đăng nhập. FE cho đăng nhập rồi quay lại cùng URL kết quả: chỉ giữ `orderId`/`purpose`/`sessionId`, không lưu dữ liệu cổng, không gửi lại thanh toán.
- 403 khi không phải Customer.
- 404 khi không phải đơn của mình hoặc sai `sessionId`.

## 3. Bảng mã cổng (đã có, dùng chung cho IPN, query và trang kết quả)

| Ý nghĩa | VNPay (`vnp_ResponseCode` / `vnp_TransactionStatus`) | MoMo `resultCode` |
| --- | --- | --- |
| Thành công | `00` / `00` | `0` |
| Khách hủy (cuối cùng) | `24` / `02` | `1006` |
| Thất bại cuối cùng | `09, 10, 11, 12, 13, 51, 65, 75, 79` với status `02` | `1001–1005, 1007, 1017, 1026` |
| Chưa kết luận / cần xác minh | Mọi mã khác (ví dụ `01`, `04`, `05`, `06`, `07`) | Mọi mã khác (ví dụ `1000`, `7000`, `7002`, `9000`) |

- BE kiểm tra chữ ký riêng cho từng cổng: VNPay HMAC-SHA512 trên các trường `vnp_*` sắp ordinal; MoMo HMAC-SHA256 theo thứ tự trường của MoMo, đúng partnerCode.
- BE đối chiếu reference, số tiền, phương thức, đơn và purpose.
- IPN lặp lại hoặc đến sau không hạ một giao dịch đã thành công. Tiền thu không áp dụng được thì vào đối soát, không hiện thành công.
- Lỗi mạng, 5xx, timeout, sai chữ ký, hết giờ đếm cục bộ đều **không** là thất bại; trạng thái vẫn Pending.
- Thử lại chỉ khi `canRetry`. Không tự gọi lại `create-url` hoặc đổi cổng.

## 4. Kiểm thử

- C# `tests/smoke/Payments` (CI): phân loại trang success/failed/cancelled/pending/review cho cả hai cổng, gồm mã hủy, thất bại, chờ, chưa áp dụng và đối soát.
- Smoke API `tests/smoke/payment-result-redirect.ps1` (CI, ký bằng khóa CI giả, provider Mock):
  - VNPay và MoMo: không chữ ký hoặc chữ ký giả → pending chung;
  - trước IPN → pending và đúng một yêu cầu xác minh;
  - sau IPN → success/failed/cancelled; IPN lặp không ghi trùng;
  - sai số tiền → review;
  - purpose 3 (phí hủy) không đổi tiền giặt sang Paid;
  - quyền 401/403/404, lần cũ giữ kết quả riêng, `canRetry`.
- Chưa kiểm với cổng thật: cần chạy sandbox VNPay/MoMo có IPN HTTPS công khai (tunnel) để nghiệm thu kết nối.
