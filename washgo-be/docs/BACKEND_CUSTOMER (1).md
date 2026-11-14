> Cập nhật triển khai 15/11/2026: đã bổ sung code mở công khai đúng 7 API đọc, phạm vi duyệt Guest riêng, rate limit và giới hạn đầu vào; các API cá nhân/ghi giữ đăng nhập. Có migration `20261115010000_AddGuestReviewApproval`, không tự public review cũ. Smoke test đã cập nhật trong bộ CI hiện có; chưa chạy test/build/Docker local và chưa áp migration/deploy staging theo yêu cầu người dùng. Bàn giao/JSON/kiểm chứng: [CUSTOMER_GUEST_ACCESS_API_REVIEW.md](CUSTOMER_GUEST_ACCESS_API_REVIEW.md).

# Cần bổ sung BE: khách chưa đăng nhập xem catalog và đánh giá

FE mở Home `/`, `/services`, `/lockers` cho khách. Tìm kiếm, lọc, gợi ý, phân trang, xem chi tiết dùng hợp đồng catalog hiện tại. Section “Khách hàng nói gì về WashGo” cũng hiển thị khi chưa đăng nhập. Tạo đơn/đơn hàng/hồ sơ/thanh toán/khiếu nại/thông báo vẫn bắt buộc đăng nhập, có toast và quay lại chức năng sau khi đăng nhập.

## Quyền đọc catalog

`CustomerHandler.cs` hiện đặt group `/customer` dưới `RequireAuthorization("CustomerOnly")`. Các endpoint catalog/review chưa cho khách đọc; FE mở được trang nhưng API thật có thể trả 401, chưa hiển thị dữ liệu cho khách trước khi BE hỗ trợ.

Mở đọc công khai đúng các endpoint sau, giữ URL/DTO/bộ lọc/sắp xếp/phân trang hiện tại:

- `POST /customer/services/filter`: keyword không phân biệt hoa thường, serviceTypes, priceRanges/minPrice/maxPrice, sortBy, page/take; chỉ dịch vụ được duyệt và đang bán.
- `GET /customer/services/{id}`: chỉ dịch vụ được phép hiển thị catalog.
- `GET /customer/lockers`: keyword, city, tọa độ/khoảng cách, page/take theo hợp đồng hiện tại.
- `GET /customer/lockers/{id}`: thông tin/ảnh công khai, số ô trống và trạng thái ô an toàn; không PIN, QR/mã mở tủ, tài khoản, liên hệ riêng, mã đơn hoặc dữ liệu vận hành nội bộ.

## Quyền đọc đánh giá trên Home

- `GET /customer/reviews`: giữ page/take/stars, tổng và phân trang trên server.
- `GET /customer/reviews/summary`: tổng đánh giá, điểm trung bình và phân bố sao.
- `GET /customer/reviews/{publicId}/images/{index}`: dữ liệu ảnh của đánh giá được phép công khai theo hợp đồng hiện tại.

Chỉ trả đánh giá đã được duyệt và được phép phát hành cho khách chưa đăng nhập theo chính sách công khai phù hợp. Cần xác định phạm vi phát hành công khai nếu quyền PublishForCustomers hiện tại chỉ dành cho người đã đăng nhập. DTO chỉ gồm ID công khai, sao, nội dung đã kiểm tra, thời gian và đường dẫn ảnh an toàn; không lộ danh tính, mã đơn, địa chỉ/liên hệ, đường dẫn upload/blob riêng. Ảnh phải kiểm tra trạng thái phát hành ở từng lần đọc; đánh giá bị ẩn/gỡ không được truy cập qua URL cũ.

Không mở anonymous toàn group `/customer` hoặc API ghi dữ liệu/cá nhân. Các endpoint đọc trên cần xử lý khi không có customerId/JWT, giới hạn page/take/keyword và rate limit. `GET /policy/current` đã public; FE dùng để khách xem điều khoản chỉ đọc, không gọi acknowledge.

Nghiệm thu: không gửi Authorization vẫn đọc được catalog, danh sách đánh giá, summary và ảnh hợp lệ; lọc trước phân trang, tổng chính xác; không truy cập được dữ liệu riêng tư/đã ẩn. API cá nhân/ghi vẫn trả 401 cho khách. FE có kiểm tra trình duyệt bằng API mock, xử lý loading/lỗi/thử lại; không dùng dữ liệu giả khi BE chưa hỗ trợ.
## Kết quả theo yêu cầu

- [x] Anonymous chỉ ở 7 endpoint catalog/review đã liệt kê; không mở toàn group Customer.
- [x] Giữ URL/DTO/phân trang/sort hiện tại. `minPrice/maxPrice` vẫn nằm trong từng phần tử `priceRanges`, không thêm alias top-level đã bị hợp đồng hiện tại loại bỏ. `serviceTypes` giữ dạng mảng.
- [x] Customer có JWT tiếp tục xem tập PublishForCustomers; Guest và role khác chỉ thấy tập Admin PublishForGuests. Mọi tập đều loại hidden/deleted/review của đơn chưa Completed hoặc đơn đã xóa.
- [x] PublishForGuests mặc định false; không backfill phạm vi công khai. True bao gồm duyệt Customer/Merchant, ghi GuestApprovedAt/By, audit, version và fingerprint operationId. Payload/version cũ không bị đổi khi chưa có duyệt Guest.
- [x] List/summary/ảnh review dùng cùng phạm vi phát hành; ảnh trả bytes qua publicId/index, kiểm tra lại mỗi lần, Cache-Control=no-store, không trả đường dẫn storage/owner hay định danh cá nhân.
- [x] Rate limit đọc public 120 request/phút theo RemoteIpAddress (không tin header client tự gửi); page/take có giới hạn, keyword ≤200 ký tự, tối đa 20 loại/20 khoảng giá/5 sort. CI DB riêng tăng quota để suite cùng IP không tự chặn nhau; không đổi default staging.
- [x] Cập nhật ba smoke suite hiện có đã nối Backend CI; thêm anonymous access, phân biệt phạm vi duyệt, replay conflict, Hide/deleted media và giữ 401 cho API riêng tư.
- [ ] PR CI xanh, deploy/apply migration và test trên staging thật.
- [ ] FE xử lý toast/đăng nhập/quay lại chức năng và kiểm thử trình duyệt; ngoài phạm vi BE.
