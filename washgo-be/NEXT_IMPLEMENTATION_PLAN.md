# Kế hoạch triển khai dự án WashGo (Backend & Admin)
Cập nhật lần cuối: 05/10/2026

---

## PHẦN I: CÁC NỘI DUNG ĐÃ HOÀN TẤT (COMPLETED)

### 1. Cơ sở dữ liệu & Entity Core (Database & EF Core)
- [x] **17 Entity nền tảng/core**:
  1. `Role` (quản trị, khách hàng, đối tác giặt, giao hàng)
  2. `User` (tài khoản định danh hệ thống)
  3. `RefreshToken` (quản lý phiên đăng nhập và thu hồi token)
  4. `OtpCode` (mã OTP xác thực đăng ký / quên mật khẩu)
  5. `Customer` (hồ sơ khách hàng)
  6. `Merchant` (hồ sơ đối tác tiệm giặt)
  7. `Shipper` (hồ sơ nhân viên giao hàng)
  8. `ServiceArea` (khu vực hoạt động / quận huyện)
  9. `MerchantServiceArea` (liên kết Merchant với ServiceArea)
  10. `MerchantImage` (hình ảnh cửa hàng đối tác)
  11. `ServiceChangeRequest` (yêu cầu phê duyệt thay đổi dịch vụ)
  12. `Locker` (tủ đồ thông minh)
  13. `LockerBox` (map DB: `locker_slots` — ô tủ đồ)
  14. `WashService` (map DB: `services` — dịch vụ giặt sấy)
  15. `WashOrder` (map DB: `orders` — đơn hàng giặt)
  16. `WashOrderItem` (map DB: `order_items` — chi tiết dịch vụ trong đơn)
  17. `WashOrderTimeline` (map DB: `order_status_history` — lịch sử trạng thái đơn hàng)
- [x] **Cấu hình EF Core & PostgreSQL**:
  - Dùng PostgreSQL schema riêng `washgo`.
  - Khóa chính chuẩn hóa kiểu `Guid`.
  - Cấu hình quan hệ bảng, foreign key, delete behavior và unique index chuẩn theo code EF:
    - Unique `users.email`
    - Unique `(users.auth_provider, users.provider_id)`
    - Unique `roles.name`
    - Unique `refresh_tokens.token`
    - Unique `service_areas.code`
    - Unique `merchants.tax_code`
    - Unique `shippers.user_id`
    - Unique `(merchant_service_areas.merchant_id, merchant_service_areas.service_area_id)`
    - Unique `(locker_slots.locker_id, locker_slots.slot_number)`
    - Unique `services.code`
    - Unique `orders.order_code`
  - Khóa SDK .NET qua `global.json` (`8.0.401`) và nâng cấp `System.Linq.Dynamic.Core` lên `1.7.4`.
- [x] **Migration**:
  - `20261004141822_InitialWashGoSchema`
  - `20261005042149_AddIdentityPartnersAndAlignCoreSchema`
  - Đã apply đầy đủ vào DB PostgreSQL, không còn pending model changes.

---

### 2. Dữ liệu mẫu (Data Seeding)
- [x] Triển khai `WashGoDataSeederContributor` tự động chạy khi khởi động hệ thống (idempotent, không sinh trùng dữ liệu, toàn bộ GUID dùng ký tự hex chuẩn):
  - **Roles**: `Admin`, `Customer`, `Merchant`, `Shipper`.
  - **Tài khoản & Hồ sơ**:
    - `admin@washgo.vn` (`Admin@123` - Role Admin, Id: `a1111111-1111-1111-1111-111111111111`)
    - `merchant@washgo.vn` (`Merchant@123` - Role Merchant kèm profile Merchant "Cửa Hàng Giặt Sấy WashGo Q1", Id: `d3333333-3333-3333-3333-333333333333`)
    - `customer@washgo.vn` (`Customer@123` - Role Customer kèm profile Customer "Nguyễn Văn Khách", Id: `c2222222-2222-2222-2222-222222222222`)
    - `shipper@washgo.vn` (`Shipper@123` - Role Shipper kèm profile Shipper "Trần Văn Shipper", Id: `e4444444-4444-4444-4444-444444444444`)
  - **Khu vực phục vụ**: `Khu vực Quận 1` (`SA_Q1`, Id: `e1111111-1111-1111-1111-111111111111`).
  - **Hạ tầng Tủ & Ô tủ**:
    - Locker: `Trạm Locker Bến Thành` (`LK_Q1_001`, Id: `a1111111-aaaa-1111-aaaa-111111111111`).
    - 3 ô tủ demo (`Slot 01` - Standard, `Slot 02` - Small, `Slot 03` - Large) với `TotalBoxes = 3`, `AvailableBoxes = 3`.
  - **Dịch vụ giặt**:
    - Giặt sấy sấy khô cao cấp (theo kg - `SVC_WASH_DRY`).
    - Giặt hấp giặt khô vest/đầm (theo chiếc - `SVC_DRY_CLEAN`).
    - Vệ sinh giày thể thao (theo đôi - `SVC_SHOES`).
  - Toàn bộ mật khẩu seed được mã hóa thực tế bằng **BCrypt** (`BCrypt.Net-Next`).

---

### 3. Xác thực & Phân quyền (Authentication & Authorization)
- [x] **Bảo mật mật khẩu**: Sử dụng BCrypt.Net-Next để hash/verify mật khẩu.
- [x] **JWT Token Generator**: Cấp JWT Access Token chứa các claim: `sub`, `email`, `role`, `user_id`.
- [x] **Refresh Token Rotation**: Cơ chế cấp mới Access Token qua Refresh Token và thu hồi (revoke) khi logout.
- [x] **Xác thực OTP**: Quản lý mã OTP có hạn sử dụng, trạng thái đã dùng và đếm số lần nhập sai.
- [x] **Cấu hình RBAC ban đầu**:
  - Cấu hình JWT Bearer Authentication middleware trong ASP.NET Core.
  - Thiết lập các policy: `AdminOnly`, `MerchantOrAdmin`, `ShipperOrAdmin`.
  - Hiện tại mới kích hoạt `.RequireAuthorization()` ở endpoint `/auth/me`; các endpoint CRUD nền tảng chưa gắn policy chi tiết để thuận tiện phát triển/test ban đầu.
- [x] **Endpoints Auth (`/auth/*`)**:
  - `POST /auth/login`: Đăng nhập cấp JWT Token và Refresh Token.
  - `POST /auth/refresh-token`: Làm mới token.
  - `POST /auth/logout`: Thu hồi Refresh Token (`IsRevoked = true`).
  - `POST /auth/register`: Đăng ký tài khoản mới.
  - `POST /auth/send-otp`: Gửi OTP xác thực.
  - `POST /auth/verify-otp`: Kiểm tra mã OTP.
  - `POST /auth/reset-password`: Đặt lại mật khẩu mới.
  - `GET /auth/me`: Lấy thông tin tài khoản hiện tại (yêu cầu Bearer Token).
- [!] **Lưu ý hiện trạng Auth/OTP**:
  - Đã triển khai mức dev/test.
  - Hiện tại còn log OTP ra console và chấp nhận fallback mã `123456` để kiểm thử thủ công dễ dàng.
  - Chưa production-ready: cần bỏ fallback `123456`, bỏ log console, tích hợp SMS/Email provider thực tế trước khi phát hành.

---

### 4. API CRUD Nền tảng & Nghiệp vụ Luồng Đơn (Order Flow)
> **Lưu ý Routing**: Hiện tại các route trong backend map trực tiếp không có prefix `/api/v1` (ví dụ: `/auth`, `/service-area`, `/merchant`, `/locker`, `/wash-service`, `/wash-order`). Nếu muốn chuyển sang chuẩn `/api/v1/...` sẽ cấu hình route group hoặc global prefix sau.

- [x] **Module ServiceArea (`/service-area`)**: Danh sách, Chi tiết, Tạo mới, Cập nhật, Đổi trạng thái kích hoạt.
- [x] **Module Merchant (`/merchant`)**: Danh sách (kèm filter/paging), Chi tiết, Tạo mới, Duyệt đối tác (`APPROVED`/`SUSPENDED`).
- [x] **Module Locker & LockerBox (`/locker`)**: Quản lý tủ, ô tủ, cập nhật trạng thái hoạt động/bảo trì.
- [x] **Module WashService (`/wash-service`)**: Quản lý danh mục dịch vụ giặt, bảng giá (theo kg / theo món).
- [x] **Module WashOrder (`/wash-order`) & Luồng trạng thái nghiêm ngặt**:
  - Quản lý tạo đơn gắn liền với Customer, Merchant, Locker, LockerSlot và danh sách chi tiết dịch vụ.
  - Kiểm tra và thực thi luồng tuần tự:
    `PENDING` -> `DEPOSITED` -> `COLLECTED` -> `WASHING` -> `DRYING` -> `IRONING` -> `FOLDING` -> `AWAITING_PAYMENT` -> `PAID` -> `READY_TO_RETURN` -> `RETURNED` -> `COMPLETED`.
  - **Quy tắc cứng (Business Rule)**: Nghiêm cấm chuyển sang `READY_TO_RETURN` và `RETURNED` nếu đơn chưa ở trạng thái `PAID`.
  - **Đồng bộ trạng thái ô tủ (`LockerBoxStatus`)**:
    - Khi tạo order: ô tủ chuyển sang `Reserved`.
    - Khi khách gửi đồ (`Deposited`): ô tủ chuyển sang `Occupied`.
    - Khi shipper lấy đồ (`Collected`): ô tủ chuyển về `Available`.
    - Khi đồ giặt xong sẵn sàng trả về tủ (`ReadyToReturn`): ô tủ chuyển sang `Occupied`.
    - Khi khách đến nhận đồ (`Returned` / `Completed`): ô tủ chuyển về `Available`.
  - Ghi nhận lịch sử luồng trạng thái vào `WashOrderTimeline` (`order_status_history`) trong cùng transaction.

---

### 5. Giao diện Quản trị Web (Admin UI - `washgo-admin`)
- [x] **Màn hình Đăng nhập / Đăng xuất (Login/Logout)**:
  - Form đăng nhập kết nối trực tiếp API Backend (`/auth/login`).
  - Đã có logoutApi, UI logout hiện xóa token local; cần nối gọi `/auth/logout` để revoke refresh token.
  - Lưu trữ Token và User Info vào Redux store + LocalStorage.
  - Tự động đính kèm `Bearer <token>` vào request header qua Axios Interceptor.
  - Điều hướng tự động (Auth Guard / Protected Routes).
- [x] **Màn hình Quản lý Khu vực (Service Area)**: Kết nối API Backend thật (`/service-area`).
- [x] **Màn hình Quản lý Đối tác (Merchant)**: Kết nối API Backend thật (`/merchant`), xem danh sách và trạng thái duyệt.
- [x] **Màn hình Quản lý Tủ đồ (Locker)**: Hiển thị danh sách tủ, vị trí và số ô tủ (`/locker`).
- [x] **Màn hình Quản lý Dịch vụ (Wash Service)**: Hiển thị danh sách bảng giá dịch vụ theo đối tác (`/wash-service`).
- [x] **Màn hình Quản lý Đơn hàng (Wash Order)**: Hiển thị danh sách đơn, xem chi tiết và thao tác cập nhật trạng thái theo luồng hợp lệ (`/wash-order`).

---

### 6. Kiểm tra & Build (Build & Verification)
- [x] Backend build sạch: `0 Warning(s), 0 Error(s)`.
- [x] EF Core không còn pending model changes.
- [x] Frontend `washgo-admin` build sạch bằng Vite (`npm run build` thành công).

---

## PHẦN II: CÁC NỘI DUNG CHƯA HOÀN THÀNH / KẾ HOẠCH TIẾP THEO (PENDING / ROADMAP)

### 1. Phân quyền Endpoint chặt chẽ (RBAC Enforce) & Chuẩn hóa Auth Production
- [ ] Gắn Policy `.RequireAuthorization(...)` chi tiết cho từng nhóm endpoint backend:
  - ServiceArea: `AdminOnly`
  - Merchant: `AdminOnly` hoặc `MerchantOrAdmin`
  - Locker: `MerchantOrAdmin`
  - WashService: `MerchantOrAdmin`
  - WashOrder: tạo đơn bởi `Customer`, đổi trạng thái bởi `MerchantOrAdmin` / `ShipperOrAdmin`.
  - Thêm policy `CustomerOnly` nếu cần.
- [ ] Chuẩn hóa Auth/OTP cho production:
  - Bỏ mã OTP fallback `123456`.
  - Bỏ log mã OTP ra console.
  - Bỏ password fallback trong Login.
  - Tích hợp dịch vụ SMS / Email thực tế (Twilio / Stringee / AWS SES).
- [ ] Cấu hình chuẩn hóa global route prefix `/api/v1` cho toàn bộ endpoint nếu có nhu cầu đồng bộ versioning.

---

### 2. Cổng thanh toán trực tuyến (Online Payment Gateway)
- [ ] Tích hợp cổng thanh toán trực tuyến (VNPay / MoMo / ZaloPay):
  - API tạo URL thanh toán cho đơn hàng (`POST /payment/create-url`).
  - Xử lý Webhook / IPN callback từ phía cổng thanh toán để tự động cập nhật trạng thái đơn sang `PAID`.
- [ ] Xử lý hoàn tiền (`Refund`, `RefundRequest`):
  - Hoàn tiền khi đơn hàng gặp sự cố, hủy đơn trước khi giặt.
  - Phê duyệt yêu cầu hoàn tiền từ Admin.
- [ ] Tính và thu phí lưu kho quá hạn (`OverdueFee`):
  - Tính phí phát sinh khi khách hàng để đồ trong tủ quá thời gian quy định (sau X giờ tính thêm Y VNĐ/giờ).
  - Yêu cầu thanh toán phí phạt trước khi mở tủ nhận đồ.

---

### 3. Module Phân công giao nhận (Shipper & Delivery Assignment)
- [ ] Nghiệp vụ điều phối Shipper (`DeliveryAssignment`):
  - Phân công tự động hoặc thủ công đơn hàng cho Shipper lấy đồ từ Locker mang về xưởng Merchant.
  - Phân công Shipper mang đồ đã giặt sạch từ Merchant trả lại vào Locker cho khách.
- [ ] API & Luồng nghiệp vụ cho Shipper:
  - Shipper nhận chuyến, xác nhận đã lấy đồ tại tủ (mở tủ bằng mã Shipper).
  - Shipper giao đồ tới tiệm giặt và giao đồ hoàn trả lại tủ.
- [ ] Màn hình điều phối giao nhận trên Admin UI.

---

### 4. Module Khuyến mãi & Giảm giá (Promotions & Vouchers)
- [ ] Quản lý chương trình khuyến mãi (`Promotion`, `PromotionUsage`):
  - Tạo mã giảm giá theo %, theo số tiền cố định, giới hạn lượt dùng, thời gian hiệu lực.
  - Giảm giá cho đơn hàng đầu tiên của khách hàng mới.
- [ ] API áp dụng voucher khi tạo đơn hàng (`POST /wash-order/apply-voucher`).
- [ ] Giao diện quản lý khuyến mãi trên Admin UI.

---

### 5. Module Đánh giá & Xử lý sự cố (Rating & Damage Report)
- [ ] Đánh giá dịch vụ (`Rating`):
  - Khách hàng chấm điểm sao (1-5 sao) và nhận xét chất lượng giặt của Merchant sau khi hoàn thành đơn.
  - Hiển thị rating trung bình của đối tác trên hệ thống.
- [ ] Báo cáo sự cố đồ giặt (`DamageReport`):
  - Báo cáo đồ bị hư hỏng, rách, phai màu, thất lạc.
  - Tiếp nhận và giải quyết khiếu nại giữa Khách hàng và Merchant.
- [ ] Xử lý đồ tồn kho lưu trữ lâu ngày (`StorageItem`):
  - Thu gom đồ quá hạn không có người lấy về kho lưu trữ tập trung.

---

### 6. Module Đối soát doanh thu đối tác (Merchant Settlement)
- [ ] Quản lý đối soát & doanh thu (`MerchantSettlement`):
  - Tính toán tổng doanh thu định kỳ (hàng tuần/hàng tháng) cho từng Merchant.
  - Trừ hoa hồng nền tảng WashGo (Platform Fee %).
  - Tạo kỳ đối soát và xác nhận thanh toán chuyển khoản cho đối tác.
- [ ] Báo cáo thống kê tài chính trên Admin UI: Biểu đồ doanh thu, số lượng đơn giặt, tỷ lệ hoàn tất.

---

### 7. Module Thông báo tự động (Notification System)
- [ ] Tích hợp gửi thông báo đa kênh:
  - Thông báo đẩy (Firebase Cloud Messaging - FCM) về ứng dụng khách hàng.
  - Gửi SMS OTP Brandname hoặc Zalo ZNS khi gửi mã PIN mở tủ gửi đồ / nhận đồ.
  - Thông báo khi quần áo đã giặt xong và sẵn sàng để lấy.
- [ ] Mẫu thông báo (`NotificationTemplate`): Cấu hình nội dung tin nhắn tự động theo từng sự kiện đơn hàng.

---

### 8. Module Quản trị hệ thống nâng cao (Admin System Management)
- [ ] Nhật ký hệ thống (`AuditLog`, `QrLog`):
  - Ghi lại vết truy cập, thay đổi dữ liệu nhạy cảm của các tài khoản Admin/Merchant.
  - Lịch sử quét mã QR mở tủ tại các điểm đặt tủ.
- [ ] Cấu hình hệ thống động (`SystemConfig`):
  - Cấu hình phí giặt mặc định, thời gian giữ đồ miễn phí tại tủ, phí phạt quá hạn.
- [ ] Quản lý duyệt thay đổi dịch vụ (`ServiceChangeRequest`):
  - Luồng đối tác đề xuất tăng giá/thay đổi mô tả và Admin duyệt trước khi áp dụng ra thị trường.
- [ ] Quản lý hồ sơ Shipper: Duyệt hồ sơ giấy phép lái xe, duyệt đăng ký Shipper mới.

---

### 9. Ứng dụng Khách hàng (Customer App / Web Client)
- [ ] Ứng dụng di động (Flutter / React Native) hoặc Web Client cho khách hàng:
  - Bản đồ tìm kiếm tủ đồ gần nhất theo định vị GPS.
  - Đặt đơn giặt, chọn ô tủ, nhận mã PIN/mã QR mở tủ để gửi đồ.
  - Theo dõi tiến độ giặt quần áo theo thời gian thực (Real-time Timeline).
  - Thanh toán đơn hàng trực tuyến.
  - Nhận mã PIN mở tủ nhận lại quần áo sạch.

---

## PHỤ LỤC: QUY CHUẨN KỸ THUẬT & LỆNH XÁC MINH

### Lệnh Build & Kiểm tra
```bash
# Kiểm tra build backend
dotnet build WashGo.HttpApi.Host\WashGo.HttpApi.Host.csproj -m:1 -nr:false -v:minimal

# Kiểm tra migration pending
dotnet ef migrations has-pending-model-changes --project src\WashGo.EntityFrameworkCore\WashGo.EntityFrameworkCore.csproj --startup-project WashGo.HttpApi.Host\WashGo.HttpApi.Host.csproj --context WashGoDbContext --no-build

# Cập nhật database
dotnet ef database update --project src\WashGo.EntityFrameworkCore\WashGo.EntityFrameworkCore.csproj --startup-project WashGo.HttpApi.Host\WashGo.HttpApi.Host.csproj --context WashGoDbContext --no-build

# Build admin frontend
npm run build
```

### Quy ước thiết kế DB & Code
- **Database Schema**: Toàn bộ bảng nằm trong schema `washgo`.
- **Khóa chính**: `Guid` chuẩn hóa.
- **Tên bảng & Cột vật lý**: `snake_case` (theo thiết kế DB).
- **Tên Class & Property C#**: `PascalCase`.
- **Bảo mật**: Mật khẩu mã hóa BCrypt, không trả password hash/token/OTP trong response API.
