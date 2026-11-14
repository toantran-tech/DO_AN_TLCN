# Customer — yêu cầu BE còn thiếu

Đối chiếu source14/11/2026. Không yêu cầu làm lại ảnh catalog/đơn hàng, quote khuyến mãi hoặc tìm locker theo tọa độ: các API đó đã có.

## 1. Trả tất cả ô tủ và trạng thái cho Customer

**Cập nhật 14/11/2026: mục 1 đã sửa code, build Release PASS (0 lỗi, 5 nullable warnings cũ).** Detail trả đúng một locker theo id, có `slots` chỉ metadata/status của mọi ô chưa xóa; giữ shape `availableSlots`. Đọc ô một lần rồi suy ra danh sách/count ô trống. Hết ô vẫn trả 200; quyền/điều kiện xem locker cũ giữ nguyên. List `/customer/lockers` vẫn tìm tủ còn ô trống, `slots` chỉ nạp ở detail. Không migration.

Smoke `customer-locker-slots.ps1` đã nối API runner CI; chưa chạy HTTP suite hoặc rebuild Docker chính bản này. Mục 2 và 3 vẫn chưa triển khai.

Bổ sung GET `/customer/lockers/{id}`:

```json
{
  "id": "locker-id",
  "availableSlotCount": 1,
  "slots": [
    { "id": "slot-1", "slotNumber": 1, "size": 2, "status": 1 },
    { "id": "slot-2", "slotNumber": 2, "size": 2, "status": 2 }
  ],
  "availableSlots": [{ "id": "slot-1", "slotNumber": 1, "size": 2 }]
}
```

- `slots` chứa đủ ô chưa xóa, sắp theo slotNumber. Status theo enum đang dùng:1 Available,2 Occupied,3 Reserved,4 Maintenance.
- Giữ `availableSlots` tương thích cho kiểm tra lại ô trước tạo đơn. Customer chỉ chọn ô status1; BE kiểm tra và giữ ô atomically khi tạo đơn.
- Không trả404 chỉ vì hết ô trống nếu locker vẫn được phép xem. Quyền truy cập và điều kiện locker/merchant active vẫn do BE xác nhận.
- Chỉ metadata/trạng thái ô; không thông tin đồ, mã đơn, khách, merchant/shipper, PIN/QR hoặc token người khác.

FE đã đọc `slots` khi có ở chi tiết locker và tạo đơn; khi chưa có chỉ hiện availableSlots thật và thông báo giới hạn.

## 2. Feedback công khai đã kiểm duyệt của dịch vụ WashGo

Hiện chỉ có đánh giá đơn của chính Customer, API Admin và API partner. Chưa có API Customer đọc feedback công khai. Home đã có section **Khách hàng nói về WashGo**, hiện là trạng thái trống, không số sao/nhận xét mẫu.

Cần chốt và bổ sung endpoint Customer-only riêng, đề xuất:

- GET `/customer/reviews?page=1&take=4&stars=5`: sort publishedAt giảm dần rồi id, phân trang server. stars tùy chọn1–5, take tối đa20. Response `succeeded: true, data: { items, total, page, take, hasMore }`.
- Mỗi item: public `id`, `overallStars`1–5, `comment`, `imageUrls`, `publishedAt`; chỉ nội dung/media được duyệt. Public ID không dùng để suy ra đơn/khách.
- GET `/customer/reviews/summary`: `data: { total, averageStars, distribution }`; distribution đủ1–5 với count/rate. averageStars=null khi total0. Tính toàn tập được công khai, không chỉ trang hiện tại.

Điều kiện/riêng tư:

- Chỉ Admin Publish, chưa Hide, còn hợp lệ. Publish/Hide/Show cập nhật public list và summary nhất quán; không trả nội dung chưa duyệt dù chưa Hide.
- Feedback dịch vụ WashGo, không công khai sao merchant/shipper/locker. Không trả tên/email/SĐT/avatar/ID khách, merchant/shipper, orderId/orderCode hoặc vị trí. FE gắn nhãn ẩn danh “Khách hàng WashGo”.
- Comment/ảnh có thể chứa PII: phải qua kiểm duyệt trước public, không chỉ ẩn trường định danh DTO. Media URL an toàn, không lộ quyền truy cập tài nguyên nội bộ.
- Nếu sau này lọc serviceId, BE cần xác định liên kết review đơn với dịch vụ và áp dụng trước pagination/summary; chưa hỗ trợ thì không nhận hoặc giả lọc.

Sau khi BE có API, FE nối section Home với endpoint và bổ sung xem thêm/phân trang theo hợp đồng đã chốt. Không gọi API Admin/partner để phục vụ Customer.

## 3. Tìm nội dung thông báo đơn hàng an toàn cho Customer

`POST /notification/filter` hiện tìm title không phân biệt hoa/thường; content chỉ tìm với thông báo không có `linkType=order` khi role Customer. BE chủ động loại content đơn hàng vì bản legacy có thể chứa địa chỉ kho nội bộ.

- Bổ sung tìm theo nội dung **đã được làm sạch và được phép hiển thị cho Customer**, bao gồm các thông báo đơn hàng. Dùng cùng nguồn nội dung an toàn cho truy vấn, kết quả gợi ý, list và total.
- Không bỏ điều kiện bảo vệ rồi tìm trên raw Content cũ; không để kết quả hoặc số lượng tiết lộ kho/merchant/shipper/PII đang bị ẩn. Giữ giới hạn UserId hiện tại, group/type/unread/date và pagination.
- Chưa có: FE dùng đúng API hiện tại, không tải mọi thông báo rồi tìm raw content ở client. Gợi ý tiêu đề/nội dung được API cho phép và số lượng kết quả đã hoạt động.
