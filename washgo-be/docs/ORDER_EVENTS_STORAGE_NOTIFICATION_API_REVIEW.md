# Bàn giao FE — timeline, mã lưu giữ và thông báo

Ngày 16/11/2026. Thay đổi additive trên các API đơn/storage/notification đang có. Source đã biên dịch; test mới đã nối CI, chưa chạy test/Docker/migration trong đợt này.

## Timeline có sự kiện riêng

Chi tiết đơn tiếp tục trả `timeline`. Thêm `fromStatus`, `eventType`, `sequence`, `basis` và snapshot theo sự kiện: merchantId/merchantName/assignmentMode, shipperId/shipperName/previousShipperId, taskId/taskType, storageCycleId/returnRequestId, paymentPurpose/amount.

- Không suy tên sự kiện chỉ từ `status`: phân công Merchant hoặc Shipper có thể giữ nguyên trạng thái đơn.
- Nhóm event chính: OrderCreated, MerchantAssigned, ShipperAssigned/ShipperReassigned, Deposited, ShipperCollected, ActualWeightConfirmed, StatusChanged, LaundryPaid/CancellationFeePaid/StorageFeePaid, ShipperReturned, Returned, Completed, Cancelled và các event `Storage.*`.
- Mỗi event mới có sequence tăng theo đơn, ghi cùng transaction. API sắp lịch sử legacy trước theo thời gian/id, rồi sự kiện mới theo sequence. FE giữ thứ tự server trả, không tự sort lại chỉ bằng timestamp.
- `basis=Captured`: dữ liệu chụp tại thao tác. `basis=Legacy`, sequence/snapshot null: lịch sử cũ; hiển thị nội dung cũ, không lấy Merchant/Shipper hiện tại để giả lập người phụ trách thời điểm đó.
- Customer không nhận snapshot nhân sự/task/cycle nội bộ; giữ các quy tắc ẩn dữ liệu đã có. Admin có dữ liệu vận hành đầy đủ. Quyền Merchant/Shipper vẫn giới hạn đơn thuộc phạm vi.
- ETA/progress chỉ dùng chuyển trạng thái thực; event phân công hoặc ghi chú không kéo dài thời gian xử lý. Đơn mới chụp ProcessingTypeSnapshot; legacy mới fallback loại xử lý hiện có.

## Lưu giữ tại tiệm Merchant

Không thêm registry kho. Dùng địa chỉ tiệm đã snapshot trên từng vòng lưu giữ.

- Mã hệ thống `LKyyyyMMddHHmmssNNNN`, 20 ký tự, thời gian Việt Nam UTC+7. BE cấp lúc xác nhận thực tế đã vào tiệm (`Stored`), không cấp ngay khi giao nhiệm vụ.
- Có unique reservation trong DB và retry khi trùng mã. Một vòng giữ một mã bất biến; replay không sinh mã mới. Vòng mới có mã mới.
- `receiptReference` ở handover là biên nhận vật lý **tùy chọn**, tối đa 120; không thay thế mã LK. Không yêu cầu Shipper tự nhập LK.
- StorageCycleDto thêm storageReference/referenceIssuedAt/referenceBasis/merchantName/currentLocation/isCurrent. Nhánh quá hạn chưa thanh toán tại tiệm cũng dùng cùng cơ chế vòng lưu giữ/mã LK.
- CurrentLocation: AtShop/InTransit/OutsideShop. Khi đã xuất tiệm, projection order.storageLocation/storageReference được xóa; lịch sử vòng vẫn giữ mã/địa chỉ. FE không hiển thị mã lịch sử như vị trí hiện tại.
- Legacy chưa có mã không giả ngày cấp theo ngày cũ: chỉ cấp khi vòng Stored được cập nhật/xác minh; referenceBasis ghi IssuedForVerifiedExistingCycle và referenceIssuedAt là thời điểm cấp thật. Đọc GET không cấp mã.
- Customer tiếp tục yêu cầu giao lại locker qua flow storage đang có; không hiện địa chỉ kho, mã LK, biên nhận hay thông tin nội bộ để khách tự đến nhận tại kho.

Tiền phí, thời điểm dừng tính khi xuất tiệm, nhận miễn phí mới sau khi vào locker và các bước QR/PIN giữ nghiệp vụ đã chốt trước. Xem [STORAGE_WORKFLOW_API_REVIEW.md](STORAGE_WORKFLOW_API_REVIEW.md); phần biên nhận bắt buộc trong tài liệu cũ được thay bằng tùy chọn như trên.

## Inbox và realtime

- Mỗi timeline event mới fan-out một inbox cho mỗi Admin đang active, unique `(eventId,userId)`. Nội dung phân biệt tạo đơn/phân công/thanh toán/storage/hoàn tất; không suy nội dung phân công thành khách vừa gửi đồ.
- NotificationDto thêm eventId/dueAt/detectedAt. eventId là định danh sự kiện, notification.id là định danh inbox của người nhận. Deadline có dueAt/detectedAt khi writer cung cấp; legacy có thể null.
- Ghi inbox và delivery outbox cùng transaction nghiệp vụ; rollback không để lại inbox. Realtime do worker đọc outbox sau commit, không giữ transaction khi gọi SignalR.
- Worker quét mỗi 5 giây, lease 2 phút, timeout gửi 15 giây, retry backoff có jitter, tối đa 8 lần rồi DeadLetter. Worker có thể tiếp tục job Sending hết lease sau restart.
- Đây là **at-least-once delivery** tới SignalR; không bảo đảm client đã đọc/nhận khi đang offline. FE upsert theo notification.id, không cộng badge mù theo mỗi push. Reconnect cần tải inbox và unread count từ API.
- Trước gửi kiểm tra lại user active/role/phạm vi đơn, đã đọc/xóa; job không còn phù hợp chuyển Suppressed. Một lần gửi đang diễn ra vẫn có thể tới sau thao tác đọc/xóa; FE xử lý đồng bộ trạng thái và tải lại khi cần.
- Retry không tạo inbox mới và không đưa thông báo đã đọc/xóa trở lại. Event nội bộ Admin không trả cho người đã chuyển sang role khác.
- Các thông báo cho Customer/Merchant vẫn theo audience cũ; mã LK và địa chỉ tiệm không được đưa vào nội dung Customer.

## Deadline và vận hành

Reminder dùng khóa/claim theo đơn, loại deadline, hạn thực và vòng lưu giữ để tránh mất cảnh báo vòng mới. Kiểm tra lại revision/trạng thái/hạn trước khi phát. Trễ SLA chỉ cảnh báo theo quy tắc hiện hành; không tự chặn lấy/trả đồ.

Admin thêm GET `/notification/outbox/summary`: pending, sending, deadLetter, oldestPendingAt, serverNow. DeadLetter có log cảnh báo; chưa tự cấu hình dịch vụ alert ngoài hệ thống hoặc API replay thủ công. Cần theo dõi và xử lý nguyên nhân trước khi can thiệp dữ liệu vận hành.

Render Free ngủ/dừng tiến trình thì worker không chạy liên tục; outbox lưu DB và tiếp tục khi app hoạt động. Không coi việc gửi HTTP thành công là xác nhận client đã nhận realtime.

## Test và trạng thái bàn giao

- `WorkflowMetadata`: chuẩn hóa danh mục, mapping timeline, mã LK UTC+7 không phụ thuộc culture.
- `storage-lifecycle.ps1`: thêm kiểm tra hai vòng LK khác nhau, replay ổn định, projection order/cycle thống nhất và biên nhận tùy chọn.
- `service-categories-workflow.ps1`: quyền, danh mục/đơn snapshot, Admin event fanout, outbox không mất và retry khi inbox đã đọc.
- Đã nối các test này trong Run-ApiSmoke; **chưa thực thi**, không ghi nhận PASS/đã deploy cho bản này.
