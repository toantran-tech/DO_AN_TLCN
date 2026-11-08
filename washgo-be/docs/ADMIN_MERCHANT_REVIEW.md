# Kiểm thử Admin Merchant

Ngày 09/11/2026. Swagger: http://localhost:5000/swagger/index.html.

## Hợp đồng version và gửi lại an toàn

GET `/merchant/{id}` và list trả `version` dạng chuỗi. Giữ nguyên chuỗi, không chuyển sang Number trong JavaScript. PUT `/merchant/` và PUT `/merchant/status` bắt buộc gửi `expectedVersion` vừa đọc: thiếu trả 400 ExpectedVersionRequired, lệch trả 409 VersionMismatch kèm hồ sơ mới nhất.

Ví dụ body khóa, thay ID và version bằng dữ liệu thật:

```json
{
  "merchantId": "<merchant-id>",
  "status": 3,
  "rejectionReason": "Lý do khóa",
  "confirm": false,
  "expectedVersion": "<version-từ-GET>",
  "operationId": "<GUID-mới>"
}
```

operationId tùy chọn nhưng nên gửi khi ghi. Sau timeout, giữ nguyên key và toàn bộ payload, kể cả expectedVersion. Gửi lại cùng yêu cầu trả response gốc, không thực hiện lại. Cùng key khác payload/đối tượng/thao tác trả 409 OperationIdReused. Replay trả snapshot cũ, không phải trạng thái hiện tại; dùng GET nếu cần dữ liệu mới nhất.

Tra cứu GET `/merchant/operations/{operationId}`: AdminOnly, chỉ thao tác do chính Admin thực hiện. Response có operationId, merchantId, operation, createdAt và result (response gốc). Chỉ thao tác đã commit mới có kết quả; 404 không chứng minh request đang chạy đã thất bại. Có thể gửi lại cùng operationId và payload. Hiện hỗ trợ Update/Status/ApproveProfile/RejectProfile; chưa hỗ trợ tạo Merchant.

## Duyệt hồ sơ

GET/list yêu cầu hồ sơ có `version` chuỗi; `current.baseMerchantVersion` là phiên bản hồ sơ lúc gửi, null với legacy.

- Duyệt: POST `/merchant-profile-request/{id}/approve?expectedVersion=<version-yêu-cầu>&operationId=<guid>`. Không cần body. expectedVersion bắt buộc; operationId tùy chọn.
- Từ chối: POST `/merchant-profile-request/{id}/reject` với body reason, expectedVersion bắt buộc và operationId tùy chọn.
- Yêu cầu đã xử lý: 409 AlreadyReviewed; version cũ: 409 VersionMismatch. Replay đúng operationId vẫn trả kết quả thành công ban đầu.
- Hồ sơ Merchant đổi từ khi gửi: duyệt trả 409 MerchantProfileChanged, không áp snapshot cũ. Từ chối yêu cầu và gửi đề xuất mới; không ép duyệt bằng cách chỉ đổi expectedVersion.
- Sửa/khóa/gửi yêu cầu/duyệt/từ chối dùng cùng khóa Merchant với phân công đơn, kiểm tra version trong transaction. Legacy chưa có base version thì đối chiếu các trường snapshot hiện có trước khi duyệt.

Migration `20261108181756_AddMerchantWriteOperations` đã áp dụng DB; model không thiếu migration. Tạo tài khoản/idempotency tạo/email delivery status còn nằm trong checklist.

## Thống kê

Đăng nhập Admin, Authorize, gọi `GET /merchant/summary`. Kỳ vọng 200 với total, active, inactive, suspended, pendingProfileRequests và generatedAt UTC. Không gửi bộ lọc bảng. Merchant/Customer gọi bị 403; không đăng nhập bị 401.

## Sửa hồ sơ và khóa tài khoản

Lấy Merchant bằng GET /merchant/{id}, dùng dữ liệu hiện tại để điền PUT /merchant/. Giữ đủ thông tin command đang có trên Swagger.

- Không gửi status: sửa thông tin, giữ trạng thái hiện tại.
- Gửi status đúng trạng thái hiện tại: vẫn sửa được để tương thích client cũ.
- Gửi status khác hiện tại: 400, meta.code=UseMerchantStatusEndpoint; không thay hồ sơ hoặc trạng thái.
- Dùng PUT /merchant/status để khóa/mở theo quy tắc hiện có: lý do khóa bắt buộc; nếu còn đơn chưa xử lý phải xác nhận. Không dùng dữ liệu thật để thử khóa tài khoản đang vận hành.

## Giá trị và phân vùng

- commissionRate=12.345 hoặc depositAmount=1.001: 400; 0% hợp lệ.
- city/district dài 101 ký tự: 400.
- serviceAreaIds không gửi hoặc null: giữ link hiện có.
- Gửi lại cùng ID vùng: giữ priority/activation/thời điểm/người gán và ID link; không đặt về mặc định.
- []: gỡ toàn bộ vùng; danh sách khác chỉ gỡ vùng bị bỏ/thêm vùng mới. Đây không phải API chỉnh priority.

## Chạy tự động

Tại thư mục washgo-be, với Docker development đang chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/smoke/merchant-admin-profile-guards.ps1
```

Script dùng fixture SQL local, không gọi endpoint gửi thông tin đăng nhập. Dọn fixture và thu hồi phiên trong finally. Đợt 2 ngày 09/11 PASS: concurrent save và approve/reject 200/409, stale/missing version, operation replay/lookup/hash conflict, không trùng audit, chặn stale approval, replay duyệt/từ chối. Hồi quy merchant-profile và merchant-profile-legal cũng PASS.
