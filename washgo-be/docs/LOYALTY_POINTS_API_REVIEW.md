# Điểm thưởng WashGo — bàn giao Backend

Ngày: 15/11/2026. Đã triển khai source; **chưa chạy HTTP/smoke, chưa áp migration hoặc build Docker/deploy trong đợt này**. Không coi tài liệu này là bằng chứng kiểm thử đã PASS.

Sửa theo lượt test local: request chưa đăng nhập tới balance từng trả 404 vì thiếu đăng ký `LoyaltyHandler` trong `WashGoHttpApiModule`. Đã thêm đăng ký và gate kiểm tra đủ bốn route loyalty/config trong Swagger trước khi chạy smoke. Giữ nguyên kỳ vọng 401/403; chờ chạy lại, chưa xác nhận toàn bộ suite PASS.

## Quy tắc đã chốt

- 1 điểm = 1đ; mặc định tích 1% tiền giặt thực trả qua cổng, làm tròn xuống điểm nguyên.
- Cộng ngay khi BE xác nhận thanh toán hợp lệ, không chờ Completed. FE chuyển trang thành công không phải bằng chứng đã trả tiền.
- Voucher trừ trước, sau đó dùng điểm tối đa 20% tiền giặt sau voucher, không vượt số dư khả dụng.
- Không tích/dùng điểm cho phí hủy, phí quá hạn/lưu kho, khoản thu dư hoặc bồi thường. Không tích cho phần trả bằng điểm.
- WashGo chịu chi phí giảm bằng điểm. Merchant được đối soát giá trị tiền giặt trước phần giảm bằng điểm, hoa hồng theo snapshot hiện hành.
- Chưa hết hạn, chuyển nhượng, rút tiền hoặc cộng thưởng bù cho các giao dịch cũ. Số điểm cũ có sẵn được giữ lại thành số dư đầu kỳ khi mở ví lần đầu.
- Hoàn tiền: phần tiền hoàn bằng tiền, phần điểm hoàn bằng điểm; thu hồi thưởng tương ứng tiền giặt thực trả đã hoàn. Nếu thưởng đã tiêu thì ghi nợ điểm, bù từ điểm kiếm sau này; vẫn cho mua đơn mới bằng tiền.
- Phí hủy trừ trước, phần giá trị được hoàn chia theo tỷ lệ tiền/điểm gốc. Làm tròn điểm nguyên, phần lẻ vào tiền; giới hạn để tiền hoàn không vượt tiền đã thu, điểm hoàn không vượt điểm đã dùng.

Ví dụ: giá sau voucher 180.000đ, dùng 36.000 điểm → cổng thu 144.000đ → thưởng 1.440 điểm. Merchant có doanh thu đối soát 180.000đ; nếu hoa hồng 20% thì hoa hồng 36.000đ và payout 144.000đ. WashGo tài trợ 36.000đ điểm. Báo cáo tiền cổng vẫn chỉ ghi 144.000đ.

## API dành cho FE Customer

Tất cả yêu cầu JWT Customer. Không nhận customerId từ client; chỉ tra hồ sơ/đơn của người đang đăng nhập. Response dùng BaseResponse/collection envelope hiện hành. Các route điểm trả `Cache-Control: no-store`.

### GET `/customer/loyalty/balance`

`data` gồm:

| Field | Ý nghĩa |
| --- | --- |
| totalPoints | Phần số dư dương; không bao gồm cách biểu diễn nợ âm |
| heldPoints | Điểm đã giữ cho các phiên thanh toán còn hiệu lực/chưa rõ kết quả |
| availablePoints | Điểm có thể dùng, tối thiểu 0 |
| debtPoints | Nợ điểm do thu hồi thưởng; không phải nợ tiền mặt |
| vndPerPoint | Luôn 1 |
| earnPercent / maxUsePercent | Chính sách áp dụng cho phiên mới |

Ví dụ: tổng 50.000, giữ 36.000 → khả dụng 14.000. Nợ 5.000 → totalPoints=0, availablePoints=0, debtPoints=5000. Có thể vừa có nợ vừa có điểm giữ từ trước; không cho giữ thêm điểm.

`GET /customer/me` giữ trường `loyaltyPoints` tương thích và bổ sung `loyaltyAvailablePoints`, `loyaltyHeldPoints`, `loyaltyDebtPoints`. Trường cũ là int, giới hạn int.MaxValue; API balance/history dùng long và là nguồn đầy đủ. `TotalSpent` hồ sơ vẫn theo cách tính hiện có từ đơn, không dùng làm sổ tích điểm.

### GET `/customer/loyalty/history?page=1&take=20`

page 1–100000, take 1–100. Envelope phân trang hiện có với `list`; sắp CreatedAt giảm dần, Id giảm dần khi cùng thời gian.

Mỗi dòng: id, orderId, kind, pointsDelta, heldDelta, netPointsAfter, heldPointsAfter, createdAt. `netPointsAfter` có thể âm (nợ điểm). Kind: Opening, Hold, Release, Redeem, Earn, RefundPoints, ReverseReward. Dòng zero có thể xuất hiện để lưu dấu sự kiện đã xử lý dù chính sách tích bằng 0.

Không trả mã cổng/URL thanh toán, key sự kiện nội bộ hoặc thông tin Customer khác.

### GET `/customer/loyalty/quote/{orderId}?pointsToRedeem=36000`

Chỉ cho đơn của mình chờ thanh toán tiền giặt (AwaitingPayment hoặc MovedToStorage chưa trả tiền giặt), đã cân/chốt giá nếu có yêu cầu cân.

`data`: orderId, laundryAmount (sau voucher, trước điểm), availablePoints, maximumPoints, pointsToRedeem, cashAmount, expectedEarnedPoints, isEstimate=true.

Đây là báo giá, chưa giữ điểm. FE phải gửi lại số điểm khi tạo phiên. Quote không cộng lại điểm đang bị giữ cho phiên cũ vào số dư khả dụng; nếu đã có URL thì tiếp tục phiên đó, không dùng quote để thay đổi phiên đang hiệu lực.

### POST `/payment/create-url`

```json
{
  "orderId": "<GUID đơn>",
  "paymentMethod": 2,
  "pointsToRedeem": 36000
}
```

paymentMethod=2 VNPay, 3 MoMo. `pointsToRedeem` là số nguyên không âm, bỏ qua mặc định 0. BE kiểm tra lại số dư/giới hạn dưới khóa ví và giữ điểm trong transaction tạo phiên.

Response thêm `laundryAmount`, `pointsToRedeem`, `pointsHeld`, `pointsEarned`. **amount là số tiền cổng thu**, không cộng điểm vào amount. pointsEarned chỉ được ghi sau khi xác nhận thành công; quote có expectedEarnedPoints để hiển thị dự kiến.

- Retry cùng đơn/phương thức/số điểm tái sử dụng phiên hiện tại.
- Khác số điểm: 409 PaymentPointsConflict. Thiếu điểm do đơn khác vừa giữ: 409 InsufficientPoints.
- Phiên chưa rõ kết quả: 409 PaymentVerificationRequired, tiếp tục giữ điểm.
- Thất bại cuối cùng được xác nhận: trả điểm đang giữ; không có thưởng.
- Thành công hợp lệ: trừ điểm giữ và cộng thưởng trong cùng transaction với ghi nhận Paid.
- Thành công đến muộn sau phiên đã đóng hoặc tiền không áp dụng vào đơn: vào đối soát/hoàn tiền dư, không cộng thưởng/tiêu điểm đã giải phóng.
- Không có thanh toán toàn bộ bằng điểm; cấu hình giới hạn dùng tối đa 99% giữ phần tiền cổng dương.

Hai API tạo URL phí không chấp nhận pointsToRedeem khác 0. API phí lưu kho không có trường điểm.

## Hoàn tiền / hủy đơn

- RefundRequest.Amount tiếp tục là **tiền cần hoàn qua ngân hàng/cổng**, không phải tiền + điểm.
- Response thêm pointsAmount và rewardPointsReversed. Yêu cầu hoàn tiền thường dùng Amount tiền mặt; server tính điểm được hoàn theo tỷ lệ lũy kế, không cho client chỉ định điểm.
- Xác nhận hoàn tiền dùng cơ chế Admin review, expectedVersion, operationId và refundReference hiện có. Chỉ sau khi xác nhận, ví mới được hoàn điểm và thu hồi thưởng.
- Hai yêu cầu xác nhận đồng thời hoặc retry không hoàn điểm hai lần.
- Báo phí hủy trả refundAmount tiền mặt và refundPoints riêng. cancellationRefundAmount trên đơn là tiền mặt sau phân bổ; phí được bù bằng giá trị tiền + điểm đã trả.
- Trường hợp làm tròn chỉ còn điểm để hoàn: vẫn tạo yêu cầu Amount=0, pointsAmount>0; Admin xác nhận bằng mã biên nhận thao tác điểm, không chuyển tiền ngân hàng 0đ.
- Không hoàn/đảo điểm đối với RefundRequest.IsExcessPayment.
- Hoàn nhiều lần dùng tổng tiền đã hoàn để tính tổng thưởng cần thu hồi, tránh mất phần lẻ. Hoàn điểm không vượt phần đã dùng ban đầu.

## Admin và báo cáo

`GET /system-setting/` thêm loyaltyEarnPercent, loyaltyMaxUsePercent.

`PUT /system-setting/loyalty` (AdminOnly):

```json
{
  "earnPercent": 1,
  "maxUsePercent": 20,
  "expectedVersion": "<version từ GET settings>",
  "operationId": "<GUID mới>"
}
```

Tích 0–100%, dùng 0–99%, tối đa 2 chữ số thập phân. Có audit/version/idempotency như các cấu hình khác. Chỉ thay phiên tạo mới; phiên đang giữ điểm dùng chính sách đã chốt trên PaymentTransaction.

- PaymentTransaction.Amount luôn là tiền cổng. Không sửa số tiền của giao dịch cũ để cộng trợ giá.
- RevenueReport.TotalRevenue và LaundryRevenue tiếp tục tính tiền thật; thêm pointsFunding (điểm sử dụng trừ điểm được hoàn trong kỳ) để theo dõi chi phí tài trợ riêng.
- Commission report và Merchant report lấy tiền + điểm đã tài trợ làm cơ sở đối soát Merchant, trừ tiền + điểm đã hoàn. Tỷ lệ hoa hồng vẫn là snapshot từng giao dịch.
- Commission DTO thêm pointsFunding, platformCommissionAfterPoints = commissionAmount − pointsFunding; đây là phần sau tài trợ điểm, chưa phải lợi nhuận sau mọi chi phí.
- Breakdown đối soát đóng băng pointsFunding ở từng dòng cùng các source ID. netCash = net − pointsFunding. Kỳ đã đóng trước đây giữ nguyên snapshot, không được tính lại.
- Merchant lifetime có pointsFunding, netGatewayCash; các biểu đồ doanh thu Merchant có fundingBasis để phân biệt giá trị dịch vụ với tiền cổng.
- Biên lai giữ các giao dịch tiền thật, thêm redeemedPoints riêng.

## Dữ liệu và concurrency

Migration: `20261115210000_AddLoyaltyPoints` + Designer/snapshot.

- Tạo loyalty_wallets, loyalty_entries; không thay giá trị điểm legacy khi migration chạy. Mở ví lần đầu ghi Opening từ Customer.LoyaltyPoints; nếu legacy âm cần đối chiếu trước, không âm thầm bỏ số âm.
- Snapshot tỷ lệ, số điểm giữ, thưởng/điểm đã hoàn và trạng thái áp dụng trên PaymentTransaction; refund có số điểm trả/thu hồi.
- Khóa theo thứ tự hiện có: payment transaction → order → loyalty wallet; tạo phiên mới khóa order → wallet, không lấy khóa transaction sau order. Không giữ transaction DB trong lúc gọi HTTP cổng.
- Ví khóa advisory theo CustomerId trong PostgreSQL và unique EventKey chống trùng sổ; điểm giữ theo từng PaymentTransaction. Hai đơn không thể cùng giữ vượt số dư khả dụng.
- Đảo thưởng có thể làm NetPoints âm; Customer.LoyaltyPoints tương thích vẫn không âm. Wallet là nguồn đầy đủ, điểm kiếm/hoàn sau đó bù nợ theo sổ.
- Không tự giải phóng điểm vì URL hết giờ local khi chưa chứng minh được cổng không thu tiền; đưa phiên vào luồng đối soát hiện có.
- Không xóa cứng ví/sổ tài chính qua API. Các bản ghi ledger giữ ID nguồn để đối chiếu kể cả khi đơn nguồn bị xóa theo quy trình khác. Down migration chặn nếu đã có bằng chứng điểm.

## Kiểm tra bàn giao

Đã thêm, nối CI nhưng chưa chạy:

- `tests/smoke/Loyalty`: công thức, làm tròn, tỷ lệ hoàn, giới hạn, số dư giữ/nợ.
- `tests/smoke/loyalty-points.ps1`: Customer fixture riêng, legacy opening, quyền, 2 đơn tranh điểm, callback trùng, unknown/failure, tiền đến muộn, hoàn từng phần/xác nhận đồng thời, nợ/thu hồi, phân trang, tiền cổng và đối soát Merchant.
- CI vẫn chạy hồi quy payment/assignment, đơn thường và các suite cũ.

Trước khi merge/deploy: CI xanh; backup DB; áp migration theo quy trình dự án; kiểm tra Swagger các route mới và thanh toán thật trên sandbox. Trong source hiện tại startup có Migrate; vẫn phải kiểm tra log/schema sau deploy, không dựa vào Swagger lên được để kết luận DB đã nâng cấp.

API danh mục Admin không thuộc đợt này, vẫn tạm hoãn.
