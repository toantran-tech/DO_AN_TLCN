# Customer guest access — bàn giao BE ngày 15/11/2026

## Trạng thái

Code đã triển khai; chưa chạy test/build local, chưa áp migration hoặc deploy staging đợt này. Theo yêu cầu người dùng, test để CI PR và Docker do người dùng thực hiện. Không ghi PASS cho HTTP/runtime chưa thực thi. Scope yêu cầu: [BACKEND_CUSTOMER (1).md](<BACKEND_CUSTOMER (1).md>).

## API không bắt buộc Authorization

| API | Hợp đồng |
|---|---|
| POST /customer/services/filter | DTO/bộ lọc/sort/phân trang cũ; chỉ dịch vụ Approved + Active + chưa xóa + UnitPrice > 0. |
| GET /customer/services/{id} | Một dịch vụ được phép bán; không hợp lệ trả 404. |
| GET /customer/lockers | city/district/keyword/page/take/latitude/longitude; chỉ tủ active có Merchant active phục vụ và còn ô khả dụng; có vị trí thì sort khoảng cách gần nhất. |
| GET /customer/lockers/{id} | Một tủ được phép hiển thị; slots chỉ id/slotNumber/size/status, availableSlots tương thích. Tủ hết ô vẫn đọc detail được; không có PIN/QR/đơn/người gửi. |
| GET /customer/reviews | page=1, take=4 mặc định, take 1–20, stars 1–5; tổng tính sau lọc ở DB. |
| GET /customer/reviews/summary | Tổng/averageStars/distribution theo cùng phạm vi phát hành, không nhận query filter. |
| GET /customer/reviews/{publicId}/images/{index} | Bytes ảnh, không redirect blob/storage; kiểm tra lại phạm vi duyệt, hidden/deleted và order ở mỗi lần; không cần JWT để xem ảnh đã duyệt Guest. |

Không gửi token cũng gọi được. Nếu có token Customer, list/summary/ảnh vẫn giữ tập dữ liệu đã duyệt Customer; Guest và role khác chỉ thấy tập duyệt Guest. FE không cache chung kết quả hai phạm vi hoặc dùng response từ phiên đăng nhập trước sau logout. Review responses có Cache-Control=no-store; ảnh thêm nosniff.

Không mở /customer/me, tạo/sửa đơn, hồ sơ, thanh toán, khiếu nại, thông báo, upload hoặc policy acknowledge. Guest gọi API riêng tư vẫn 401; policy/current giữ public cũ. Yêu cầu toast, điều hướng login rồi quay lại chức năng là trách nhiệm FE, không thêm redirect HTML ở BE.

## Ví dụ catalog không token

POST /customer/services/filter, Content-Type application/json:

```json
{
  "keyword": "giặt",
  "serviceTypes": [1, 2],
  "priceRanges": [
    {"minPrice": 30000, "maxPrice": 50000},
    {"minPrice": 90000, "maxPrice": 100000}
  ],
  "sortBy": [{"field": "UnitPrice", "isDescending": false}],
  "page": 1,
  "take": 20
}
```

OR giữa các loại, OR giữa các khoảng, AND giữa loại/giá/keyword. Giá gồm cả hai đầu. minPrice/maxPrice là thuộc tính của **phần tử priceRanges**, không đặt top-level; giữ hợp đồng hiện hành và reject thuộc tính JSON không hỗ trợ. Không gửi filter hoặc mảng rỗng/null = không lọc tiêu chí đó. Keyword tìm Name/Code không phân biệt hoa thường. Sort Name|UnitPrice (price alias), thêm Id để phân trang ổn định.

Catalog page clamp 1–100000, take 1–100; keyword ≤200 ký tự. ServiceTypes/PriceRanges tối đa 20 phần tử, sort tối đa 5; invalid enum, giá âm, khoảng ngược hoặc sort lạ trả 400. Locker city/district ≤100 ký tự; tọa độ phải đủ cả hai, hữu hạn và đúng phạm vi. Không có token không thay đổi điều kiện bán/hiển thị dữ liệu.

## Admin phê duyệt phạm vi Guest

Giữ POST /rating/{internalRatingId}/publish, AdminOnly. Sau khi kiểm tra nhận xét và từng ảnh/video không có PII, dùng version từ detail Admin:

```json
{
  "publishForGuests": true,
  "expectedVersion": "<version từ GET /rating/{id}>",
  "operationId": "<UUID mới>",
  "reason": "Đã kiểm tra nội dung và ảnh cho Home công khai"
}
```

- publishForGuests=true cấp Guest + Customer + Merchant. Lưu GuestApprovedAt/GuestApprovedBy và PublicId ngẫu nhiên; không dùng internal rating/order/customer ID làm public ID.
- publishForCustomers=true chỉ giữ phạm vi Customer/Merchant trước đây; **không tự cấp Guest**. Mặc định hai flag false giữ Merchant-only. False không phải lệnh thu hồi quyền.
- Rating DTO Admin thêm canPublishForGuests và guestApprovedAt. GuestApprovedBy chỉ lưu DB/audit, không phát trong testimonial DTO.
- operationId replay cùng request không nhân đôi phê duyệt hoặc thay mốc thời gian; cùng operationId khác Guest flag trả 409. Giữ payload/version tương thích review cũ chưa có Guest approval.
- Hide làm list/summary không còn review, URL ảnh cũ trả 404 ngay. Show chỉ khôi phục phạm vi đã được duyệt, không tự cấp Guest. Xóa review/order hoặc order chưa Completed cũng không phát hành.
- Existing Customer/Merchant-only reviews sau migration **không tự xuất hiện trên Home Guest**. Nếu Home Guest rỗng, Admin phải kiểm tra và cấp publishForGuests, không dùng seed/hard-code để lấp dữ liệu.

Guest DTO vẫn chỉ id (public), overallStars, comment, imageUrls, publishedAt; không customer/order/merchant identity, contacts, mã đơn, PIN/QR hoặc owner/blob URL. Hệ thống không tự sửa văn bản tự do để coi là đã kiểm duyệt; Admin chịu trách nhiệm xác nhận không có PII trước khi cấp phạm vi Guest.

## Rate limit

Bảy API đọc dùng public-catalog: mặc định 120 request/phút/IP kết nối, không queue, quá hạn mức trả 429. Cho phép điều chỉnh PublicCatalog__PermitLimit trong 1–1000; CI isolated override 1000 vì mọi suite chung loopback IP. Không dùng cấu hình CI cho staging. Render có reverse proxy nên RemoteIpAddress có thể là IP proxy; không tự tin X-Forwarded-For do client gửi để né giới hạn. Nếu mở UAT đông người, cần cấu hình proxy tin cậy riêng trước khi kỳ vọng quota theo IP end-user.

## Migration và kiểm chứng

`20261115010000_AddGuestReviewApproval` chỉ thêm hai cột nullable guest_approved_at/guest_approved_by vào order_ratings, không backfill/reset scope hoặc xóa dữ liệu. Snapshot/Designer đi kèm. CI compose chạy DB tạm và áp migration khi Host khởi động; deployment cũng dùng migration startup hiện có. Xem migration log, không dùng Swagger hiện được làm bằng chứng DB đã áp thành công.

Ba suite hiện đã có trong tests/ci/Run-ApiSmoke.ps1 được cập nhật:

- customer-service-filter.ps1: anonymous + Customer cùng catalog, sort/ranges/paging/case-insensitive, loại dịch vụ ẩn/chưa duyệt/xóa và input giới hạn.
- customer-locker-slots.ps1: Guest/detail full locker, metadata allowlist, phân trang/coordinates, me vẫn 401.
- customer-public-reviews-notifications.ps1: phân biệt Customer-only với Guest approval, DTO, anonymous image, summary, Hide/Show/delete, replay/mismatch và private endpoints 401; giữ kiểm tra notification privacy cũ.

Chỉ kiểm tra syntax PowerShell và git diff whitespace local, chưa chạy HTTP/build/tests hay Docker. Sau PR CI xanh, deploy đúng branch Render, đợi migration thành công rồi test không Authorization tại staging. FE có thể dùng img src cho ảnh đã phát hành Guest; Customer-only image vẫn cần fetch kèm token.
