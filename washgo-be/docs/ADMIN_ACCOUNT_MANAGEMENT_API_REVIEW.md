# Bàn giao FE — Quản lý tài khoản trong Admin

Ngày 21/11/2026. Thực hiện `Docs/cần làm/BACKEND_ADMIN_ACCOUNT_MANAGEMENT.md`. Mọi API dưới `/admin/accounts` cần đăng nhập Admin (`AdminOnly`), dùng envelope chuẩn `{ succeeded, statusCode, message, data, errors, meta }`; lỗi có `meta.code` ổn định và `errors.<trường>` để hiện đúng ô.

## Quyền

| Quyền | Ai có | Dùng cho |
| --- | --- | --- |
| `Accounts.Read/Create/Update/SetStatus/SoftDelete` | Mọi Admin đang hoạt động | Customer, Merchant, Shipper |
| `Accounts.ManageAdmins` | Cấp riêng | Tạo/sửa/khóa/xóa/cấp quyền tài khoản **Admin** (kèm quyền hành động tương ứng) |
| `Accounts.ReadDeleted` | Cấp riêng | Xem tài khoản đã xóa (`deleted=Include/Only`, chi tiết, nhật ký) |
| `Accounts.Restore` | Cấp riêng | Khôi phục tài khoản đã xóa |

- Migration cấp 3 quyền riêng cho **Admin đang hoạt động được tạo sớm nhất** (DB local/seed: `admin@washgo.vn`). Admin mới tạo không có quyền riêng nào; cấp qua `PUT /admin/accounts/{id}/permissions`.
- Thao tác trên tài khoản Admin cần **xác thực lại** trong 10 phút: `POST /auth/reauthenticate { password }` → `{ reauthenticatedUntil, validForSeconds }`. Chưa xác thực → 403 `ReauthenticationRequired` (FE mở hộp nhập mật khẩu rồi gửi lại cùng request). Sai mật khẩu → 400 `WrongPassword` (`errors.password`), 5 lần/15 phút → 429.
- BE luôn chặn: tự khóa/tự xóa/tự đổi quyền (`CannotModifySelf`), tài khoản được bảo vệ `isProtected` (`ProtectedAccount`, chỉ đặt bằng DB), làm mất Admin hoạt động cuối cùng có `ManageAdmins` (`LastPrivilegedAdmin`, an toàn cả khi hai Admin thao tác cùng lúc). Không đổi role ở bất kỳ API nào.

## Đọc

`GET /admin/accounts?page&take&keyword&role&isActive&createdFrom&createdTo&deleted&sortBy&sortDirection`

- `take` 1–100 (mặc định 20); trả `{ items, total, page, take, hasMore }`. `createdFrom/To` là unix giây.
- `role`: Customer|Merchant|Shipper|Admin. `deleted`: Exclude (mặc định) | Include | Only (cần `ReadDeleted`, thiếu → 403).
- `sortBy`: createdAt (mặc định, desc) | lastLoginAt | fullName (mặc định asc) | deletedAt (chỉ khi xem đã xóa). `lastLoginAt`/`deletedAt` null luôn ở cuối. Giá trị khác → 400 `ValidationFailed` (`errors.sortBy`).
- Mỗi dòng: `id, revision, avatarUrl (null = avatar mặc định), fullName, email, phone, role, isActive, isDeleted, deletedAt, isProtected, createdAt, updatedAt, lastLoginAt, signInMethod (Password|Google|PasswordAndGoogle), hasPassword, googleLinked, partner, capabilities`. Thời gian là UTC.
- `partner`: Merchant → `merchantId, businessName, merchantStatus`; Shipper → `shipperId, shipperMerchantId, shipperMerchantName, shipperDeleted`; kèm `credentialDelivery/credentialSentAt` của hồ sơ đối tác.
- `capabilities`: `canEdit, canChangeEmail, canLock, canUnlock, canDelete, canRestore, canManagePermissions, requiresReauthentication, blocked[{action, code, message}]`. Trên danh sách **không** tính nghĩa vụ nghiệp vụ; chi tiết và preview tính đầy đủ. BE vẫn kiểm tra lại khi thực thi.

`GET /admin/accounts/summary` → `{ total, active, locked, deleted (null nếu không có ReadDeleted), byRole: { Customer: {total, active, locked, deleted}, ... } }` (tài khoản chưa xóa, một truy vấn).

`GET /admin/accounts/{id}` → thêm `statusChangedAt/statusChangedBy{id, fullName}/statusReason` (lần khóa/mở gần nhất), `deletedBy, deleteReason`, `credentialDelivery` (tài khoản tạo từ trang này), `permissions` (Admin), `obligations`:

| Trường | Ý nghĩa |
| --- | --- |
| `openOrders` | Đơn chưa kết thúc (kể cả đơn hủy còn đồ ở locker/tiệm) |
| `pendingPayments`, `openRefunds`, `openIncidents` | Thanh toán chờ, hoàn tiền Pending/Approved, khiếu nại/bồi thường chưa xong |
| `unsettledSettlements`, `activeShippers`, `lockerAssignments` | Merchant: đối soát Pending, Shipper chưa xóa, locker đang phụ trách |
| `activeTasks` | Shipper: nhiệm vụ lấy/trả chưa xong |
| `loyaltyPoints`, `heldLoyaltyPoints` | Điểm Customer (điểm đang giữ hoặc âm chặn xóa) |
| `deleteBlockers[]`, `items[]` | Mã nghĩa vụ chặn xóa; tối đa 20 đơn/nhiệm vụ cần bàn giao `{type, orderId, orderCode, status}` |

`GET /admin/accounts/{id}/activities?page&take` → audit phân trang (trang tài khoản, đăng nhập, trang Merchant/Shipper).

DTO không bao giờ chứa mật khẩu, hash, token, OTP, provider subject, giấy tờ, ngân hàng.

## Ghi (mọi thao tác ghi)

- `operationId` (GUID) chống tạo/ghi trùng: gửi lại cùng khóa + cùng payload trả đúng kết quả cũ; cùng khóa khác payload → 409 `OperationIdReused`. Timeout/mất mạng: tra `GET /admin/accounts/operations/{operationId}`, **không** chạy lại bằng khóa mới.
- `expectedRevision` (từ `revision`) hoặc header `If-Match: "12"` là bắt buộc khi sửa/khóa/xóa/khôi phục/cấp quyền/xác nhận đổi email; lệch → 409 `RevisionConflict` kèm bản mới nhất trong `data`. Đăng nhập của người dùng không làm đổi `revision`.
- Đã ở trạng thái đích → 200 `meta.outcome = "Unchanged"`.

| API | Ghi chú |
| --- | --- |
| `POST /admin/accounts` | `role`: Customer/Admin gửi `fullName, email, phone, avatarUrl?`; Merchant/Shipper gửi `merchant`/`shipper` (y hệt payload POST /merchant, POST /shipper). Mật khẩu tạm gửi email sau commit; xem `credentialDelivery` (0 chưa rõ, 1 đã gửi, 2 thất bại), lỗi email không tạo trùng tài khoản. Admin cần `ManageAdmins` + xác thực lại. |
| `PATCH /admin/accounts/{id}` | `fullName, phone, avatarUrl` (`""` = xóa ảnh; ảnh phải do chính Admin upload qua /files/images). Gửi `email` khác → 400 `UseEmailChangeFlow`. Shipper: SĐT trùng → 409 `PhoneAlreadyInUse`. Không sửa thông tin cửa hàng của Merchant. |
| `POST /admin/accounts/{id}/status` | `{ isActive, reason (bắt buộc khi khóa), confirm }`. Khóa thu hồi mọi phiên, access token hết hiệu lực ngay request sau, đóng kết nối realtime. Customer/Merchant còn đơn → 409 `ConfirmationRequired` (`meta.openOrders`), gửi lại `confirm: true`. Shipper còn nhiệm vụ → 409 `HasActiveTasks`. Mở: phiên cũ không sống lại; Merchant đang tạm ngưng → 409 `PartnerNotActive` (mở tại trang Merchant); Shipper đã xóa hồ sơ → 409 `PartnerProfileDeleted`. Không đổi trạng thái kinh doanh/nhận việc. |
| `POST /admin/accounts/{id}/soft-delete` | `{ reason }`. Còn nghĩa vụ → 409 `AccountHasOpenObligations` (`meta.blockers`, `data.obligations`); có thể khóa trước rồi bàn giao, sau đó mới xóa. `deletedAt` là giờ server, xóa lại không đổi. Merchant đang hoạt động chuyển Ngừng hoạt động; Shipper xóa hồ sơ; Admin mất quyền riêng. Email/Google được giải phóng cho tài khoản **mới** (ID mới, không kế thừa gì). |
| `POST /admin/accounts/{id}/restore` | Cần `Restore`; chỉ Customer/Admin (`RestoreNotSupportedForPartner`). Email/Google đã có tài khoản khác dùng → 409 `IdentityAlreadyInUse`. Khôi phục về trạng thái **khóa**. |
| `PUT /admin/accounts/{id}/permissions` | `{ permissions: [...], reason }` — đặt toàn bộ quyền riêng của một Admin. |
| `POST /admin/accounts/{id}/email-change` | `{ newEmail }` → gửi OTP (10 phút) tới email mới; trùng → 409 `EmailAlreadyInUse` (`errors.newEmail`); gửi lại sau 60 giây (429 `OtpThrottled`); gửi thư lỗi → 503 `EmailDeliveryFailed`. |
| `POST /admin/accounts/{id}/email-change/confirm` | `{ newEmail, code }` — chủ email mới đọc mã cho Admin. Sai mã → 400 (`errors.code`, còn số lần thử). Liên kết Google giữ nguyên. |

## Hàng loạt

- `POST /admin/accounts/bulk/preview { action: Lock|Unlock|SoftDelete, ids: [≤100] }` → mỗi dòng `found, fullName, avatarUrl, role, isActive, isDeleted, revision, alreadyInTargetState, canApply, requiresConfirmation, requiresReauthentication, reasonCodes[], openOrders, activeTasks, deleteBlockers`. Không giữ chỗ.
- `POST /admin/accounts/bulk { operationId, action, items: [{id, expectedRevision}], reason, confirm }` → mỗi tài khoản một transaction, áp dụng đúng luật như thao tác đơn lẻ. Kết quả `items[{ id, outcome: Changed|Unchanged|Skipped|Failed, reasonCode, statusCode, message, version (revision mới), isActive }]` và `totals{changed, unchanged, skipped, failed, succeeded}`. Thất bại một dòng không làm hỏng dòng khác.

## Mã lỗi

`ValidationFailed`, `EmailAlreadyInUse`, `PhoneAlreadyInUse`, `IdentityAlreadyInUse`, `UseEmailChangeFlow`, `RevisionConflict`, `OperationIdReused`, `PermissionRequired`, `ManageAdminsRequired`, `ReauthenticationRequired`, `CannotModifySelf`, `ProtectedAccount`, `LastPrivilegedAdmin`, `AccountAlreadyDeleted`, `AccountNotDeleted`, `AccountHasOpenObligations`, `ConfirmationRequired`, `HasActiveTasks`, `PartnerNotActive`, `PartnerProfileDeleted`, `RestoreNotSupportedForPartner`, `NotAdminAccount`, `OtpThrottled`, `EmailDeliveryFailed`, `TooManyAttempts`. 401 hết phiên, 403 thiếu quyền, 404 không có/không trong phạm vi.

## Thay đổi ở luồng hiện có

- Tài khoản đã xóa mềm không đăng nhập (mật khẩu, Google), không làm mới token, không đặt lại mật khẩu; OTP/challenge cũ bị vô hiệu khi xóa; OTP quên mật khẩu chỉ dùng cho đúng tài khoản đã nhận mã.
- Email không phân biệt hoa thường/khoảng trắng; tài khoản bị khóa (chưa xóa) vẫn giữ email. Đăng ký trùng đồng thời: một thành công, request còn lại 409 `EmailAlreadyInUse`.
- Xóa Shipper ở trang Shipper giờ xóa mềm luôn tài khoản đăng nhập (giải phóng email). Khóa/mở ở trang Merchant/Shipper dùng chung cơ chế khóa tài khoản (ghi người/lý do, đóng realtime). Merchant có tài khoản bị khóa/xóa không được gợi ý phân công đơn mới.

## Dữ liệu và triển khai

Migration `AdminAccountManagement`: thêm cột xóa mềm/khóa/bảo vệ/revision cho `users`, bảng `user_permissions`, cột `reauthenticated_at` cho phiên; chuẩn hóa email về chữ thường (dừng và không đổi gì nếu có email chỉ khác hoa thường); thay unique email/Google toàn bảng bằng unique trên tài khoản chưa xóa; tài khoản của Shipper đã xóa trước đây được đánh dấu đã xóa (vẫn khóa, không kích hoạt lại); cấp quyền riêng cho Admin hoạt động sớm nhất. Không xóa cứng, không có job archive/ẩn danh (cần thiết kế riêng). FE mới cần BE này.
