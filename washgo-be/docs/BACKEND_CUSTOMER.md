# Customer — yêu cầu BE bổ sung

Đối chiếu source14/11/2026. Không yêu cầu làm lại ảnh catalog/đơn hàng, quote khuyến mãi hoặc tìm locker theo tọa độ: các API đó đã có.

## 1. Trả tất cả ô tủ và trạng thái cho Customer

**Cập nhật 14/11/2026: mục 1 đã sửa code, build Release PASS (0 lỗi, 5 nullable warnings cũ).** Detail trả đúng một locker theo id, có `slots` chỉ metadata/status của mọi ô chưa xóa; giữ shape `availableSlots`. Đọc ô một lần rồi suy ra danh sách/count ô trống. Hết ô vẫn trả 200; quyền/điều kiện xem locker cũ giữ nguyên. List `/customer/lockers` vẫn tìm tủ còn ô trống, `slots` chỉ nạp ở detail. Không migration.

Cập nhật hoàn tất: cả 3 mục đã có code và tài liệu [CUSTOMER_COMPLETION_API_REVIEW.md](CUSTOMER_COMPLETION_API_REVIEW.md). Build/model check và hai smoke HTTP trên DB tạm PASS; suite đã nối CI. Docker image đã build trước khi người dùng yêu cầu để CI/người dùng tự kiểm thử; chưa recreate container hoặc áp migration lên DB chính. Không chạy thêm bộ test rộng local.

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

**Đã triển khai:** list/summary Customer-only và URL ảnh ẩn danh. Admin Publish cần publishForCustomers=true để duyệt rõ phạm vi Customer; review cũ/Merchant-only không tự public. Public ID ngẫu nhiên độc lập, ẩn danh, Hide/Show cập nhật query/media; migration AddPublicCustomerReviewApproval. FE nối section Home theo hợp đồng bàn giao, không tạo nhận xét mẫu.

Hợp đồng đã triển khai (chi tiết/default/error/media xem API review):

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

**Đã triển khai:** POST /notification/filter tìm title/content đã được phép hiển thị cho Customer bằng biểu thức SQL dùng chung quy tắc privacy với DTO. Legacy storage được thay text trước tìm/count/paging, không tìm raw nội dung bị che; giữ UserId và bộ lọc cũ. Không rewrite lịch sử.

- Bổ sung tìm theo nội dung **đã được làm sạch và được phép hiển thị cho Customer**, bao gồm các thông báo đơn hàng. Dùng cùng nguồn nội dung an toàn cho truy vấn, kết quả gợi ý, list và total.
- Không bỏ điều kiện bảo vệ rồi tìm trên raw Content cũ; không để kết quả hoặc số lượng tiết lộ kho/merchant/shipper/PII đang bị ẩn. Giữ giới hạn UserId hiện tại, group/type/unread/date và pagination.
- FE dùng keyword tại API, không tải cả inbox rồi tìm raw content. Total sau lọc, counts/unreadTotal vẫn toàn inbox như cũ. Đã test tìm text nhìn thấy, ẩn raw title/content, scope và bộ lọc trên PostgreSQL tạm.
