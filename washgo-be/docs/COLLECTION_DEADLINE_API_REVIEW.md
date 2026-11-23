# Bàn giao FE — Giờ mở cửa Merchant và hạn thu gom

Ngày 23/11/2026. Thực hiện `Docs/cần làm/BACKEND.md`.

## 1. Giờ mở cửa có cấu trúc (bắt buộc)

Trường mới `openingSchedule` trên `POST /merchant/`, `PUT /merchant/`, `POST /merchant-profile/update-requests`. Trường này cũng được trả trong `GET /merchant/{id}`, danh sách Merchant và `GET /merchant-profile`.

```json
{
  "timeZone": "Asia/Ho_Chi_Minh",
  "alwaysOpen": false,
  "weekly": [
    { "dayOfWeek": 1, "shifts": [ { "open": "08:00", "close": "12:00" }, { "open": "13:00", "close": "20:00" } ] }
  ],
  "closedDates": ["2026-12-25"]
}
```

- `dayOfWeek`: 1 = Thứ Hai … 7 = Chủ Nhật. Ngày không khai báo là ngày nghỉ.
- Mỗi ngày tối đa 4 ca. Giờ theo dạng `HH:mm`; giờ đóng được phép là `24:00`.
- Ca là [mở, đóng). Mở phải trước đóng. **Mở = đóng không được hiểu là 24 giờ.**
- Không có ca qua nửa đêm. Muốn chạy qua đêm thì tách thành `22:00–24:00` và `00:00–06:00` ngày sau.
- `alwaysOpen: true` là lựa chọn 24/7 tường minh, không kèm `weekly`. BE không bao giờ tự suy ra 24/7.
- `closedDates`: ngày nghỉ hoặc ngoại lệ theo giờ VN, dạng `yyyy-MM-dd`, tối đa 60 ngày.
- Chỉ hỗ trợ múi giờ `Asia/Ho_Chi_Minh` (UTC+7, không có giờ mùa hè).
- `openingHours` (chuỗi) vẫn được trả để hiển thị. Khi gửi `openingSchedule`, BE tự sinh `openingHours` (ví dụ "Hằng ngày 08:00-20:00").
- Tương thích ngược: nếu không gửi `openingSchedule`, BE chỉ nhận `openingHours` khi tính được chính xác:
  - `"HH:mm-HH:mm"`: áp dụng mọi ngày;
  - `{"mon":"HH:mm-HH:mm[,HH:mm-HH:mm]", …, "sun": …}`;
  - gửi lại nguyên chuỗi hiện tại.

  Chuỗi mô tả tự do bị từ chối.
- Tạo Merchant: bắt buộc. Sửa: bỏ trống cả hai trường thì giữ lịch hiện có. Merchant cũ chưa có lịch phải bổ sung mới lưu được.

Lỗi 400 có `meta.code`, `meta.fields: ["openingSchedule"]` và `errors.openingSchedule`:

| meta.code | Khi nào |
| --- | --- |
| `OpeningHoursRequired` | Thiếu, rỗng hoặc chỉ khoảng trắng; Merchant chưa có lịch |
| `OpeningHoursInvalidFormat` | Sai dạng giờ/ngày, chuỗi tự do, 24/7 kèm ca, quá 4 ca/ngày |
| `OpeningHoursInvalidShift` | Mở ≥ đóng (kể cả mở = đóng) |
| `OpeningHoursOverlap` | Các ca trong một ngày chồng lấn |
| `OpeningHoursNoOpenDay` | Không có ngày nào có ca |
| `OpeningHoursUnsupportedTimeZone` | timeZone khác `Asia/Ho_Chi_Minh` |

FE giữ dữ liệu đã nhập khi nhận các lỗi này. Nhãn vẫn là "Giờ mở cửa", kèm ghi chú "dùng để tính hạn thu gom".

## 2. Cách tính hạn thu gom

- Mốc gốc là `depositedAt` (lúc khách gửi đồ thành công).
- Thời lượng là `collectSlaHoursSnapshot`: SLA thu gom lưu theo đơn lúc tạo, mặc định 4 giờ.
- BE chỉ cộng giờ trong các ca của Merchant phụ trách:
  - gửi trong ca thì tính ngay;
  - gửi ngoài ca thì bắt đầu ở đầu ca kế tiếp;
  - hết ca chưa đủ thì chuyển phần còn lại sang ca sau;
  - bỏ qua giờ nghỉ trưa, ngày nghỉ trong tuần và `closedDates`.
- Gửi đúng giờ đóng cửa thì tính từ ca sau. Quá hạn khi thời điểm hiện tại > hạn.
- Ví dụ với lịch 08:00–20:00 và SLA 4 giờ: gửi 10:00 → hạn 14:00; gửi 18:00 → hạn 10:00 hôm sau; gửi 21:00 hoặc đúng 20:00 → hạn 12:00 hôm sau.
- Thời điểm tính:
  - Khi gửi đồ, nếu đơn đã có Merchant.
  - Khi Admin phân công Merchant, kể cả tự động. Hạn vẫn tính từ `depositedAt`, nên phân công muộn không kéo dài hạn.
- Sau khi đã tính, hạn **không đổi** khi:
  - phân công hoặc đổi Shipper;
  - Admin sửa giờ mở cửa;
  - đổi SLA hệ thống.

  BE lưu lịch đã áp dụng (`collectionScheduleVersion` và snapshot) cùng đơn.
- Đổi Merchant cho đơn đã có hạn: giữ nguyên hạn, không đặt lại.
- Merchant không có ca nào trong 400 ngày tới: BE dừng tìm, không lặp vô hạn, và trả trạng thái `NoUpcomingShift`.
- Quá hạn chỉ là cảnh báo. BE không tự hủy đơn, mở ô hay đổi trạng thái.

## 3. Trường trên đơn (một nguồn cho mọi vai trò)

`WashOrderResultDto`/`WashOrderDetailDto` (Customer, Merchant, Shipper, Admin) có thêm:

| Trường | Ý nghĩa |
| --- | --- |
| `collectionDeadlineAt` | Hạn thu gom có thẩm quyền (UTC). Null khi chưa tính được. |
| `collectionStartsAt` | Lúc bắt đầu tính: lúc gửi nếu đang trong ca, ngược lại là đầu ca kế tiếp. FE dùng để hiển thị "Bắt đầu thu gom từ 08:00 ngày mai". |
| `collectionDeadlineState` | Xem bảng dưới. |
| `collectSlaHoursSnapshot` | Số giờ SLA của đơn. |
| `isCollectionOverdue` | `serverNow > collectionDeadlineAt` khi đồ còn ở locker gửi. |
| `serverNow` | Đã có sẵn. |

| collectionDeadlineState | Ý nghĩa / FE hiển thị |
| --- | --- |
| `Scheduled` | Có hạn. |
| `AwaitingMerchant` | Đã gửi, chờ phân công Merchant. Hạn sẽ tính từ lúc gửi. |
| `ScheduleMissing` | Merchant chưa có giờ mở cửa hợp lệ. Admin được thông báo; khi Admin bổ sung lịch, hạn tự tính lại từ `depositedAt`. |
| `NoUpcomingShift` | Không có ca trong 400 ngày tới. Sẽ tính lại khi lịch được sửa. |
| `Legacy` | Đơn gửi trước quy tắc này; giữ hạn đã lưu (xem mục 5). |

- Nhiệm vụ Shipper lấy đồ (`deadline` của task) dùng đúng `collectionDeadlineAt`. `slaStartedAt` = `depositedAt`, `slaHours` = SLA của đơn.
- FE chỉ định dạng các mốc BE trả, không tự cộng SLA hay tự phân tích `openingHours`.

## 4. Nơi dùng chung hạn này

- Cảnh báo `CollectionOverdue` của Admin (`GET /admin/alerts`).
- Cảnh báo SLA của Merchant.
- Nhắc trễ lấy đồ (worker; mỗi đơn chỉ nhắc một lần).
- Cảnh báo khi Shipper lấy đồ trễ.
- ETA khi chưa đủ dữ liệu lịch sử.

Không còn nhánh nào dùng `depositedAt + giờ` liên tục. Đơn gửi ban đêm sẽ không bị báo trễ trước khi cửa hàng mở.

## 5. Dữ liệu cũ (migration `AddCollectionDeadline`)

- **Merchant**:
  - `openingHours` đúng dạng `"HH:mm-HH:mm"` được chuyển thành lịch hằng ngày tương ứng.
  - Các chuỗi khác để trống lịch: không điền giờ giả, không coi là 24/7. Admin cần bổ sung qua `PUT /merchant/`.
  - Merchant mẫu trên DB dev hiện có `opening_hours = NULL`, nên cần bổ sung trước khi dùng cách tính mới.
  - Seed mới (DB trống/CI) tạo Merchant mẫu với lịch 24/7 khai báo tường minh.
- **Đơn đã gửi trước migration**:
  - Lấy `deadline` của nhiệm vụ lấy đồ nếu đã có.
  - Nếu chưa, dùng `depositedAt + collectSlaHoursSnapshot` (hoặc SLA hệ thống), tức đúng hạn đã hứa trước đây.
  - Trạng thái là `Legacy`. Không đơn nào bị null vĩnh viễn để thành "không bao giờ quá hạn".
- Khi Admin bổ sung giờ mở cửa (sửa trực tiếp hoặc duyệt yêu cầu cập nhật hồ sơ), các đơn `ScheduleMissing`/`NoUpcomingShift` còn ở locker gửi của Merchant đó được tính hạn ngay, vẫn từ `depositedAt`.

## 6. Ngoài phạm vi

- Giờ tiếp cận locker của khách (locker 24/7) không bị giới hạn bởi lịch Merchant. Chưa có dữ liệu giờ mở locker riêng.
- Lịch chuyến cố định (ví dụ 08:00/14:00) là mô hình khác, chưa làm.

## 7. Kiểm thử

- C# `tests/smoke/CollectionDeadline` (CI) kiểm tra bộ tính hạn:
  - các ví dụ trong yêu cầu: 10:00, 18:00, 21:00, đúng giờ đóng/mở, trước giờ mở;
  - SLA dài hơn một ca, nghỉ trưa, cuối tuần, ngày lễ, ca tách qua nửa đêm, 24/7 tường minh;
  - ngày địa phương khi ngày UTC đổi, giới hạn tìm ca;
  - mã lỗi validate, đọc chuỗi cũ chính xác.
- Smoke API `tests/smoke/collection-deadline.ps1` (CI) kiểm tra:
  - bắt buộc nhập lịch và các mã lỗi;
  - sửa Merchant giữ lịch cũ khi không gửi;
  - hạn tính từ lúc gửi khi phân công muộn, không đổi khi sửa lịch;
  - `ScheduleMissing` rồi được tính lại;
  - cảnh báo Admin dùng hạn đã lưu: gửi ban đêm + 4 giờ chưa bị báo.
- Các smoke cũ có lùi `deposited_at` để giả quá hạn đã được cập nhật để lùi cả `collection_deadline_at`.
