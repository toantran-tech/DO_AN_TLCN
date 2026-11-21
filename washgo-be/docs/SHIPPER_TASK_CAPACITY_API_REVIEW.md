# Bàn giao FE — Shipper nhận nhiều nhiệm vụ trong giới hạn

Ngày 21/11/2026. Thực hiện `Docs/cần làm/BACKEND_MERCHANT_SHIPPER_MANAGEMENT.md`.

## Luật

- Shipper nhận thêm nhiệm vụ dù đang có nhiệm vụ chưa hoàn tất, miễn **activeTaskCount < maxActiveTasks**. Nhận trước để làm lần lượt; không có lập tuyến, không tự kéo dài deadline của từng nhiệm vụ.
- **Giới hạn**: cài đặt hệ thống `maxActiveTasksPerShipper` (1–20, **mặc định 3**), Admin đổi bằng `PUT /system-setting/dispatch { maxActiveTasksPerShipper, expectedVersion, operationId }`; `GET /system-setting/` trả giá trị hiện hành. Không có giá trị 0/null = không giới hạn. Giảm dưới số đang có chỉ chặn nhận thêm, không hủy nhiệm vụ đã giao.
- **Đếm**: mỗi đơn đang có chuyến chưa hoàn tất của Shipper tính 1 (lấy, trả, chuyển kho, giao lại từ kho, trả đồ đơn hủy) — cùng quy tắc ở mọi màn hình. Lịch sử đã xong/hủy/thay người không tính. Hoàn tất chuyến (ví dụ đã lấy đồ về tiệm, đơn vẫn đang giặt), hủy phân công hoặc chuyển cho người khác giải phóng chỗ ngay.
- Phân công lại cùng chuyến cho cùng người hoặc gửi lại cùng `operationId` không chiếm thêm chỗ.
- Trạng thái vận hành không tự đổi khi có nhiệm vụ: chỉ Shipper **đang làm việc + Available + tài khoản hoạt động** mới nhận; Busy/Offline/tạm nghỉ/bị khóa vẫn bị chặn như cũ.
- Kiểm tra năng lực chạy trên server sau khi khóa hàng Shipper trong transaction: hai phân công cùng lúc vào chỗ cuối chỉ một thành công.

## API

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

Migration `AddShipperTaskCapacity` thêm `system_settings.max_active_tasks_per_shipper` = 3 cho dữ liệu hiện có. Không đổi đơn/nhiệm vụ cũ.
