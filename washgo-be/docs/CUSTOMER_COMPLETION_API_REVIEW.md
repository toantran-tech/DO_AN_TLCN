# Bàn giao Customer bổ sung — 14/11/2026

Phạm vi: ba mục BACKEND_CUSTOMER.md. JWT Customer cho toàn bộ `/customer/...`; không dùng API Admin/partner thay Customer.

## 1. Chi tiết một locker

GET `/customer/lockers/{id}` vẫn trả **một** locker theo id. `data.slots` gồm mọi ô chưa xóa, chỉ id/slotNumber/size/status, sắp slotNumber rồi ID; status 1 Available, 2 Occupied, 3 Reserved, 4 Maintenance. `data.availableSlots` giữ shape cũ (id/slotNumber/size); count lấy từ cùng lần đọc. Hết ô vẫn 200 với availableSlots=[]/count=0. Locker ngừng/xóa/không có Merchant hoạt động vẫn 404.

List `/customer/lockers` tiếp tục tìm locker còn ô trống; `slots` chỉ được nạp ở detail. Không chọn ô khác status=1, nhưng BE vẫn kiểm tra/giữ ô atomically khi tạo đơn vì trạng thái có thể thay đổi sau lúc đọc.

## 2. Feedback WashGo cho Customer

GET `/customer/reviews?page=1&take=4&stars=5`: page từ 1, take 1–20, stars tùy chọn 1–5. Không truyền serviceId hoặc bộ lọc khác; chưa hỗ trợ thì trả 400. `publishedAt` giảm dần, public ID phụ tăng dần. Response:

```json
{
  "succeeded": true,
  "data": {
    "items": [{"id":"<public UUID>","overallStars":5,"comment":"...","imageUrls":[],"publishedAt":"<UTC>"}],
    "total":1,"page":1,"take":4,"hasMore":false
  }
}
```

GET `/customer/reviews/summary` không nhận bộ lọc: data có total, averageStars (null khi rỗng), distribution đủ 5 phần tử {stars,count,rate}. Rate là phần trăm 0–100, làm tròn 2 chữ số; averageStars cũng làm tròn 2 chữ số. Tính toàn tập được phát hành, không dựa vào trang đang xem. List và total đọc cùng snapshot; các request độc lập có thể thay đổi khi Admin đang kiểm duyệt.

Điều kiện chung list/summary/media: review chưa xóa, không Hide, đã duyệt nội dung Merchant và đã duyệt riêng cho Customer, public ID hợp lệ, 1–5 sao, đơn gốc chưa xóa/đã Completed. Không có tên/avatar/contact/CustomerId/OrderId/OrderCode/nhân sự/vị trí. FE gắn nhãn “Khách hàng WashGo”. Không trả các tiêu chí sao nhân sự và không đưa video vào API này.

### Admin duyệt rõ phạm vi

POST `/rating/{id}/publish` bổ sung `publishForCustomers`:

```json
{"expectedVersion":"<version mới nhất>","operationId":"<UUID mới>","reason":"Đã kiểm tra nội dung và ảnh trước phát hành Customer","publishForCustomers":true}
```

Mặc định false giữ hành vi cũ cho Merchant. Đánh giá đã duyệt trước đây **không tự trở thành testimonial**. Admin DTO bổ sung canPublishForCustomers/publicApprovedAt. FE Admin cần hiển thị hành động/checkbox duyệt cho Customer, yêu cầu người duyệt kiểm tra PII trong comment/ảnh; BE không khẳng định tự phát hiện hết PII bằng regex. Chỉ flag true mới cấp public ID ngẫu nhiên độc lập và mốc publicApprovedAt; Publish cũ/replay false vẫn tương thích payload cũ.

Hide loại khỏi list, summary và endpoint ảnh ngay ở request kế tiếp. Show khôi phục nếu đã được duyệt; không đặt lại publishedAt hoặc cấp ID mới. Không sửa điểm/aggregate chỉ vì duyệt Customer. Batch Publish hiện vẫn Merchant-only; public approval dùng endpoint đơn lẻ với flag rõ ràng.

### Ảnh không lộ owner

imageUrls là URL tương đối `/customer/reviews/{publicId}/images/{index}` (index từ 0), không phải `/uploads/{CustomerId}/...`. FE **fetch với Bearer JWT rồi tạo blob URL** để gắn vào ảnh; `<img src>` trực tiếp không tự gửi JWT. Endpoint trả bytes, không redirect và không lộ path/owner; Cache-Control no-store, nosniff, kiểm tra lại điều kiện phát hành mỗi request. Chỉ ảnh owned JPG/PNG/WEBP hợp lệ còn tồn tại trong storage, tối đa 5 MB; ảnh lỗi/mất/URL ngoài không phát hành. Public ID hoặc index sai, review bị Hide/xóa trả 404.

Không thể thu hồi bản ảnh/text người dùng đã tải hoặc chụp màn hình trước Hide. Phát hành cần được nhóm duyệt theo chính sách thực tế.

## 3. Tìm thông báo an toàn

POST `/notification/filter` giữ nguyên body/response hiện có. Customer được tìm title **và content hiển thị được phép**, kể cả thông báo đơn hàng. Biểu thức query SQL dùng cùng quy tắc nội dung với inbox/mark-read/SignalR, trước count/sort/pagination. Legacy lưu kho có địa chỉ nội bộ được thay bằng nội dung WashGo chung trước khi so khớp; tìm chuỗi bị che không tạo kết quả hoặc làm lộ qua total. Không sửa raw lịch sử hoặc tải cả inbox về client để tìm.

Giữ scope UserId, groups/types/unread/date và keyword tối đa 100 ký tự. Total là kết quả sau lọc; counts/unreadTotal vẫn là thống kê toàn inbox như hợp đồng cũ để số tab không nhảy theo keyword. Staff giữ phạm vi tìm nội dung được phép của họ. Nội dung lưu kho hướng dẫn yêu cầu giao lại locker.

## Migration và kiểm thử

Migration `AddPublicCustomerReviewApproval` thêm 3 cột nullable và index trên order_ratings; không auto approve/backfill public cho review cũ. Backup trước deploy, áp migration trước khi dùng app mới; không sửa tiền, đơn hoặc đánh giá cũ để làm dữ liệu Home mẫu.

Smoke `customer-locker-slots.ps1` và `customer-public-reviews-notifications.ps1` được nối CI. Fixture chỉ dùng DB tạm/CI, Mock payment và không SMTP thật; không chạy script có SQL lên production. Kết quả và trạng thái Docker chính xem IMPLEMENTATION_CHECKLIST.md.
