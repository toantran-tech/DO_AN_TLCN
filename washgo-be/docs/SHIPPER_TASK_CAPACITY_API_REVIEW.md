# Bàn giao FE — Shipper nhận nhiều nhiệm vụ trong giới hạn

Cập nhật 23/11/2026 theo `BACKEND_MERCHANT_SHIPPER_MANAGEMENT.md`.

## Luật

- Shipper nhận thêm nhiệm vụ dù đang có nhiệm vụ chưa hoàn tất, miễn **activeTaskCount < maxActiveTasks**. Nhận trước để làm lần lượt; không có lập tuyến, không tự kéo dài deadline của từng nhiệm vụ.
- **Giới hạn** lưu riêng trên từng Shipper, là số nguyên 1–20. Shipper mới mặc định 3. Chỉ Merchant sở hữu được đặt giới hạn. Khi đang có 3 nhiệm vụ, đặt 2 bị chặn, đặt 3 được lưu và hết chỗ, đặt 4 còn một chỗ.
- **Đếm**: mỗi đơn đang có chuyến chưa hoàn tất của Shipper tính 1 (lấy, trả, chuyển kho, giao lại từ kho, trả đồ đơn hủy) — cùng quy tắc ở mọi màn hình. Lịch sử đã xong/hủy/thay người không tính. Hoàn tất chuyến (ví dụ đã lấy đồ về tiệm, đơn vẫn đang giặt), hủy phân công hoặc chuyển cho người khác giải phóng chỗ ngay.
- Phân công lại cùng chuyến cho cùng người hoặc gửi lại cùng `operationId` không chiếm thêm chỗ.
- Trạng thái vận hành không tự đổi khi có nhiệm vụ: chỉ Shipper **đang làm việc + Available + tài khoản hoạt động** mới nhận; Busy/Offline/tạm nghỉ/bị khóa vẫn bị chặn như cũ.
- Kiểm tra năng lực chạy trên server sau khi khóa hàng Shipper trong transaction: hai phân công cùng lúc vào chỗ cuối chỉ một thành công.

## API

### Merchant tạo và sửa hồ sơ

- `POST /shipper/` nhận thêm `maxActiveTasks` tùy chọn. Bỏ qua trường thì BE dùng 3. Admin tạo theo luồng hiện tại sẽ dùng 3 và nhận 403 `ShipperTaskLimitForbidden` nếu gửi trường này. Quy tắc áp dụng cả khi Admin tạo qua `/admin/accounts`.
- `PUT /shipper/{id}` giữ payload hồ sơ hiện có. Khi gửi `maxActiveTasks`, gửi kèm `expectedRevision` hoặc `If-Match` và `operationId` hoặc `Idempotency-Key`. Nếu bỏ qua giới hạn khi sửa, BE giữ giá trị cũ. Null tường minh, số thập phân, 0 và ngoài 1–20 bị từ chối với HTTP 400.
- Merchant khác tiệm nhận 404. Shipper tự xem ở `/shipper-profile/` và không được đặt giới hạn.
- Giảm dưới tải trả 409 `ShipperTaskLimitBelowActiveCount`, kèm `meta.activeTaskCount`, `meta.maxActiveTasks` hiện hành, `meta.minimumAllowedMaxActiveTasks` và lỗi `errors.maxActiveTasks`. Toàn bộ hồ sơ giữ nguyên. FE hiển thị số mới để người dùng điều chỉnh.
- Phiên bản cũ trả 409 `RevisionMismatch` với dữ liệu mới. Gửi lại cùng mã và cùng payload trả kết quả đã lưu. Dùng cùng mã cho payload khác trả 409 `OperationIdReused`. Sau timeout, tra `GET /shipper/{id}/operations/{operationId}` bằng chính tài khoản đã gửi.
- Tạo lại bằng cùng `clientRequestId` và cùng payload trả Shipper đã tạo, không gửi lại email. Với bản ghi trước migration, replay giữ cách đối chiếu email cũ và cần bỏ trường `maxActiveTasks`.
- Gỡ nhóm Điều phối Shipper khỏi Cài đặt hệ thống Admin. `PUT /system-setting/dispatch` trả 410 `DispatchSettingsMovedToShipper`. `GET /system-setting/` bỏ trường `maxActiveTasksPerShipper`.


### Danh sách Shipper cho một chuyến

`GET /wash-order/{id}/available-shippers?includeUnavailable=true|false` và `GET /storage/{orderId}/shippers?includeUnavailable=...`

- Mặc định chỉ trả người **chọn được**; `includeUnavailable=true` trả mọi Shipper chưa xóa của tiệm, người không chọn được có lý do.
- Trường mới mỗi dòng: `activeTaskCount`, `maxActiveTasks`, `remainingTaskCapacity` (= max − active, không âm), `canAssign`, `blockedReasonCode` (`AccountInactive` | `ShipperOnLeave` | `ShipperNotAvailable` | `ShipperCapacityExceeded` | null), `status`, `isWorking`. Giữ nguyên `id, fullName, avatarUrl, phone, email, vehicleType, vehiclePlate, rating, taskType`; `activeDeliveryCount` giữ để tương thích (= activeTaskCount).
- `canAssign` đã loại chính chuyến đang xét (phân công lại cùng người không bị coi là đầy). Người chọn được xếp trước.
- Danh sách mặc định rỗng → `message` giải thích chưa có Shipper nhận được.

### Xác nhận phân công

`PUT /wash-order/assign-shipper` (và phân công kho) kiểm tra lại trên server:

| Tình huống | HTTP | `meta.code` |
| --- | --- | --- |
| Đã đủ giới hạn | 409 | `ShipperCapacityExceeded`, kèm `meta.activeTaskCount`, `meta.maxActiveTasks`, `meta.remainingTaskCapacity` mới nhất |
| Shipper khác tiệm/không tồn tại | 400 | `ShipperNotInMerchant` |
| Tạm nghỉ, Busy/Offline, tài khoản khóa | 400 | `ShipperNotAvailable` (phân công đơn) / `ShipperNotEligible` (phân công kho) |

### Quản lý Shipper và hồ sơ Shipper

- `GET /shipper/{id}` và `POST /shipper/filter`: thêm `maxActiveTasks`, `remainingTaskCapacity` cạnh `activeTaskCount` (cùng cách đếm với màn phân công).
- `GET /shipper-profile/` (Shipper tự xem): `activeTaskCount`, `maxActiveTasks`, `remainingTaskCapacity`.

## Dữ liệu

Migration `AddPerShipperTaskLimit` khởi tạo mỗi giới hạn bằng số lớn hơn giữa cấu hình chung cũ (mặc định 3 nếu chưa có cấu hình) và số nhiệm vụ chưa xong. Nếu kết quả ngoài 1–20, migration dừng và rollback. Giữ nguyên mọi nhiệm vụ. Cột cấu hình chung được giữ để chuyển đổi dữ liệu và đã ngừng dùng ở API. Triển khai BE mới và migration cùng đợt.
