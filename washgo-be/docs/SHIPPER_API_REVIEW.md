# Hợp đồng BE Shipper — 11/11/2026

Nghiệm thu local: Docker build, Swagger/OpenAPI, model sạch và các smoke `shipper-workspace`, `shipper-profile`, `shipper-tasks`, `shipper-assignment`, `qr-pin-flow`, `order-cancellation` PASS. Hồi quy thanh toán dùng Mock/SMTP tắt tạm thời; đã khôi phục cấu hình Live của sandbox chính thức từ `.env`. Không gửi giao dịch thật. Chia thành 3 nhánh nối tiếp; xem [hướng dẫn PR](SHIPPER_GIT_PUSH_PLAN.md).

Phạm vi: dashboard, hồ sơ, lịch sử và nhiệm vụ trong ba file `BACKEND_SHIPPER_*.md`. Luồng giao nhận vẫn **mô phỏng xác nhận lấy/đặt đồ**, không phải tích hợp cửa tủ thật. Tiến độ và bằng chứng nghiệm thu xem `IMPLEMENTATION_CHECKLIST.md`; phần phân loại đề xuất tùy chọn ở `SHIPPER_IMPLEMENTATION_PLAN.md` mục 10 vẫn giữ nguyên.

## 1. API và phân quyền

Các API dưới đây dùng ShipperOnly. Tài khoản được lấy từ JWT; User.Id và Shipper.Id là hai định danh khác nhau. Không truyền identity người khác vào bộ lọc để thay scope.

| API | Hợp đồng |
| --- | --- |
| `GET /shipper-dashboard` | period=day/week/month/custom, fromDate/toDate Unix giây cho custom; mặc định hôm nay VN. |
| `GET /shipper-profile` | Thêm revision chuỗi, createdAt/updatedAt, merchantPhone/email/address/imageUrls, ratingCount/ratingSource. |
| `PUT /shipper-profile` | Bắt buộc expectedRevision và operationId bên cạnh các trường hồ sơ được sửa. |
| `GET /shipper-profile/operations/{operationId}` | Trả response đã lưu của chính actor; không có kết quả trả 404, không đồng nghĩa request cũ chắc chắn thất bại. |
| `GET /shipper-profile/activities` | Nhật ký chính actor, page/take/module; không cấp quyền admin audit. |
| `POST /shipper-tasks/filter` | Keyword hai mã đơn, taskType, taskStatus, assignedFrom/assignedTo, sort AssignedAt/Deadline. Mặc định Assigned, Page=1/Take=20. |
| `POST /shipper-tasks/summary` | Current pending theo keyword/ngày phân công; bỏ taskType/taskStatus bảng, trả collect/return/total trong một aggregate. |
| `GET /shipper-tasks/{taskId}` | taskId định danh một lần phân công; assignmentVersion chính là taskId. Chi tiết đơn hiện tại chỉ kèm khi task còn actionable. |
| `GET /shipper-tasks/{taskId}/credentials` | Mã của nhiệm vụ đang hoạt động do chính Shipper được giao; có audit ViewTaskCredential, Cache-Control no-store. |
| `GET /shipper-tasks/{taskId}/operations/{operationId}` | succeeded + kết quả gốc hoặc unknown; chỉ actor thao tác, giữ quyền sau phân công lại. |
| `POST /shipper-delivery/history` | Tìm event hoàn tất theo actor, ngày, hai mã đơn, taskType, lockerId, cancelledReturn, eventStatus. |
| `POST /shipper-delivery/summary` | Tổng/collect/return theo cùng date/keyword/locker/nhánh đơn hủy; bỏ taskType bảng. |
| `GET /shipper-delivery/history/{eventId}` | Lượt hoàn tất của actor, không phụ thuộc Shipper hiện tại của đơn. |
| `POST /shipper-delivery/lockers/filter` | Lookup trạm trong lịch sử của actor, tối đa 1.000 trạm, không lookup toàn hệ thống. |
| `POST /shipper-delivery/export` | CSV UTF-8 có BOM, cùng bộ lọc, chống formula injection, tối đa 366 ngày/25.000 lượt. |

Hai POST `/wash-order/{orderId}/shipper-collect` và `shipper-return` vẫn ShipperOrAdmin. Với Shipper, request mới bắt buộc guard dưới đây. Admin giữ khả năng thao tác hỗ trợ theo policy cũ; không đổi actor Admin thành Shipper.

## 2. Payload giao nhận và timeout

1. Lấy task mới nhất và revision.
2. Khi người dùng chọn xem mã, gọi credential của task đang được giao. Không dùng mã từ list/detail mặc định.
3. Chỉ khi người dùng xác nhận giao nhận mới gọi POST; gửi đúng một PIN hoặc QR.

```json
{
  "taskId": "<taskId>",
  "expectedAssignmentVersion": "<taskId>",
  "expectedTaskType": 1,
  "expectedRevision": 7,
  "operationId": "<GUID sinh một lần cho ý định này>",
  "pinCode": "123456"
}
```

Return dùng expectedTaskType=2. Với QR, bỏ pinCode và gửi qrCode ≤500 ký tự. PIN phải đúng 6 số ASCII. Các mẫu trên là placeholder, lấy dữ liệu thật từ API.

- `operationId` + payload giữ nguyên khi timeout hoặc gửi lại. Không sinh operation mới để né kết quả chưa rõ.
- Replay thành công trả 200/meta.replayed, snapshot trạng thái/revision/ô của lần đầu; kể cả đơn đã chuyển Washing/Completed hoặc phân công lại. Không thêm timeline, không cộng lượt/ô và không thử lại PIN.
- Cùng operationId khác payload →409 OperationIdReused. Expected revision cũ →409 RevisionMismatch; task đã kết thúc/đổi thế hệ →409 AssignmentChanged. Quyền actor được kiểm tra trước khi trả kết quả cũ.
- Lookup unknown không khẳng định request chưa chạy. Gửi lại đúng operationId/payload để server khóa đơn và xử lý an toàn. Bản này không có ledger processing/failed bền vững riêng.
- Sai mã lưu attempt trước trả 400/meta InvalidTaskCredential hoặc TaskLocked, có remainingAttempts/lockoutUntil/serverNow. Lần sai làm revision đổi; khi người dùng nhập lại mã sau lỗi xác định, tải revision mới và tạo ý định mới. Không tự retry mã sai.
- Return hết ô/tủ không khả dụng →409 NoSuitableBox/LockerUnavailable, không đổi slot/counter/order/task. Sau xử lý nguyên nhân, dùng lại operation cũ nếu payload/revision vẫn giữ nguyên.
- Request có mã không được tự động audit payload; ledger lưu fingerprint HMAC, kết quả đã ẩn khách/mã và bỏ timeline. Không lưu plaintext hoặc hash PIN 6 số không có khóa. Nếu xoay JWT signing key đang dùng cho HMAC, request keyed cũ có thể không replay theo fingerprint cũ; cần giữ chính sách xoay khóa phù hợp trước khi vận hành dài hạn.

## 3. Hồ sơ

PUT giữ fullName/phone/avatarUrl/vehicleType/vehiclePlate và thêm expectedRevision chuỗi từ GET + operationId GUID. Email/Merchant/status/isActive/tổng lượt không sửa qua đây. Revision bao phủ User editable fields + Shipper.Revision; đọc entity sau khóa để không giữ revision stale trong EF tracking.

- Avatar null giữ, empty xóa, URL mới phải thuộc uploader. Biển số xe đạp tùy chọn nhưng nếu gửi vẫn kiểm tra pattern/độ dài như phương tiện khác.
- User + Shipper + operation + audit commit chung. Replay không cập nhật lại, lookup chỉ của actor; cập nhật chạy đua với Merchant dùng cùng khóa Shipper.
- Lỗi có code: ShipperProfileMissing, ProfileGuardRequired, RevisionMismatch, InvalidProfile, PhoneAlreadyUsed, AvatarNotOwned, OperationIdReused.
- ratingCount=null/ratingSource=NotAvailable: hiện chưa có mô hình đánh giá Shipper riêng. Không lấy đánh giá Merchant thay Shipper, không bịa rating count cho trường Rating cũ.
- Thông tin Merchant là liên hệ của cửa hàng gắn hồ sơ; thiếu trả null/[] hoặc tên rỗng. Không trả tài liệu pháp lý/CCCD/ngân hàng. GET không tự tạo hồ sơ rỗng hoặc đoán Merchant khi thiếu liên kết.
- Tạm nghỉ (`Shipper.IsActive=false`) khác khóa tài khoản: vẫn xem hồ sơ/dashboard và kết quả operation của mình; không cấp quyền nhận việc/mã nhiệm vụ mới. User bị khóa hoặc Shipper đã xóa vẫn theo guard xác thực hiện hành.
- Dùng lại `/auth/security`, change-password, sessions/revoke và logout. 2FA vẫn không hỗ trợ.

## 4. Nguồn dữ liệu, preview và SLA

- DeliveryTask hiện có được mở rộng; không tạo bảng song song. Assign tạo task mới, kết thúc task cũ; taskId bất biến cho thế hệ đó.
- `taskLocker*` trên list/detail/preview: Collect lấy trạm gửi, Return lấy trạm trả; thiếu trạm trả không lấy tên/địa chỉ trạm gửi khác ID thay thế. Return chưa đặt vào tủ thì box=null. Mọi ảnh/liên hệ enrich theo tập ID, không gọi GET từng dòng.
- Items.ServiceImageUrls, LockerImageUrls/ReturnLockerImageUrls và MerchantImageUrls/PhoneNumber tái sử dụng bộ media cũ; email nghiệp vụ bổ sung cho Shipper. PIN/QR Customer và nhiệm vụ không có trong list/detail mặc định của Shipper.
- Bản ghi hoàn tất lưu completion event/actor, mốc thời gian, tên/địa chỉ/toạ độ/ô, mã đơn/Merchant, trạng thái sự kiện và ghi chú. Thay đổi trạm/đơn sau đó không ghi đè snapshot.
- Ảnh là tài nguyên hiện tại (`imageBasis=CurrentResource`), không bảo đảm URL ảnh tồn tại vĩnh viễn; mất ảnh không xóa dòng lịch sử. Metadata snapshotSource phân biệt CapturedAtCompletion và LegacyCurrentResource.
- Migration ghép timeline/task cũ một-một kể cả cùng giây, bổ sung event thiếu task, không đếm cả task và timeline hai lần. Địa điểm khôi phục từ dữ liệu hiện tại được đánh dấu LegacyCurrentResource; không coi đó là snapshot thật tại thời điểm cũ. Actor/mốc không có bằng chứng vẫn null.
- `eventStatus` là trạng thái tại sự kiện. `orderStatus` giữ hợp đồng trạng thái hiện tại khi order còn dữ liệu; `currentOrderStatus=null` nếu không tìm thấy, vẫn giữ event.
- SLA Collect tính từ DepositedAt, Return thường từ PaidAt, theo giờ SLA chốt trên task từ policy của đơn. Trễ vẫn cho xác nhận, chỉ ghi cảnh báo. Return đơn hủy chưa có quy tắc mốc riêng: deadline=null và SlaUnavailableReason, không giả PaidAt.

## 5. Dashboard và bộ lọc

- Ngày/tuần (7 ngày gần nhất)/tháng/custom theo lịch Việt Nam. Custom tối đa 366 ngày; ngày hiện tại có thể chọn đến cuối ngày nhưng server clamp To về hiện tại. Không trả số liệu tương lai.
- Pending hiện tại; số hoàn tất trong kỳ theo **người thực hiện thực tế**. totalDeliveries giữ counter lifetime cũ của Shipper được giao, có thể gồm lượt Admin làm thay; DTO trả basis để không so nhầm.
- Dashboard đọc repeatable-read, tối đa 25.000 completion rows cho cả kỳ + kỳ so sánh; quá giới hạn trả413 thay vì âm thầm cắt số liệu. Preview tối đa5 mỗi loại và5 hoàn tất gần nhất.
- Biểu đồ theo giờ với day, theo ngày với các kỳ dài. KPI có sample/unknown count và snapshot coverage; không có mẫu thì OnTimeRate/AverageDurationHours=null. Kỳ trước cùng độ dài elapsed time; không bịa tăng trưởng khi baseline thiếu.
- Lịch sử mặc định 366 ngày gần nhất khi không truyền cận; truyền khoảng ≤366 ngày để xem các kỳ cũ. Page=1, take1–100; Unix giây, enum/range/sort sai trả400. Sort mặc định CompletedAt giảm, Return trước Collect cùng giây, eventId cuối; sort tùy chọn CompletedAt/OrderCode.
- Task filter dùng assignedFrom/assignedTo, không dùng ngày tạo order. Summary pending độc lập taskType/taskStatus của bảng; snapshot lịch sử summary độc lập taskType bảng. Xem các trường basis/applied date trước hiển thị.

## 6. Ngoài phạm vi / lưu ý tích hợp

Không làm IoT cửa thật, routing traffic/ETA, thu nhập Shipper, 2FA, tự nhận/từ chối nhiệm vụ hoặc tự Available/Offline, nhiệm vụ chuyển kho mới. Chuyển kho cũ giữ guard/quyền hiện có. Không gọi các phần này “đã hoàn thành” từ checkbox mô phỏng.

Đây có **breaking changes cho FE Shipper**: lấy mã qua credentials và bổ sung delivery/profile guards. Nâng FE cùng đợt; không giữ endpoint cũ thiếu guard như một đường vòng. Các route list/detail đơn và quản lý Merchant/Admin vẫn tồn tại với quyền hiện hành.

Migration: `20261111021910_AddShipperTaskSnapshots` và `20261111024338_ReconcileLegacyShipperCompletions`. Migration sau ghép lại 21 cặp task legacy lệch phần lẻ giây đã phát hiện local: giữ task gốc/AssignedAt, giữ eventId và bỏ dòng phát sinh trùng chưa được operation tham chiếu. Không xóa sự kiện timeline hoặc bịa người thực hiện; Down không tạo lại dữ liệu trùng. Không có yêu cầu thay đổi quy tắc nghiệp vụ Word ngoài phạm vi đã chốt; các thay đổi kỹ thuật/payload và giới hạn đọc được ghi tại đây, không thêm Use Case trùng vào Word.
