# Kế hoạch triển khai dự án WashGo (Backend & Admin)
Cập nhật lần cuối: 28/10/2026

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
    - Locker thứ hai cùng Merchant: `Trạm Locker Nhà thờ Đức Bà` (`LK_Q1_002`, Id: `a2222222-aaaa-2222-aaaa-222222222222`), có 2 ô; dùng để kiểm thử gửi và nhận ở hai tủ khác nhau. Seeder bỏ qua nếu ID đã tồn tại.
  - **Dịch vụ giặt**:
    - Giặt sấy sấy khô cao cấp (theo kg - `SVC_WASH_DRY`).
    - Giặt hấp giặt khô vest/đầm (theo chiếc - `SVC_DRY_CLEAN`).
    - Vệ sinh giày thể thao (theo đôi - `SVC_SHOES`).
  - Toàn bộ mật khẩu seed được mã hóa thực tế bằng **BCrypt** (`BCrypt.Net-Next`).

---

### 3. Xác thực & Phân quyền (Authentication & Authorization)
- [x] **Chuẩn hóa cấu trúc Auth Contracts**: Tách `AuthContracts.cs` thành `Commands/AuthCommands.cs`, `Results/AuthResultDtos.cs`, `Services/IAuthAppService.cs`; chuyển `IAuthEmailSender` nội bộ về `WashGo.Application/Features/AuthApplication/Services`. Giữ nguyên hợp đồng HTTP/API.
- [x] **Rà soát cấu trúc toàn bộ BE**: Quét 170 file C# gồm Host và migrations; tách contracts của `WashService`, `Merchant`, `Locker`, `ServiceArea`, `Customer` theo `Commands`, `Queries`, `Results`, `Services`; chuyển response DTO Payment về `Results`. Tách entity, EF configuration, repository, enum và constants đang gộp nhiều loại thành file riêng. Chuyển `IMesRepository` về `Core.Domain` để `WashGo.Domain` không phụ thuộc project chứa EF/ASP.NET; khai báo `SearchVector` trong EF bằng shadow property để Domain không phụ thuộc Npgsql. Kiểm tra cuối: không còn file nhiều namespace, trộn public interface/class, nhiều entity trong một file, nhiều EF configuration/repository trong một file hoặc namespace lệch thư mục. Build 0 lỗi/cảnh báo; EF model không khác migration cuối.
- [x] **Bảo mật mật khẩu**: Sử dụng BCrypt.Net-Next để hash/verify mật khẩu.
- [x] **JWT Token Generator**: Cấp JWT Access Token chứa các claim: `sub`, `email`, `role`, `user_id`.
- [x] **Refresh Token Rotation**: Cơ chế cấp mới Access Token qua Refresh Token và thu hồi (revoke) khi logout.
- [x] **Bảo vệ Refresh Token**: Chỉ lưu SHA-256 hash của Refresh Token trong database; logout chỉ được thu hồi token thuộc tài khoản đang đăng nhập.
- [x] **Xác thực OTP**: Quản lý mã OTP có hạn sử dụng, trạng thái đã dùng và đếm số lần nhập sai.
- [x] **API cho đăng ký Customer theo đặc tả/Figma**: UI thu thập họ tên, email, số điện thoại, mật khẩu và xác nhận mật khẩu; sau đó gọi `send-otp` → `verify-otp` trả `verificationToken` có hạn 10 phút → `register` nhận token cùng thông tin form. OTP không thể dùng lại sau khi xác minh; token được lưu dạng BCrypt trên bản ghi OTP hiện có. `register` trả ID/email và thông báo đăng nhập, không cấp phiên tự động. Quên mật khẩu dùng `send-otp` với purpose ForgotPassword rồi `reset-password` nhận OTP và mật khẩu mới.
- [x] **Kiểm tra form trước OTP theo use case**: `send-otp` với purpose Register yêu cầu họ tên, số điện thoại, mật khẩu và xác nhận mật khẩu; kiểm tra hợp lệ cùng email chưa tồn tại trước khi gửi OTP. Mật khẩu tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số. Purpose ForgotPassword vẫn chỉ cần email.
- [x] **Hoàn tất kiểm tra Auth theo đặc tả**: `register` bắt buộc xác nhận mật khẩu; `reset-password` bắt buộc xác nhận mật khẩu mới. JWT access token gắn với phiên lưu trong DB; logout thu hồi phiên hiện tại ngay cả khi body không có refresh token. Khi refresh token được xoay vòng hoặc reset mật khẩu, phiên cũ bị thu hồi và access token cũ bị từ chối ngay. URL avatar Google được mã hóa đúng dạng JSON trước khi lưu vào cột `jsonb` và trả lại dạng URL cho API.
- [ ] **Màn hình Customer theo Figma**: Chưa có frontend Customer trong `DO_AN` (hiện chỉ có `washgo-admin` và `washgo-be`). Khi triển khai app Customer, cần làm màn Đăng nhập, Đăng ký, Xác nhận OTP và Quên mật khẩu; sau đăng ký thành công điều hướng về Đăng nhập theo đặc tả.
- [x] **Hạn chế abuse Auth**: Áp dụng rate limit cho các API nhạy cảm: login, register, gửi OTP, reset password và Google login.
- [x] **Cấu hình RBAC & bảo vệ endpoint**:
  - Cấu hình JWT Bearer Authentication middleware trong ASP.NET Core.
  - Thiết lập các policy: `AdminOnly`, `MerchantOrAdmin`, `ShipperOrAdmin`, `CustomerOnly`, `OrderStatusUpdater`.
  - Gắn policy cho các endpoint ServiceArea, Merchant, Locker, WashService và WashOrder.
- [x] **Endpoints Auth (`/auth/*`)**:
  - `POST /auth/login`: Đăng nhập cấp JWT Token và Refresh Token.
  - `POST /auth/refresh-token`: Làm mới token.
  - `POST /auth/logout`: Thu hồi Refresh Token (`IsRevoked = true`).
  - `POST /auth/register`: Đăng ký tài khoản mới.
  - `POST /auth/send-otp`: Gửi OTP xác thực.
  - `POST /auth/verify-otp`: Kiểm tra mã OTP.
  - `POST /auth/reset-password`: Đặt lại mật khẩu mới.
  - `POST /auth/google`: Nhận và xác minh Google ID token, sau đó đăng nhập hoặc tạo tài khoản Customer mới.
  - `POST /auth/google/admin`: Google login cho trang quản trị; chỉ chấp nhận Admin/Merchant/Shipper đã liên kết, không tự tạo Customer.
  - `POST /auth/link-google`: Người dùng đã đăng nhập liên kết Google sau khi xác nhận lại mật khẩu hiện tại.
  - `GET /auth/me`: Lấy thông tin tài khoản hiện tại (yêu cầu Bearer Token).
- [x] **Harden public registration & OTP**:
  - Public register chỉ tạo role `Customer`, không còn nhận role từ client.
  - Đã loại bỏ password fallback trong Login và OTP fallback `123456`.
  - OTP được hash bằng BCrypt trước khi lưu và kiểm tra thời hạn/số lần nhập sai.
  - Có abstraction gửi email OTP qua SMTP; môi trường Development chỉ được phép in OTP ra console để test, API không trả OTP trong response.
- [x] **Google OAuth Backend (Customer & tài khoản quản trị đã cấp quyền)**:
  - Dùng Google ID token flow; backend xác minh chữ ký token và `audience` bằng Google Client ID của dự án WashGo.
  - Tài khoản Google mới được tạo với role `Customer` và customer profile tương ứng.
  - Google Customer với email đã có tài khoản Customer sẽ dùng lại tài khoản đó và liên kết Google đã xác minh email; không tạo trùng. Email thuộc role quản trị không đi qua `/auth/google`.
  - Admin/Merchant/Shipper không được tạo qua Google login. Họ phải có tài khoản WashGo đã được cấp quyền, đăng nhập mật khẩu, xác nhận lại mật khẩu và gọi `/auth/link-google` để liên kết Google.
  - Sau khi liên kết, Google login trả đúng role sẵn có; Google account không tự được nâng quyền.
  - Local Client ID đã cấu hình; Admin UI gọi riêng `/auth/google/admin`, nên Google account Customer mới không thể vào dashboard quản trị.
  - Docker backend/admin đã build/chạy và Swagger đã xác nhận routes `/auth/google`, `/auth/google/admin`, `/auth/link-google`.
- [ ] **Kiểm thử Google OAuth end-to-end**: Chờ Customer Web/Mobile Client lấy Google ID token thật rồi gọi API `/auth/google`.
- [ ] **Cấu hình SMTP/SMS production**: Điền credentials provider thật qua biến môi trường/secret store và kiểm thử gửi OTP thực tế.

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
- [x] **Bảo mật đơn hàng Customer**:
  - `POST /wash-order/` không còn nhận `CustomerId`, tên hoặc số điện thoại từ client; backend lấy Customer hiện tại từ JWT và hồ sơ người dùng.
  - `POST /wash-order/mine`: Customer chỉ lọc được các đơn thuộc chính mình.
  - `GET /wash-order/{id}`: Customer nhận `404` khi cố xem đơn không thuộc tài khoản của mình.
  - `POST /wash-order/{id}/cancel`: Customer chỉ hủy được đơn của mình khi còn `Pending`; backend giải phóng ô locker đang `Reserved` và ghi lịch sử hủy đơn.
  - API lọc đơn quản trị (`/wash-order/filter`) và lấy dữ liệu bộ lọc cột chỉ dành cho `OrderStatusUpdater`.

---

### 4.1. Hồ sơ Customer Backend
- [x] `GET /customer/me`: lấy thông tin từ `User` và `Customer` theo JWT, yêu cầu policy `CustomerOnly` và tài khoản đang hoạt động.
- [x] `PUT /customer/me`: sửa họ tên, điện thoại, địa chỉ, quận/huyện và tỉnh/thành phố. Không nhận ID, email, role, điểm thưởng hoặc tổng chi tiêu để cập nhật.
- [x] Validation: họ tên 2–100 ký tự, điện thoại 9–15 chữ số (chuẩn hóa bỏ dấu phân cách), địa chỉ tối đa 500 ký tự, quận/thành phố tối đa 100 ký tự. Trường tùy chọn bỏ trống hoặc không gửi sẽ được xóa về `null` theo ngữ nghĩa PUT.
- [x] Cập nhật `User` và `Customer` trong cùng transaction; không thay đổi schema và không cần migration.
- [x] Backend build thành công: 0 warning, 0 error.
- [x] Docker backend đã rebuild và smoke test runtime ngày 26/10/2026 đã PASS: `tests/smoke/customer-profile.ps1` kiểm tra lưu/đọc lại, đồng bộ `/auth/me`, validation không làm thay đổi hồ sơ, chuẩn hóa/xóa trường tùy chọn, chống sửa hồ sơ người khác và chống thay đổi email/role/số liệu qua JSON, quyền 401/403. Script tạo một tài khoản test riêng trên DB development và thu hồi các refresh token đã dùng sau test.
- [ ] Upload/cập nhật avatar chưa nằm trong đợt triển khai này.
- [ ] Unique số điện thoại chưa được áp dụng; cần thống nhất quy tắc và đồng bộ với luồng đăng ký trước khi bổ sung.

### 4.2. Danh mục đặt đơn dành cho Customer Backend
- [x] `GET /customer/services` (lọc `merchantId`, `serviceType`, `keyword`, phân trang) và `GET /customer/services/{id}`: chỉ trả dịch vụ đã duyệt, đang bán, thuộc Merchant đang hoạt động. DTO chỉ chứa thông tin hiển thị và giá, không lộ trường duyệt nội bộ.
- [x] `GET /customer/lockers` (lọc `city`, `district`, `keyword`, phân trang) và `GET /customer/lockers/{id}`: chỉ trả tủ đang hoạt động, thuộc Merchant đang hoạt động và thực sự còn ô `Available`; số ô trống tính từ trạng thái slot hiện tại thay vì trường đếm lưu sẵn. API chi tiết trả ID/số/kích cỡ ô trống, không trả PIN.
- [x] Cả bốn endpoint yêu cầu policy `CustomerOnly`; page/take được giới hạn an toàn. Không đổi schema, không cần migration.
- [x] Backend build sạch (0 warning, 0 error), Docker backend đã rebuild và smoke test ngày 26/10/2026: Customer đọc/lọc/phân trang/xem chi tiết thành công (3 dịch vụ, 1 tủ với 3 ô trống trên DB dev); anonymous 401, Admin 403.
- [ ] Client Customer chưa tích hợp các endpoint này; danh mục chỉ là ảnh chụp thời điểm truy vấn.

### 4.3. Củng cố tạo đơn Customer Backend
- [x] `POST /wash-order/` kiểm tra lại trạng thái tủ, ô, Merchant và tài khoản Customer tại thời điểm đặt; tủ trả đồ nếu khác tủ gửi phải đang hoạt động và thuộc cùng Merchant.
- [x] Chỉ nhận dịch vụ đang bán, đã duyệt, thuộc đúng Merchant của tủ. Yêu cầu 1–30 dịch vụ không trùng, số lượng/khối lượng hợp lệ, phương thức thanh toán hợp lệ; dịch vụ tính theo kg phải có khối lượng. Giá và tổng tiền lấy từ dữ liệu dịch vụ trên server, không tin giá từ client.
- [x] Giữ ô bằng `UPDATE` có điều kiện theo trạng thái `Available`; cập nhật bộ đếm ô và tạo order trong cùng transaction. Request thứ hai chọn cùng ô nhận 400 thay vì tạo thêm order. Không đổi schema, không cần migration.
- [x] Build backend sạch (0 warning, 0 error), Docker đã rebuild và test runtime ngày 26/10/2026: tạo/hủy đơn khôi phục số ô trống, hai request đồng thời cùng ô nhận 201/400, sáu payload sai đều nhận 400 và không giữ ô.
- [ ] Cần kiểm thử tải, thanh toán end-to-end và các thao tác quản trị locker đồng thời trước khi dùng production.

### 4.4. Hủy đơn Pending an toàn khi thao tác đồng thời
- [x] Customer `POST /wash-order/{id}/cancel` chỉ đổi đơn từ `Pending` sang `Cancelled` bằng cập nhật có điều kiện, rồi giải phóng đúng ô đang `Reserved`; trạng thái đơn, trạng thái ô, bộ đếm ô và lịch sử hủy cùng một transaction. Nếu một bước không thành công, transaction không commit.
- [x] Nhánh quản trị `PUT /wash-order/status` đổi `Pending → Cancelled` cũng giải phóng ô; nhánh `Pending → Deposited` chỉ nhận ô đang `Reserved`. Các chuyển trạng thái qua endpoint này dùng cập nhật có điều kiện để không ghi đè trạng thái vừa được request khác thay đổi.
- [x] Build sạch (0 warning, 0 error), Docker backend đã rebuild và test ngày 26/10/2026: hủy Customer hai lần đồng thời trả 200/400; hủy Customer và Admin đồng thời trả 200/400; hủy Admin đơn Pending thành công; các case kết thúc `Cancelled` và số ô trống trở lại 3.
- [ ] Cần kiểm thử tải và các tương tác quản trị đồng thời trước production.

### 4.5. Đồng bộ ô gửi và ô trả trong luồng đơn
- [x] `Deposited → Collected` giải phóng ô gửi bằng cập nhật có điều kiện `Occupied → Available`, không ghi đè một ô đã đổi trạng thái.
- [x] `Paid → ReadyToReturn` chọn ô `Available` ở `ReturnLockerId` (hoặc tủ gửi nếu không chọn tủ khác), giữ ô có điều kiện rồi chuyển `Occupied` trong cùng transaction; lưu `ReturnBoxId`/`ReturnBoxNumber`. Nếu tủ trả hết ô, đơn giữ nguyên `Paid`.
- [x] API chi tiết/danh sách đơn trả `ReturnLockerId`, `ReturnBoxId`, `ReturnBoxNumber` để client hiển thị đúng vị trí lấy đồ.
- [x] `ReadyToReturn → Returned` chỉ giải phóng ô trả; `Returned → Completed` không giải phóng lần hai. Chuyển kho quá hạn dùng ô trả và cập nhật đơn/ô trong cùng transaction; hỗ trợ đơn cũ chưa có `ReturnBoxId` bằng cách dùng ô gửi như luồng cũ.
- [x] Build sạch và Docker đã rebuild ngày 26/10/2026. Smoke test: ô gửi bị đơn khác giữ nhưng đồ trả được xếp sang ô khác; `Returned → Completed` trả đủ 3 ô. Khi cả 3 ô bị giữ, `ReadyToReturn` trả 400 và đơn vẫn `Paid`; sau khi ô được giải phóng, đơn hoàn tất và tủ trở lại 3 ô trống.
- [x] Đã kiểm thử thực tế nhánh quá hạn và tủ trả khác tủ gửi ở mục 4.8 dưới đây.

### 4.6. Bảo vệ ô tủ khỏi thao tác quản trị trực tiếp
- [x] `PUT /locker/box-status` chặn đổi trạng thái ô đang gắn với đơn `Pending`/`Deposited` hoặc là ô trả của đơn `ReadyToReturn` (kể cả đơn cũ chưa lưu `ReturnBoxId`). Kiểm tra ràng buộc và đổi trạng thái ô trong một cập nhật có điều kiện; cập nhật bộ đếm ô cùng transaction.
- [x] Ô không gắn đơn vẫn được chuyển sang bảo trì và mở lại; enum trạng thái không hợp lệ bị chặn.
- [x] Build sạch và Docker backend đã rebuild ngày 26/10/2026. Smoke test: ép ô đang giữ đơn/chứa đồ/đang chờ khách nhận sang `Available` đều trả 400; ô rảnh đổi `Maintenance → Available` thành công và số ô trống trở lại 3.
- [ ] Các thao tác quản trị và đặt/trả đơn đồng thời vẫn cần kiểm thử tải thêm trước production.

### 4.7. Bảo vệ trạng thái cả tủ
- [x] `PUT /locker/` chặn đưa tủ sang `Maintenance`/`Inactive` khi còn ô `Reserved`/`Occupied` hoặc đơn `Pending`/`Deposited`/`ReadyToReturn` liên quan. Kiểm tra và cập nhật trạng thái tủ trong một lệnh có điều kiện, cùng transaction.
- [x] Sửa thông tin tủ chỉ cập nhật những trường metadata cần sửa, không ghi đè `AvailableBoxes` vừa được đơn hàng cập nhật; trạng thái enum không hợp lệ bị chặn.
- [x] Build sạch và Docker backend đã rebuild ngày 26/10/2026. Smoke test: tủ có đơn `Pending` hoặc `ReadyToReturn` không thể chuyển bảo trì (400); sửa metadata khi có đơn vẫn giữ đúng bộ đếm ô; sau khi đơn kết thúc có thể chuyển bảo trì rồi mở lại, tủ còn đủ 3 ô trống.
- [ ] Vẫn cần bài test chạy đồng thời giữa cập nhật cả tủ và tạo/trả đơn.

### 4.8. Kiểm thử nhiều tủ và chuyển kho quá hạn
- [x] Seeder bổ sung tủ demo thứ hai cùng Merchant, 2 ô, ID cố định; Docker restart không tạo trùng (catalog vẫn có đúng 2 tủ).
- [x] Đơn gửi ở `LK_Q1_001` và nhận tại `LK_Q1_002` chạy đến `Completed`: tủ gửi trở lại 3 ô sau khi Shipper lấy đồ, tủ trả giảm 2→1 khi đồ nằm trong ô rồi trở lại 2 sau khi Customer nhận.
- [x] Dùng một đơn smoke riêng ở DB dev, thử chuyển kho trước hạn nhận bị 400; sau đó chỉ chỉnh `pickup_deadline` của đúng đơn này về 73 giờ trước để mô phỏng quá hạn. `POST /wash-order/move-to-storage` chuyển đơn sang `MovedToStorage`, tính phí 180.000đ, giải phóng đúng ô trả ở tủ thứ hai; gọi chuyển kho lần hai bị 400.
- [x] Khi chưa thanh toán phí lưu kho, `Returned` bị 400. Customer tạo giao dịch phí lưu kho sandbox 180.000đ, Admin xác nhận callback mock; sau đó đơn chuyển `Returned → Completed`, ghi nhận `StorageFeeTotal = 180.000đ`. Đây là kiểm thử sandbox, không phải giao dịch VNPay thật.
- [ ] Chưa kiểm thử VNPay thật và race condition giữa chuyển kho với khách nhận đồ trong tải đồng thời; cần trước production. Callback phí đồng thời đã được xử lý và smoke test ở mục 4.9.

### 4.9. Chống ghi nhận trùng phí lưu kho
- [x] Tạo URL phí lưu kho khóa đơn trong transaction trước khi kiểm tra giao dịch `Pending`, tránh hai request đồng thời tạo hai giao dịch chờ cho cùng đơn.
- [x] Callback sandbox và VNPay IPN khóa bản ghi giao dịch; khi ghi nhận phí thì khóa thêm đơn, kiểm tra số phí còn thiếu trên dữ liệu mới nhất, cộng tiền và đổi trạng thái giao dịch trong cùng transaction. Callback lặp không cộng phí lần hai. Không đổi schema, không cần migration.
- [x] Build sạch, Docker backend đã rebuild ngày 26/10/2026. Smoke test sandbox: hai lệnh tạo URL đồng thời trả 200/400; hai callback cùng mã đồng thời đều trả 200 nhưng chỉ cộng `StorageFeeTotal` đúng một lần 10.000đ. Đơn test sau đó chuyển `Returned → Completed` thành công. Luồng thanh toán tiền giặt thông thường qua callback mock cũng đã test hồi quy đến `Completed`.
- [ ] Chưa kiểm thử IPN VNPay thật với chữ ký từ provider và các giao dịch khác mã cùng đơn trong tải lớn; cần trước production.

### 5. Giao diện Quản trị Web (Admin UI - `washgo-admin`)
- [x] **Màn hình Đăng nhập / Đăng xuất (Login/Logout)**:
  - Form đăng nhập kết nối trực tiếp API Backend (`/auth/login`).
  - Đã có logoutApi; UI logout gọi `/auth/logout` để revoke refresh token rồi xóa session local.
  - Lưu trữ Token và User Info vào Redux store + LocalStorage.
  - Tự động đính kèm `Bearer <token>` vào request header qua Axios Interceptor.
  - Điều hướng tự động (Auth Guard / Protected Routes).
  - Có nút Google Sign-In gọi `/auth/google/admin`; chỉ tài khoản Admin/Merchant/Shipper đã liên kết mới đăng nhập được.
  - Có hộp thoại liên kết Google trong thanh tài khoản, bắt buộc nhập lại mật khẩu hiện tại trước khi chọn Google account.
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
- [x] Docker stack đã rebuild và chạy: PostgreSQL, Backend (`http://localhost:5000`) và Admin (`http://localhost:5173`). Swagger đã xác nhận route Customer Order mới; đăng nhập Customer seed và gọi `POST /wash-order/mine` thành công, còn `POST /wash-order/filter` bị chặn `403` đúng policy.

---

## PHẦN II: CÁC NỘI DUNG CHƯA HOÀN THÀNH / KẾ HOẠCH TIẾP THEO (PENDING / ROADMAP)

### 1. Phân quyền Endpoint chặt chẽ (RBAC Enforce) & Chuẩn hóa Auth Production
- [x] Gắn Policy `.RequireAuthorization(...)` chi tiết cho từng nhóm endpoint backend:
  - ServiceArea: `AdminOnly`.
  - Merchant: `AdminOnly`.
  - Locker và WashService: `MerchantOrAdmin`.
  - WashOrder: danh sách quản trị và bộ lọc cột dành cho `Admin`, `Merchant`, `Shipper`; Customer dùng `POST /wash-order/mine` và chỉ xem được chi tiết đơn của chính mình. Tạo/hủy đơn bởi `Customer`; cập nhật trạng thái bởi `Admin`, `Merchant`, `Shipper`; phân công shipper bởi `Admin`.
  - Đã thêm policy `CustomerOnly` và `OrderStatusUpdater`.
- [x] Loại bỏ password fallback trong Login và OTP fallback `123456`; OTP chỉ được in console khi chạy Development để hỗ trợ test.
- [x] Public register chỉ được tạo Customer; Refresh Token lưu hash và logout kiểm tra đúng token owner.
- [x] Google OAuth: Customer mới được tạo tự động; Admin/Merchant/Shipper phải liên kết Google sau khi đăng nhập mật khẩu và xác nhận lại mật khẩu.
- [x] Tích hợp Google Sign-In vào Admin UI, có màn hình liên kết Google riêng và không tự nâng quyền Google account.
- [ ] Tích hợp Google Sign-In ở Customer Web/Mobile Client và kiểm thử end-to-end với Google ID token thật.
- [ ] Tích hợp dịch vụ SMTP/SMS thực tế (Twilio / Stringee / AWS SES) và cấu hình secret production.
- [ ] Cấu hình chuẩn hóa global route prefix `/api/v1` cho toàn bộ endpoint nếu có nhu cầu đồng bộ versioning.

---

### 2. Cổng thanh toán trực tuyến (Online Payment Gateway)
- [x] Payment sandbox foundation:
  - `POST /payment/create-url`: tạo payment URL cho VNPay sandbox hoặc URL giả lập theo cấu hình.
  - `POST /payment/callback`: callback sandbox yêu cầu Admin, chuyển đơn sang `PAID` qua business rule/order timeline.
  - Từ chối tạo URL cho phương thức tiền mặt và xử lý callback idempotent khi đơn đã `PAID`.
  - Lưu lịch sử giao dịch vào bảng `payment_transactions`, unique transaction reference và trạng thái `Pending/Succeeded/Failed/Refunded`.
- [ ] Tích hợp cổng thanh toán thật (VNPay):
  - [x] Tạo URL VNPay 2.1.0, ký HMAC-SHA512 và lấy cấu hình qua `Payment:VnPay`.
  - [x] `GET /payment/vnpay-ipn`: xác minh chữ ký, merchant, số tiền và trạng thái trước khi cập nhật đơn.
  - [ ] Cấu hình credential merchant, IPN HTTPS public URL tại VNPay Portal và kiểm thử end-to-end trên VNPay Sandbox.
  - [ ] Chưa bật provider `VnPay` trên môi trường nhóm đến khi cấu hình secrets/IPN URL an toàn.
- [x] Nền tảng yêu cầu hoàn tiền (`RefundRequest`):
  - `POST /payment/refund-requests` để Customer gửi yêu cầu cho đơn đã thanh toán; chỉ chủ đơn được tạo/xem yêu cầu của mình qua `GET /payment/refund-requests/mine`.
  - Admin dùng `GET /payment/refund-requests` để xem danh sách và `POST /payment/refund-requests/{id}/review` để duyệt/từ chối.
  - Hoàn tiền hiện là thủ công ngoài hệ thống; sau khi thực hiện, Admin gọi `POST /payment/refund-requests/{id}/confirm-refunded` kèm mã tham chiếu.
  - Khi xác nhận, cập nhật yêu cầu, giao dịch gốc và PaymentStatus của đơn thành `Refunded`.
  - Migration `AddRefundRequests` đã áp dụng vào database local.
- [ ] Tích hợp hoàn tiền tự động qua cổng VNPay/MoMo/ZaloPay (hiện chưa gọi API chuyển tiền thực tế).
- [x] Nền tảng phí lưu kho quá hạn (`OverdueFee`):
  - Mốc bắt đầu là khi đơn chuyển sang `ReadyToReturn` (đồ đã được đưa vào locker trả); hiện giả định 24 giờ đầu miễn phí.
  - Sau hạn miễn phí, mỗi 24 giờ bắt đầu tính tròn thành một ngày: ngày quá hạn 1/2/3 lần lượt 10.000/20.000/50.000đ; từ ngày thứ 4 là 100.000đ/ngày. Cách làm tròn này cần được xác nhận theo quy định nghiệp vụ cuối cùng.
  - Customer dùng `POST /payment/overdue-fee/create-url` để tạo giao dịch VNPay cho phí còn thiếu; IPN/callback phân biệt giao dịch phí lưu kho với tiền giặt, không đổi trạng thái thanh toán tiền giặt.
  - Chặn chuyển đơn sang `Returned` (khách đã lấy đồ theo flow status hiện tại) nếu chưa trả đủ phí đã phát sinh.
  - Migration `AddOverdueStorageFeePayments` đã áp dụng vào database local để phân biệt giao dịch tiền giặt và giao dịch phí lưu kho.
  - Sau hơn 72 giờ quá hạn, `POST /wash-order/move-to-storage` dành cho Shipper/Admin ghi nhận vị trí và mã lưu kho, cập nhật trạng thái `MovedToStorage`, thêm timeline và giải phóng ô locker.
  - Admin có form ghi nhận thao tác này trên màn hình chi tiết đơn. Người vận hành vẫn phải di chuyển đồ thực tế trước khi bấm xác nhận; hệ thống chưa kết nối thiết bị/kho tự động.
  - Đơn `MovedToStorage` vẫn có thể thanh toán phần phí phát sinh; khách chỉ chuyển sang `Returned` sau khi phí đã trả đủ.
  - Migration `AddOverdueStorageTransfer` đã áp dụng vào database local.

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
  - Tích hợp nút Google Sign-In, lấy Google ID token và gọi `POST /auth/google` (backend đã hoàn tất; Admin UI đã có luồng riêng cho tài khoản được cấp quyền).
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

# Chạy toàn bộ hệ thống bằng Docker Compose (PostgreSQL + Backend + Admin UI)
docker compose up -d --build

# Xem log các container
docker compose logs -f

# Dừng hệ thống
docker compose down
```

### Quy ước thiết kế DB & Code
- **Database Schema**: Toàn bộ bảng nằm trong schema `washgo`.
- **Khóa chính**: `Guid` chuẩn hóa.
- **Tên bảng & Cột vật lý**: `snake_case` (theo thiết kế DB).
- **Tên Class & Property C#**: `PascalCase`.
- **Bảo mật**: Mật khẩu mã hóa BCrypt, không trả password hash/token/OTP trong response API.
