> Bổ sung 16/11/2026: FE dùng thêm [Payment session recovery](PAYMENT_SESSION_RECOVERY_API_REVIEW.md) cho GET phiên/quote, giới hạn tiền gửi cổng, verify và xác nhận tạo phiên thay thế. Giữ nguyên concurrency/điểm đã chốt; test/deploy mới chưa chạy.

# Thanh toán và phân công đồng thời — bàn giao 15/11/2026

## Trạng thái

Đã sửa source A–D của [plan](ORDER_PAYMENT_CONCURRENCY_PLAN_20261115.md). Chưa chạy test/build Docker hoặc áp migration trong đợt này. Test tranh ô cuối PASS 5/5 trước đó không phải kết quả kiểm thử các sửa đổi thanh toán/phân công mới.

Migration mới: `20261115190000_PaymentSessionConcurrency`. Phải triển khai migration cùng bản code; không dùng code mới với DB chưa có các cột này.

## 1. Các route tạo URL giữ nguyên

- `POST /payment/create-url`: tiền giặt.
- `POST /payment/overdue-fee/create-url`: phí kho.
- `POST /payment/cancellation-fee/create-url`: phí hủy.

JWT Customer, đúng chủ đơn. Request vẫn dùng `orderId`, `paymentMethod` (2 = VNPay, 3 = MoMo); số tiền tính tại BE.

- Một phiên hiệu lực theo `(orderId, purpose)`; DB có unique index phòng vệ.
- Reference được commit trước khi gọi cổng. MoMo không tự retry POST với một reference mới khi timeout.
- Gửi lại cùng phương thức/số tiền khi URL đã sẵn sàng: HTTP 200, cùng reference và URL, `meta.code=PaymentSessionReused`, `meta.reused=true`.
- Khác phương thức: HTTP 409 `PaymentMethodConflict`; không mở cổng thứ hai.
- Phiên chưa sẵn sàng: 409 `PaymentInitializing`, `meta.retryAfterSeconds=2`. FE có thể chờ rồi gửi lại cùng request; BE giữ nguyên reference.
- Timeout, Creating cũ sau 2 phút, hoặc URL VNPay quá hạn 15 phút: 409 `PaymentVerificationRequired`. Hai phút chỉ là ngưỡng hiển thị cần xác minh, không phải căn cứ giao dịch thất bại; không mở phiên thay thế.
- Amount thay đổi: 409 `PaymentAmountChanged`; đơn đổi trong lúc gọi cổng: 409 `PaymentStateChanged`.
- Pending cũ trùng: 409 `LegacyPaymentsRequireReview`; giữ nguyên dữ liệu cũ để đối soát.
- Đơn đã trả đủ/không còn trạng thái cho thanh toán: 400.

DTO PaymentResult bổ sung `transactionStatus`, `sessionState`, `requiresReconciliation`; không xóa field cũ.

SessionState: 0 Legacy, 1 Creating, 2 Ready, 3 Unknown, 4 Closed.

PaymentTransactionStatus giữ nguyên 1 Pending, 2 Succeeded, 3 Failed, 4 Refunded; thêm **5 RequiresReview**.

## 2. Callback và tiền không áp dụng được vào đơn

- VNPay/MoMo vẫn phải qua kiểm tra chữ ký/merchant/amount hiện có. Callback mô phỏng chỉ dùng khi provider Mock và JWT Admin.
- Cùng một receipt callback lặp: không cộng tiền, chuyển trạng thái, tạo refund, timeline hoặc notification lần thứ hai.
- Callback có thể tới trước khi BE lưu URL: reference đã có trong DB. Bước hoàn thiện URL không được đổi Succeeded về Pending.
- Không coi mọi resultCode khác 0 là thất bại cuối cùng. MoMo chờ xác nhận/đang xử lý và VNPay chưa hoàn tất/đảo giao dịch/nghi ngờ được giữ Unknown để xác minh, không mở thêm phiên. Quy tắc đối chiếu [MoMo result codes](https://developers.momo.vn/v3/docs/payment/api/result-handling/resultcode/) và [tài liệu VNPay](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html); danh sách thất bại cuối dùng whitelist bảo thủ trong PaymentGatewayStatusRules.
- Giao dịch khác xác nhận đã thu tiền nhưng đơn đã Paid/không còn đủ điều kiện: lưu status RequiresReview, bằng chứng gateway và lý do; không bỏ giao dịch lại Pending và không ép đơn đổi trạng thái.
- Tạo một RefundRequest Pending riêng cho khoản không áp dụng được; Admin duyệt và xác nhận hoàn ngoài hệ thống qua các route đang có. Chưa tự gọi API hoàn tiền cổng.
- RefundRequest bổ sung `isExcessPayment`; luôn gắn `originalPaymentTransactionId` đúng khoản cần trả lại.
- Hoàn khoản dư không đổi PaymentStatus tiền giặt, không khấu trừ phí hủy lần nữa. Khoản này và refund tương ứng không tính vào doanh thu, hoa hồng hoặc payout Merchant.
- `requiresReconciliation` là phân loại tài chính giữ lại cả sau khi đã hoàn; `reconciledAt` ghi nhận xử lý xong. Không xóa cờ để đưa khoản thu dư vào doanh thu.

FE không chỉ dựa vào HTTP 200 của callback mô phỏng để hiện đơn đã Paid; đọc trạng thái đơn và `requiresReconciliation`.

## 3. Admin xem danh sách cần đối soát

`GET /payment/reconciliations?page=1&take=20&includeResolved=false`

JWT Admin. Trả PaginatedResult chuẩn (`list`, `total`, `currentPage`, `pageSize`...), gồm:

- Khoản đã thu chưa áp dụng và chưa hoàn xong.
- Phiên Unknown, Creating quá ngưỡng, Legacy còn Pending, VNPay quá hạn và nhóm Pending trùng.
- Reference, mã giao dịch cổng, số tiền, purpose, trạng thái, lý do, mốc xác minh và `refundRequestId` nếu có.

Không trả khóa ký/API key hoặc tự chốt kết quả cổng. `includeResolved=true` thêm các khoản tiền dư đã xử lý; không biến thành danh sách mọi giao dịch.

Route này không phải chức năng "Admin bấm xác nhận đã thu". Với phiên timeout/Legacy không có kết quả, cần đối chiếu gateway; hiện chưa thêm adapter query/cancel cổng hoặc nút đóng phiên thủ công. Không xóa Pending bằng SQL để tạo URL mới.

## 4. Phân công

Giữ phạm vi một Admin và các route phân công hiện có.

- Gửi operationId theo một thao tác của UI; retry dùng lại cùng key. Thao tác khác phải sinh key mới.
- BE lấy khóa operation trước khi đọc replay, dùng chung giữa các đường ghi OrderOperation.
- Cùng key/payload/actor: HTTP 200, `meta.replayed=true`, không gán/ghi audit hoặc notification thành công hai lần.
- Cùng key nhưng khác payload/đơn: 409 `OperationIdReused`.
- expectedRevision cũ: 409 `RevisionMismatch`; client tải lại trước khi tạo thao tác mới.
- Batch khóa capacity Merchant theo ID, sau đó khóa OrderId theo ID tăng dần. Response vẫn theo thứ tự input.
- Một đơn lỗi thì rollback toàn batch. Hai batch tranh đơn có thể một thành công, một lỗi nghiệp vụ/version; không cam kết cả hai thành công.
- Chưa thêm retry mù cho toàn hệ thống. Thứ tự khóa sửa nguy cơ đã tìm thấy, không phải cam kết không bao giờ có deadlock ở mọi module.

## 5. Test đã viết, chưa chạy

1. `tests/smoke/Payments/Payments.csproj`: HTTP handler giả lập MoMo, reference/orderId/requestId ổn định, không retry ẩn, phân biệt từ chối với timeout/mất mạng/JSON lỗi/thiếu URL.
2. `tests/smoke/payment-assignment-concurrency.ps1`: hai request tạo phiên, reuse/đổi cổng/Unknown, callback trước URL và callback lặp, tiền dư/hoàn đúng nguồn, Legacy Pending trùng, replay phân công/key collision, hai batch đảo thứ tự và rollback.
3. `tests/smoke/order-cancellation.ps1`: cập nhật kỳ vọng retry phiên phí hủy từ lỗi sang reuse.
4. `tests/smoke/locker-last-slot-race.ps1`: giữ hồi quy tranh ô cuối đã có.

Đã nối 1–2 vào `tests/ci/Run-ApiSmoke.ps1`. PR CI chạy DB cô lập/provider Mock; không cần secret MoMo/VNPay thật.

Chạy thủ công từ `washgo-be` sau khi fen build và áp migration, **chỉ trên local provider Mock**:

```powershell
dotnet run --project tests/smoke/Payments/Payments.csproj -c Release
powershell -ExecutionPolicy Bypass -File tests/smoke/payment-assignment-concurrency.ps1 -Database washgo_main
```

Đổi Database theo DB local thực tế. Script cần tài khoản seed Admin/Customer/Merchant với mật khẩu dev mặc định và tạo fixture riêng. Không chạy script lên staging hoặc cổng thu tiền thật. Có thể để PR CI chạy với DB seed cô lập thay cho local.

Các kiểm tra tích hợp còn cần chạy riêng: gateway thật gửi IPN trong lúc HTTP create đang chờ; timeout/crash tiến trình rồi phục hồi; nghiệm thu sandbox và migration trên dữ liệu Pending legacy thực tế. Unit gateway giả lập không thay thế các kiểm tra đó.

## 6. Triển khai và rollback

- Backup trước migration. Migration giữ nguyên mọi giao dịch cũ, không tự Failed/Refunded hoặc xóa Pending trùng.
- Kiểm tra danh sách đối soát sau deploy và trao đổi với FE về các mã 409 mới.
- Không rollback code cũ rồi tiếp tục nhận thanh toán với dữ liệu RequiresReview chưa xử lý. Down migration chủ động từ chối xóa bằng chứng tiền dư/hoàn tiền dư; cần export và kế hoạch rollback riêng.
- Không đổi PAYMENT_PROVIDER staging sang Mock để vượt nghiệm thu. Mock chỉ dùng DB local/CI test.
