# Phối hợp Backend — Merchant

Rà soát 07/11/2026 bằng Swagger đang chạy tại localhost:5000 và source BE. Chỉ thay đổi FE, không sửa BE/env/DB.

## Đã có API, đã ghép FE ở lượt dashboard

| Phần | API / phạm vi | FE |
| --- | --- | --- |
| Danh tính | GET /auth/me, JWT Merchant | Avatar từ avatar, fullName/email, dropdown và logout dùng auth chung. |
| Cửa hàng | GET /merchant-profile, MerchantOnly | Tên cửa hàng trên dashboard, trang /merchant/profile và modal gửi hồ sơ cập nhật chờ duyệt. Không dùng GET /merchant/{id} vì endpoint đó thuộc Admin. |
| Đơn được giao | POST /wash-order/filter, OrderStatusUpdater | BE ApplyScope ép MerchantId = CurrentUser.Id; FE không gửi merchantId/customerId. Tổng từ response.total cho toàn bộ kết quả, không đếm một trang. Đọc tổng từng status bằng take1, danh sách mới nhất take5. |
| Chi tiết đơn | GET /wash-order/{id}, BE kiểm tra Merchant sở hữu | Modal chỉ đọc mã đơn, locker/ô, dịch vụ/số lượng/tổng tiền; title tab có mã đơn. Không hiện thông tin Customer đã redact/PIN/QR mở tủ. Chưa triển khai cập nhật trạng thái/cân/điều phối ở lượt này. |
| Đối soát | POST /finance/settlements/filter, MerchantOrAdmin | BE giới hạn theo Merchant hiện tại. Hiển thị4 kỳ mới nhất, periodFrom/periodTo/payoutAmount/status. Không gọi tạo kỳ hoặc confirm-paid của Admin. |
| Thông báo | /notification và SignalR /hubs/notifications, theo JWT user | Dùng chung provider/API/inbox/bell/icon với Customer, thêm icon enum Merchant. Link order đi /merchant?order=UUID để mở modal riêng; không điều hướng sang Customer. Type chưa có màn chi tiết chỉ hiện nội dung/đọc/xóa. |

## Thiếu API dashboard / báo cáo cho Merchant

- Chưa thấy endpoint dashboard tổng hợp cho Merchant. FE hiện ghép tổng trạng thái bằng các truy vấn POST /wash-order/filter (14 status +1 trang mới nhất); nguồn dữ liệu là BE và áp dụng scope trước phân trang, nhưng các truy vấn không cùng một snapshot và có thể lệch tạm thời khi đơn đổi trạng thái. Đề nghị GET/POST /merchant/dashboard trả tổng, counts theo status, đơn mới nhất và generatedAt trong một response nhất quán.
- POST /finance/revenue-report, /finance/revenue-report/export và /finance/commissions hiện **AdminOnly**. Merchant không có API báo cáo doanh thu theo ngày/7ngày, so sánh hôm qua/kỳ trước, target doanh thu hoặc thống kê tăng trưởng như UI_TLCN trang16/21. Không dùng token Admin, không cộng FinalAmount của một trang hoặc lấy payout làm doanh thu. FE hiện có chart trạng thái (bar + donut) và kỳ đối soát; chưa dựng chart doanh thu giả.
- Đề nghị endpoint báo cáo riêng Merchant, luôn lấy identity từ JWT: khoảng from/to, timezone Asia/Ho_Chi_Minh, ngày/tuần/tháng, daily series, doanh thu/hoàn/phí/hoa hồng/thực nhận có định nghĩa rõ, số đơn và kỳ so sánh. Thống nhất mốc ngày (tạo/hoàn thành/thanh toán) và cách xử lý refund/Cancelled/đối soát; không để FE tự suy ra.
- Chưa thấy summary cảnh báo quá hạn SLA cho Merchant có tổng số toàn bộ/phân loại/thời điểm hạn như panel “Overdue Attention” trong PDF. BE đã có worker và thông báo SLA; đó không phải API tổng hợp dashboard. Đề nghị trả canhBao counts và danh sách ưu tiên cùng deadline/serverNow. Không tự coi ETA đã qua là vi phạm SLA hoặc chỉ lọc một trang rồi báo tổng.

## FE còn làm tiếp, không phải BE thiếu

- Quản lý đơn: đã có filter/detail/status/actual-weight và các luồng vận hành BE. Lượt này chỉ dashboard + xem đơn, chưa cân/chuyển trạng thái/điều phối; navbar quản lý tạm disabled.
- Shipper/locker/service/report: nhiều endpoint đã có theo role, cần đọc hợp đồng khi làm từng feature. Sidebar chưa triển khai không đồng nghĩa BE thiếu các phần này. Hồ sơ đã có trang xem và modal gửi yêu cầu cập nhật qua request Admin duyệt, không PUT trực tiếp.
- Chi tiết incident/service proposal/profile request từ thông báo sẽ nối vào màn Merchant tương ứng ở feature sau; không mở route Customer hoặc dựng link hỏng.

## Giới hạn thông báo dùng chung

- Đọc/xóa ở thiết bị khác chưa có event invalidation; focus/refresh để reconcile. Nội dung title/content BE hiện tiếng Việt, UI có VI/EN. Giữ nguyên payload nghiệp vụ, không đoán bản dịch.
- Kiểm tra cấu hình proxy WebSocket khi triển khai. Không gọi /notification/reminders/run (AdminOnly) từ Merchant.

## Kiểm thử

- Đã đọc Swagger/schema và DTO/service/source phân quyền, kiểm thử API layer bằng mock response thật về cấu trúc. E2E dùng dữ liệu Merchant giả lập, chặn endpoint Admin và mọi thao tác ghi nghiệp vụ. Chưa đăng nhập Merchant thật để kiểm chứng số liệu DB hoặc push SignalR end-to-end; không tạo tài khoản, thay vai trò, sửa DB hoặc lấy credential trong env.

## Ghi chú hồ sơ và đánh giá — 07/11/2026

### Chọn địa chỉ bản đồ và thành phố bắt buộc (07/11/2026)

- FE đã dùng chung AddressPickerDialog/AddressMap với Customer: bấm điểm/chọn tâm bản đồ, xem trước, Áp dụng mới điền địa chỉ + tỉnh/thành phố; Hủy không đổi form, không tự gửi hồ sơ. Chỉ hỏi quyền vị trí khi bấm Dùng vị trí của tôi; không gửi Bearer token cho geocoder. Các thành phần và API bản đồ chuyển sang thư mục dùng chung, không sao chép logic cho Merchant.
- City đã thành trường nhập bắt buộc, trim 1–100 ký tự. **BE chưa có City trong MerchantProfileUpdateCommand/Proposed/Current/ApproveAsync.** FE hiện cho chọn/nhập để chuẩn bị, nhưng nếu city khác hồ sơ đã lưu thì báo chưa hỗ trợ và không POST/upload; không gửi một trường BE bỏ qua rồi báo thành công. Các sửa đổi được giữ trong modal. Khi city không đổi, các trường API đang hỗ trợ vẫn gửi duyệt bình thường.
- **BE cần thêm** City vào command/DTO proposed-current/Changes/ApproveAsync, validate bắt buộc và tối đa 100 ký tự, migration nếu cần. Sau khi cập nhật hợp đồng, FE mới bỏ chặn đổi city và đưa City vào payload whitelist; không coi UI hiện tại đã lưu được thay đổi này.
- **BE cần hỗ trợ tọa độ khi duyệt đổi địa chỉ:** Latitude/Longitude hiện chỉ có trên DTO xem, chưa có trong request sửa. Thống nhất việc nhận cặp tọa độ hợp lệ, đối chiếu địa chỉ/thành phố và áp dụng đồng thời khi duyệt; tránh địa chỉ mới nhưng vị trí điều phối cũ. Hiện FE chỉ dùng điểm chọn trong bộ nhớ để mở lại bản đồ, không tuyên bố đã cập nhật tọa độ cửa hàng.

### Hoa hồng theo đơn — cần BE bổ sung

- Đã kiểm tra source: Merchant.CommissionRate là cấu hình hiện tại; WashOrder chưa có tỷ lệ hoa hồng được lưu theo đơn. FinanceAppService.BuildCommissionsAsync đang cộng tiền thanh toán/hoàn trong kỳ theo Merchant rồi nhân **merchant.CommissionRate hiện tại**. Do đó đổi cấu hình có thể làm đổi báo cáo kỳ cũ chưa chốt. CreateSettlementAsync đã lưu CommissionRate/CommissionAmount/PayoutAmount vào MerchantSettlement khi tạo, nhưng đó là snapshot của cả kỳ, chưa bảo vệ từng đơn trước khi chốt kỳ.
- **Đề xuất cần BE triển khai:** lưu CommissionRateApplied trên đơn khi Merchant được giao/xác nhận xử lý theo mốc chính sách được chốt. Tỷ lệ lấy từ BE, không nhận tỷ lệ do Merchant/FE gửi; 20 nghĩa là 20%, decimal có precision rõ ràng, hợp lệ 0–100. Đổi Merchant.CommissionRate chỉ áp dụng cho các đơn mới sau mốc có hiệu lực. Đơn cũ không lấy lại cấu hình hiện tại khi đọc/tính báo cáo.
- Cần lưu/định nghĩa CommissionBaseAmount, CommissionAmount và MerchantNetAmount theo từng đơn hoặc sổ giao dịch, cùng thời điểm/version chính sách áp dụng để audit. Tỷ lệ được cố định trước khi xử lý; cơ sở tính/số tiền chỉ chốt khi có giá và thanh toán hợp lệ. Quy định rõ chiết khấu, phụ phí khối lượng, phí hủy/lưu kho có thuộc cơ sở tính không và quy tắc làm tròn VND; FE không tự suy ra từ FinalAmount.
- Sửa BuildCommissionsAsync để tính/cộng từng đơn theo tỷ lệ snapshot, không nhân một tỷ lệ hiện tại cho cả kỳ. Một kỳ có thể chứa nhiều tỷ lệ; trả breakdown theo đơn/tỷ lệ, không gán một CommissionRate chung gây hiểu nhầm. Lưu chi tiết đối soát gắn đơn/transaction và giữ số đã chốt, không ghi đè khi sửa cấu hình Merchant.
- Hoàn tiền/hoàn một phần phải bám đơn và tỷ lệ gốc, tạo điều chỉnh có audit, tránh dùng tỷ lệ mới hoặc trừ tổng refund không phân biệt đơn. Phân công lại Merchant cần chính sách rõ trước khi chốt snapshot; sau khi đã phát sinh/chốt tài chính không âm thầm thay tỷ lệ/đơn vị hưởng.
- DTO chi tiết/danh sách đơn cho Merchant và Admin cần trả tỷ lệ áp dụng, cơ sở tính, hoa hồng, thực nhận, trạng thái tạm tính/đã chốt/điều chỉnh. Scope theo JWT cho Merchant. Khi chưa có snapshot thì hiện Chưa có dữ liệu lịch sử, không mượn Merchant.CommissionRate hiện tại để giả làm tỷ lệ cũ.
- Dữ liệu lịch sử: chỉ backfill từ hợp đồng/lịch sử cấu hình/tài chính có chứng cứ; không điền hàng loạt tỷ lệ hiện tại. Chưa xác định thì null + đánh dấu cần đối chiếu. Test BE: đổi 20% thành 25% giữa kỳ, đơn cũ giữ 20%, đơn mới 25%, tổng đối soát cộng theo từng đơn; thêm test hoàn tiền, đổi Merchant, kỳ đã chốt, tính idempotent và phân quyền. FE chưa triển khai hiển thị hoa hồng từng đơn vì API chưa có dữ liệu này; không sửa BE/DB trong lượt này.

### Trang hồ sơ và gửi duyệt

- City từ MerchantProfileDto đã hiển thị ở trang hồ sơ; modal nay cho nhập/chọn bản đồ và bắt buộc, nhưng chặn gửi khi city thay đổi cho đến khi BE hỗ trợ command duyệt (chi tiết ở trên).
- **Chưa làm phía BE — ngày tham gia:** Merchant có CreatedAt nhưng MerchantProfileDto chưa trả ngày tham gia. Đề nghị xác định mốc nghiệp vụ (tạo hồ sơ hay kích hoạt đối tác) và trả joinedAt trong GET /merchant-profile. FE đã có hàng Ngày tham gia, hiện Chưa cập nhật khi thiếu; không thay bằng ContractStartDate hoặc ngày tạo request.

- Hồ sơ nay là `/merchant/profile`, mở từ dropdown tài khoản. Thẻ tổng doanh thu đã dựng UI, hiện `— / Chưa cập nhật` vì chưa có API tổng doanh thu dành cho Merchant. Không lấy tổng một trang đối soát làm doanh thu hoặc hiển thị số mẫu.
- POST `/merchant-profile/update-requests` đã có: tên, địa chỉ, điện thoại, giờ mở cửa, imageUrls; Admin phê duyệt mới áp dụng. FE nối modal gửi duyệt, khóa khi đang chờ duyệt/tài khoản không Active, chặn gửi lặp và kiểm tra lại hồ sơ sau kết quả không rõ. Không tự gọi approve/reject.
- Source BE đã có bảng/entity MerchantImage, ImageUrls trong MerchantProfileDto và cơ chế ReplaceStoreImagesAsync khi duyệt. Upload `/files/images` yêu cầu đăng nhập, không giới hạn Customer; ảnh mới phải thuộc Merchant hiện tại. FE dùng chung upload/hook với Customer, tối đa 5 ảnh JPG/PNG/WEBP, 5 MB/ảnh. Cần xác nhận migration đã áp dụng trên DB môi trường triển khai; không sửa DB trong lượt FE này.
- Mã số thuế có trong DTO xem nhưng chưa nằm trong command cập nhật; giấy phép chưa được trả trong MerchantProfileDto/command. FE hiển thị Chưa cập nhật khi thiếu; modal thông báo liên hệ Admin cho hai trường này. Đề nghị BE bổ sung quy trình cập nhật/duyệt giấy phép và mã số thuế nếu Merchant được tự gửi thay đổi.
- Giờ mở cửa đã hỗ trợ, FE giải mã JSON string BE lưu để tránh dấu ngoặc kép xuất hiện trong trường nhập.

### Điểm đánh giá cửa hàng

- Hồ sơ dùng Rating, TotalOrders, ImageUrls, Status và các trường liên hệ/thuế/giờ mở cửa của GET `/merchant-profile`; dữ liệu thiếu hiển thị Chưa cập nhật. Thẻ doanh thu là placeholder chờ API; không dựng QR cửa hàng, giấy phép hay cấu hình vận hành từ dữ liệu giả.
- RatingAppService.CreateAsync gắn đánh giá với order.MerchantId. RefreshMerchantRatingAsync lấy trung bình OverallStars của đánh giá không bị ẩn theo MerchantId, làm tròn một chữ số. Đây là đánh giá trải nghiệm đơn hàng, tổng hợp thành điểm cửa hàng; không phải điểm riêng từng dịch vụ hoặc toàn WashGo.
- BE hiện mặc định Rating = 5 khi không có đánh giá công khai; MerchantProfileDto chưa trả số lượt đánh giá. FE hiển thị điểm được trả về và không suy diễn số lượt. Đề nghị BE thêm publicRatingCount để phân biệt chưa có đánh giá với điểm 5 thực tế.

