# Bàn giao FE — Thanh toán gộp theo đơn

Màn hình danh sách, chi tiết, lưu giữ và checkout lấy số phải trả từ cùng API. Giao diện thanh toán gộp dùng `purpose=4`. Các lần thanh toán đã tạo theo `purpose=1/2/3` vẫn có thể tiếp tục hoặc xác minh theo purpose gốc.

## Bảng kê và thanh toán

1. Gọi `GET /payment/orders/{orderId}/summary?pointsToRedeem=0` bằng token Customer. `obligations[]` có `purpose` (1 dịch vụ, 2 lưu giữ, 3 hủy), `due`, `paid`, `outstanding`. Dùng `outstanding` của bảng kê cho tổng trước dùng điểm, `quote.cashAmount` cho số tiền đi cổng. `quote.computedAt`, `quoteExpiresAt` do BE trả.
2. Khi đổi số điểm, lấy lại bảng kê. Hiển thị `quote.methods`, `quote.canCreate`, `quote.maximumPoints`. Gửi `POST /payment/orders/{orderId}/checkout` với body ví dụ dưới đây. Dùng `data.paymentUrl` để chuyển khách sang cổng nếu có.
3. Nếu BE trả `409` với `meta.code=QuoteChanged`, đọc `data.summary` và để khách xác nhận lại. Nếu tổng bằng 0, response có `data.summary` và URL trống. Đọc `settled` để phân biệt đã trả đủ với trường hợp chưa phát sinh khoản cần thu.
4. Nếu `quote.activeSession` có phiên cũ hoặc gộp, dùng `purpose`, `paymentMethod`, `pointsToRedeem` của phiên đó khi tiếp tục. Phiên gộp dùng route checkout; phiên 1/2/3 dùng route tạo URL tương ứng. Khi được phép xác minh, gọi `POST /payment/orders/{orderId}/verify-session` với `operationId` UUID và purpose thực của phiên. Thăm dò theo `retryAfterSeconds`. Phiên đang chờ hoặc đang đối soát chặn tạo phiên chồng lấn.

```json
{
  "paymentMethod": 2,
  "pointsToRedeem": 0,
  "expectedQuoteToken": "<data.quote.quoteToken>"
}
```

`paymentMethod=2` là VNPay và `3` là MoMo. `refunds` tách tiền/điểm chờ xử lý và đã hoàn. `cancellationOffset` là phần đã cấn vào phí hủy. `cancellationRefundRemaining` là phần có thể còn cần xử lý sau khi trừ các yêu cầu hoàn đang xử lý hoặc đã hoàn. `cancellationBasis` và `cancellationRatePercent` có thể rỗng ở dữ liệu cũ thiếu căn cứ.

## Sau khi quay lại từ cổng

| Trạng thái | Route FE |
| --- | --- |
| Xác minh thành công và đã phân bổ | `/payment/success` |
| Thất bại đã xác định | `/payment/failed` |
| Khách hủy đã xác minh | `/payment/cancelled` |
| Đang xác minh hoặc cần đối soát, có định danh hợp lệ | `/orders/{orderId}` |
| Không xác định được đơn an toàn | `/orders` |

Route có định danh mang `orderId`, `purpose`, `sessionId`. Gọi `GET /payment/orders/{orderId}/result?purpose=4&sessionId={sessionId}` với purpose đọc từ URL. `status=Succeeded` nghĩa là lần thu đã ghi nhận. `summary.outstanding` và `summary.settled` cho biết toàn đơn còn phải trả hay không. `allocations[]` là phần tiền và điểm đã ghi của phiên. `resultPage` rỗng khi chưa có trang kết quả cuối. `summary.returnLockerId` lấy trạm nhận từ yêu cầu hiện tại hoặc đơn. `summary.canRequestStorageReturn` và `storageReturnReason` hỗ trợ hiển thị ở màn hình kho. API kho kiểm tra lại tại bước ghi.
