# Bàn giao FE — mã lỗi tài khoản bị khóa/đã xóa và tra cứu tạo Merchant/Shipper

Ngày 21/11/2026. Thực hiện `Docs/cần làm/BACEND_ADMIN.md`. Envelope giữ nguyên `{ succeeded, statusCode, message, meta: { code } }`; `message` vẫn là câu tiếng Việt cũ nên FE cũ không bị ảnh hưởng.

## 1. Đăng nhập, Google và làm mới phiên (HTTP 400)

| Tình huống | `meta.code` |
| --- | --- |
| Email không tồn tại, sai mật khẩu, dữ liệu trống | `InvalidCredentials` (kể cả khi tài khoản đó đang bị khóa/đã xóa) |
| Đúng mật khẩu (hoặc Google hợp lệ) nhưng tài khoản bị khóa | `AccountLocked` — Shipper: "...liên hệ Merchant", vai trò khác: "...liên hệ Admin" |
| Đúng mật khẩu của tài khoản đã xóa (email chưa có tài khoản mới) | `AccountDeleted` |
| Google cổng quản trị khớp tài khoản đã xóa | `AccountDeleted` |
| Refresh token không tồn tại/đã thu hồi/hết hạn | `InvalidRefreshToken` (không cho biết tài khoản có tồn tại hay bị khóa) |
| Refresh token **còn hiệu lực** nhưng tài khoản bị khóa/xóa | `AccountLocked` / `AccountDeleted` |

Không trả lý do khóa, người khóa hay dữ liệu nội bộ. Khóa/xóa vẫn thu hồi mọi phiên, nên sau khi khóa, refresh token cũ trả `InvalidRefreshToken`; FE biết tài khoản bị khóa qua request kế tiếp có access token (mục 2).

## 2. Request có access token (HTTP 401/403)

| Tình huống | HTTP | `meta.code` |
| --- | --- | --- |
| Không gửi token | 401 | `Unauthenticated` |
| Token sai định dạng/chữ ký | 401 | `InvalidToken` |
| Access token hết hạn (làm mới bằng refresh token) | 401 | `TokenExpired` |
| Tài khoản của token đã bị khóa | 401 | `AccountLocked` |
| Tài khoản của token đã bị xóa | 401 | `AccountDeleted` |
| Phiên bị đăng xuất/thu hồi (tài khoản vẫn hoạt động, kể cả sau khi được mở khóa lại) | 401 | `SessionRevoked` |
| Phiên hết hạn | 401 | `SessionExpired` |
| Đăng nhập rồi nhưng không đủ quyền | 403 | `Forbidden` |

Trạng thái khóa/xóa lấy từ chính tài khoản của token có chữ ký hợp lệ, không suy ra từ việc phiên bị thu hồi. FE chỉ coi là tài khoản bị khóa khi nhận đúng `AccountLocked`/`AccountDeleted`; lỗi mạng hoặc 403 `Forbidden` không phải khóa. Áp dụng cho cả kết nối SignalR `/hubs/notifications` (negotiate trả cùng mã).

## 3. Tra cứu tạo Merchant/Shipper từ trang tài khoản

- `POST /admin/accounts` với `role = Merchant|Shipper` và `operationId` giờ được ghi vào module Account: khóa được giữ ở trạng thái Processing trước khi gọi dịch vụ tạo đối tác (dịch vụ này dùng chính `operationId` làm `clientRequestId`), xong thì lưu kết quả.
- `GET /admin/accounts/operations/{operationId}` (chỉ người gửi): `state = Processing` (chưa có kết quả, `completedAt = null`) hoặc `Completed` với `result` là response đã lưu (`result.data.id` = account ID). Nếu tài khoản đã được tạo nhưng response bị mất, lookup tự nhận ra tài khoản đã commit và trả Completed. 404 = chưa có kết quả của bạn (Admin khác cũng nhận 404).
- Gửi lại cùng `operationId` + cùng payload: trả đúng kết quả cũ, không tạo thêm tài khoản/gửi lại thư; đang Processing thì gửi lại an toàn. Cùng khóa, payload khác → 409 `OperationIdReused`. Không cần (và không nên) tạo lại bằng khóa mới khi chưa biết kết quả.
- Kết quả không chứa mật khẩu, OTP, token, giấy tờ.
