# Bàn giao FE — phiên thanh toán, báo phí và xác minh

Ngày 16/11/2026. Phạm vi: `BACKEND_PAYMENT.md`, cụm 1–2 của `BACKEND_REQUIREMENTS_PLAN_20261116.md`. Source đã bổ sung; **chưa chạy test, build Docker, áp migration hoặc deploy đợt này**. Không coi mô phỏng CI là đã nghiệm thu sandbox VNPay/MoMo.

## 1. Quy ước và quyền

- JWT Customer; chỉ đọc/thao tác đơn của chính mình. Anonymous 401, role khác 403, đơn/operation khách khác 404.
- `purpose`: **1 tiền giặt**, **2 phí quá hạn/lưu kho**, **3 phí hủy**. `paymentMethod`: **2 VNPay**, **3 MoMo**.
- Response vẫn dùng envelope `{meta,data,succeeded,message,errors,statusCode}`. Các GET mới có `Cache-Control: no-store`; không dùng cache giữa tài khoản/đơn/purpose.
- GET không tạo reference, mở ví, giữ/nhả điểm, gọi gateway hoặc gửi callback. Không trả URL, transactionReference, chữ ký, token thanh toán hay thông tin đối tác.
- Không suy phương thức phí từ `WashOrder.PaymentMethod`. Mỗi cặp `(orderId,purpose)` có nghĩa vụ/phiên riêng.

## 2. Đọc phiên hiện hành

`GET /payment/orders/{orderId}/active-session?purpose=3`

Không còn phiên chặn: `data: null`. Có một phiên: ví dụ:

```json
{
  "orderId": "<uuid>", "purpose": 3, "sessionId": "<uuid>",
  "paymentMethod": 2, "amount": 30000, "pointsToRedeem": 0,
  "transactionStatus": 1, "sessionState": 2,
  "expiresAt": "2026-11-16T10:15:00Z", "serverNow": "2026-11-16T10:01:00Z",
  "requiresReconciliation": false, "reasonCode": "PaymentSessionReady",
  "canContinue": true, "canVerify": true, "canCreateReplacement": false,
  "retryAfterSeconds": 10
}
```

`sessionState`: 0 Legacy, 1 Creating, 2 Ready, 3 Unknown, 4 Closed. Dùng capability/reason, không tự quyết định từ enum/countdown. `expiresAt=null` nghĩa là chưa biết thời hạn từ dữ liệu cổng, không tự gán 15 phút cho MoMo. VNPay giữ thời hạn ban đầu, reuse không gia hạn.

Session còn trả `verificationOperationId`/`verificationState` nếu có operation trước đó. Khi đang Queued/Verifying, `canContinue=false`, `canVerify=false`, reason=`PaymentVerificationInProgress`; FE khôi phục operation này sau refresh và thăm dò GET thay vì tạo yêu cầu mới.

| Kết quả | FE xử lý |
| --- | --- |
| null | Đọc quote hiện tại, chỉ mở cổng nếu `canCreate` và method được phép |
| `canContinue=true` | Chọn/khóa đúng method và số điểm đã giữ; POST create-url cùng dữ liệu để lấy lại URL |
| `PaymentInitializing` | Chờ `retryAfterSeconds`, đọc lại; không gửi tạo phiên bằng cổng khác |
| Unknown/hết hạn, `canVerify=true` | Hiện “Kiểm tra thanh toán”, dùng POST verify dưới đây |
| Nhiều phiên legacy | Method/sessionId null, reason `MultipleSessionsRequireReview`; không chọn tùy tiện một phiên |
| `requiresReconciliation=true` | Hiện cần đối soát; không tiếp tục/đổi cổng |

Khoản thu dư chưa đối soát vẫn được trả về để chặn tạo thêm; vì vậy đơn đã Paid có thể còn session cần review, nhưng `canContinue=false`. Sau khi hết blocker, quote vẫn kiểm tra nghĩa vụ, không cho trả tiền giặt hai lần.

## 3. Báo phí và hạn mức

`GET /payment/orders/{orderId}/quote?purpose=1&pointsToRedeem=1000`

`data`: `grossDue`, `pointsToRedeem`, `availablePoints`, `maximumPoints`, `cashAmount`, `earnPercent`, `maxUsePercent`, `computedAt`, `quoteToken`, `canCreate`, `reasonCode`, `message`, `recommendedMethod`, `activeSession`, `methods`.

- `grossDue`: tiền giặt sau voucher, phí hủy còn thiếu hoặc phí quá hạn động còn thiếu, theo purpose. `cashAmount`: tiền gửi cổng sau giảm điểm. Điểm chỉ dành cho purpose 1; purpose 2/3 có earn/maxUse=0.
- Mỗi method có `enabled`, `reasonCode`, `policyMinAmount`, `gatewayMinAmount`, `gatewayMaxAmount`, `minAmount`, `maxAmount`, `limitSource`, `walletOnly`. Dùng **cashAmount**, không dùng giá niêm yết để xét hạn mức.
- Mặc định VNPay: mức tối thiểu **WashGo quy định 10.000đ**; không tuyên bố đây là giới hạn chung cho mọi ngân hàng/VNPay merchant. Min/max riêng của merchant có thể cấu hình.
- Mặc định MoMo wallet: **1.000–50.000.000đ**, hai đầu mút được phép. Dưới 10.000đ `walletOnly=true`, request thực tế dùng `captureWallet` để không đưa sang thẻ ATM. Từ 10.000đ dùng RequestType đã cấu hình (`payWithMethod`/`captureWallet`). Quyền kênh của merchant phải kiểm tra tại sandbox.
- Khoản dưới mọi cổng, cổng thiếu cấu hình/tắt hoặc giá không hợp lệ: không có method hợp lệ, trả hướng dẫn liên hệ WashGo. Không tăng/miễn phí hay fake Paid. MoMo không tự làm tròn VND lẻ.
- Khi có phiên chặn, các method của quote đều disabled (`ExistingSessionTakesPriority`). Phiên cũ ưu tiên hơn gợi ý cổng theo tiền mới.

## 4. Tạo/reuse URL và xác nhận báo phí mới

Giữ các route hiện có:

| Purpose | POST |
| --- | --- |
| 1 | `/payment/create-url` |
| 2 | `/payment/overdue-fee/create-url` |
| 3 | `/payment/cancellation-fee/create-url` |

Body tiền giặt/phí hủy:

```json
{"orderId":"<uuid>","paymentMethod":3,"pointsToRedeem":0,"expectedQuoteToken":"<quoteToken vừa đọc>"}
```

Phí quá hạn dùng body DTO hiện có `{orderId,paymentMethod,expectedQuoteToken}`; không gửi điểm. FE không gửi amount tự tính. Hạn mức/điểm/nghĩa vụ được kiểm tra lại dưới khóa trước khi lưu reference/giữ điểm/gọi create gateway.

409 `meta.code`: `PaymentMethodConflict`, `PaymentPointsConflict`, `PaymentAmountChanged`, `PaymentInitializing`, `PaymentVerificationRequired`, `LegacyPaymentsRequireReview`, `PaymentRequiresReview`; kèm projection an toàn `activeSession`/`existingPaymentMethod` khi có phiên. Không coi mọi 409 là retry POST ngay.

`QuoteChanged`/`QuoteConfirmationRequired` trả quote mới trong `meta.quote`: hiển thị cho khách xác nhận rồi gửi lại token mới. Token có gắn mốc đóng phiên đã xác minh nên quote trước khi đóng không dùng làm xác nhận thay thế. Lần tạo đầu vẫn nhận body cũ không token để tương thích; **phiên thay thế sau một phiên đóng đã xác minh bắt buộc token**. Khuyến nghị FE luôn gửi token khi tạo mới. Reuse cùng phiên giữ đúng reference/URL/amount/points, không tạo reference khác.

## 5. Xác minh và thăm dò operation

`POST /payment/orders/{orderId}/verify-session`

```json
{"operationId":"<UUID mới do FE sinh>","purpose":1}
```

- Trả 202 khi Queued/Verifying (retry cùng key có thể 200); không chờ HTTP gateway trên request FE. Lưu operationId để tiếp tục sau refresh/mất mạng.
- `GET /payment/verification-operations/{operationId}`: data gồm operationId/orderId/purpose/state/reasonCode/createdAt/updatedAt/retryAfterSeconds/requiresReconciliation.
- State: `Queued`, `Verifying`, `Completed`, `NeedsReview`. Operation khác khi một query đang chạy sẽ theo dõi chung kết quả, không tạo query song song. Cùng key nhưng khác đơn/purpose trả 409 `OperationIdReused`.
- POST giới hạn 10 lần/phút/Customer, không xếp hàng; vượt trả 429. Cooldown mỗi reference 60 giây; operation mới trong cooldown theo kết quả operation trước. Không sinh operation mới liên tục; thăm dò GET theo `retryAfterSeconds` (2–120 giây khi active).
- Completed chỉ cho biết việc xác minh đã kết thúc. Đọc lại session, quote và đơn; không giả định cứ Completed là Paid. `GatewayVerifiedFailure` khác `GatewayVerifiedSuccess`; `NoActiveSession` cũng là Completed.
- Worker mặc định quét 10 giây/lần, claim lease 2 phút, HTTP timeout 35 giây, backoff 60/120/240/480 giây + jitter, tối đa 5 lần (cấu hình 1–10), sau đó NeedsReview, giữ khóa phiên/điểm và cảnh báo Admin. Render ngủ/restart không mất job đã lưu; xử lý tiếp khi process chạy lại.
- Timeout, 5xx, JSON lỗi, chữ ký/correlation/amount sai, “không tìm thấy”, pending/risk/refund không được coi là chưa thu tiền. Giữ phiên. Không có thao tác FE giả kết quả để nhả điểm.
- Query và IPN dùng cùng đường settle dưới khóa transaction → order → wallet. Thành công nhận trước/sau nhau chỉ cộng tiền/điểm một lần. Bằng chứng thất bại cuối mới đóng phiên, nhả điểm; query không tự tạo phiên thay thế. Receipt thành công muộn sau khi đã đóng được lưu xử lý thu dư/hoàn tiền, không mất dấu hay cộng thưởng hai lần.

Luồng FE: **mở dialog → GET session → tiếp tục đúng phiên hoặc verify → poll operation → GET session + quote + đơn → khách xác nhận báo phí → POST tạo lại nếu được phép**. Trang return chỉ điều hướng/thông báo, không chứng minh thanh toán thành công.

## 6. Cấu hình và triển khai BE

Migration mới: **20261116120000_AddPaymentVerification**, gồm bảng `washgo.payment_verifications`, unique một job active/reference, lease/attempt và 3 mốc gateway trên payment transaction. Migration additive; không backfill ngày gateway giả cho phiên legacy. Legacy VNPay có thể lấy ngày từ URL đã lưu; nếu thiếu ngày thì cần đối soát thay vì đoán ngày tạo.

Các biến Render tùy chọn (tên .NET, không cần đổi secrets hiện có):

```dotenv
Payment__Verification__Enabled=true
Payment__Verification__MaxAttempts=5
Payment__VnPay__QueryEndpoint=https://sandbox.vnpayment.vn/merchant_webapi/api/transaction
Payment__VnPay__QueryIpAddress=127.0.0.1
Payment__Momo__QueryEndpoint=https://test-payment.momo.vn/v2/gateway/api/query
Payment__Limits__VnPay__PolicyMinAmount=10000
Payment__Limits__Momo__GatewayMinAmount=1000
Payment__Limits__Momo__GatewayMaxAmount=50000000
```

`Payment:Limits:{VnPay|Momo}:Enabled/PolicyMinAmount/GatewayMinAmount/GatewayMaxAmount` là cấu hình BE. Không thêm bí mật vào FE. Endpoint query chỉ cho HTTPS host chính thức; không follow redirect. Production phải khai báo query endpoint phù hợp, không fallback từ production sang sandbox. Mock query không sinh kết quả tiền thật; CI tắt worker để test HTTP operation ổn định, fake HTTP suite kiểm adapter riêng.

VNPay query ký request và xác thực response HMAC SHA512, đối chiếu merchant/reference/amount/type. Chỉ responseCode=00 và transactionStatus=00 có transactionNo hợp lệ mới settle; status=02 mới đóng thất bại. MoMo query v2 ký request HMAC SHA256 (canonical có requestId), kiểm response correlation/amount từ HTTPS chính thức; tài liệu query không có cùng response signature như IPN nên không tự giả một chữ ký response. Các trạng thái cuối dùng chung bảng phân loại của IPN.

Nguồn hợp đồng cổng: [VNPay QueryDR](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html), [MoMo Query v2](https://developers.momo.vn/v3/docs/payment/api/payment-api/query/), [MoMo result codes](https://developers.momo.vn/v3/docs/payment/api/result-handling/resultcode/). Cần chủ merchant kiểm quyền query, callback public và các kết quả sandbox thực trước nghiệm thu.

## 7. Test người dùng chạy

Đã nối `tests/smoke/PaymentRecovery/PaymentRecovery.csproj` (fake HTTP, không gọi cổng) và `tests/smoke/payment-session-recovery.ps1` vào `tests/ci/Run-ApiSmoke.ps1`. CI tiếp tục chạy payment concurrency/loyalty hiện có. Người dùng chạy suite CI trên DB tạm, backup rồi áp migration/build/deploy theo quy trình hiện có; **chưa có kết quả PASS cho test mới**.

Nghiệm thu sandbox thêm: VNPay 9k legacy hết hạn/query chưa rõ; terminal failure → quote mới → MoMo; IPN/query cùng lúc; return mất mạng; MoMo ví dưới 10k, đúng 1k/50m và hạn mức merchant; phiên giữ điểm/nhả một lần. Kiểm cả purpose 1/2/3, hai tab, khác Customer và 429. Không chạy smoke fixture lên Neon/staging hay live gateway.
