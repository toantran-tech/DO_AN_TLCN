# Bàn giao FE — danh mục dịch vụ động

Ngày 16/11/2026. Source đã bổ sung; HTTP regression đã nối CI nhưng **chưa chạy** trong đợt này. Cần áp migration `20261116120715_AddOrderEventsStorageReferencesAndCategories` trước khi dùng API.

## Quy tắc chung

- Danh mục một cấp, UUID do server cấp. `categoryId` không phải enum `serviceType`.
- `serviceType` vẫn quyết định quy trình giặt/ETA. Thêm danh mục thứ sáu không cần thêm enum.
- `code` duy nhất, 2–30 ký tự Latin/số/`_`/`-`, bất biến sau tạo; mã đã soft delete không dùng lại.
- `name` 1–100 ký tự, chuẩn hóa Unicode NFC/khoảng trắng; duy nhất không phân biệt hoa thường. `description` tối đa 500, không HTML. `sortOrder` 0–100000.
- `isActive=false` khóa bán mới của mọi dịch vụ con nhưng giữ nguyên cờ `isActive` riêng của dịch vụ. Mở danh mục không mở dịch vụ đang khóa riêng.
- Đơn đã tạo giữ snapshot tên/mã/UUID danh mục và loại xử lý; đổi danh mục không tính lại giá hoặc tiến độ đơn cũ. Dữ liệu lịch sử chưa có snapshot để `null`, không gán thông tin hiện tại thành lịch sử.

## Lookup

| API GET | Quyền | Dữ liệu |
| --- | --- | --- |
| `/customer/service-categories` | Guest/Customer | Danh mục active; đếm dịch vụ active, approved, giá > 0 |
| `/merchant/service-categories` | Merchant | Danh mục active để chọn khi đề xuất |
| `/admin/lookups/service-categories` | Admin | Có thể thêm `includeInactive=true` |

Query: `keyword`, `page` từ 1, `take` 1–100. Thứ tự mặc định sortOrder/name/id. Response phân trang theo cấu trúc chung của dự án. Mỗi dòng có `id`, `code`, `name`, `description`, `isActive`, `sortOrder`, `legacyServiceType` nếu thuộc nhóm seed. Các capability/version/đếm proposal dùng cho Admin; FE không tự suy ra quyền từ UUID.

Năm seed tương thích: StandardWash=1, DryClean=2, IronOnly=3, BlanketWash=4, ShoeCare=5. **4 không phải Combo**. Lấy UUID qua lookup, không hardcode trên FE.

## CRUD Admin

| Method | Route | Hành vi |
| --- | --- | --- |
| POST | `/admin/service-categories/filter` | Body keyword/isActive/page/take/sortBy/isDescending |
| GET | `/admin/service-categories/{id}` | Chi tiết, version, serviceCount/pendingProposalCount và capability |
| POST | `/admin/service-categories/` | Tạo |
| PUT | `/admin/service-categories/{id}` | Sửa |
| PUT | `/admin/service-categories/{id}/status` | Khóa/mở |
| POST | `/admin/service-categories/{id}/delete` | Soft delete, chặn khi còn dịch vụ chưa xóa hoặc proposal pending |

Body tạo/sửa:

```json
{
  "operationId": "<UUID mới cho thao tác>",
  "expectedVersion": "<version từ GET, bỏ khi tạo>",
  "code": "PREMIUM_CARE",
  "name": "Chăm sóc đặc biệt",
  "description": "Dịch vụ chăm sóc theo yêu cầu",
  "isActive": true,
  "sortOrder": 10,
  "reason": "Bổ sung nhóm dịch vụ"
}
```

Body status/delete: `operationId`, `expectedVersion`, `isActive`, `reason`; delete gửi `isActive=false` (trường này bắt buộc trong contract dùng chung).

Filter `sortBy=sortOrder|name|code`, mặc định sortOrder; `isDescending` mặc định false. Tối đa 100 kết quả mỗi trang.

Write phải dùng version đang xem. Replay cùng key/payload trả kết quả cũ; key khác payload hoặc version stale trả 409. No-op không tăng version/ghi audit thay đổi giả. Lỗi có reasonCode; CategoryInUse kèm dữ liệu ảnh hưởng để FE giải thích. Sau 409 tải lại chi tiết và cho người dùng quyết định thao tác mới, không tự lấy version mới rồi ghi đè.

## Batch và phục hồi sau timeout

POST `/admin/service-categories/batch/status` hoặc `/batch/delete`:

```json
{
  "operationId": "<UUID của toàn batch>",
  "isActive": false,
  "reason": "Tạm khóa nhóm dịch vụ",
  "items": [
    { "id": "<UUID danh mục>", "expectedVersion": "<version>" }
  ]
}
```

1–100 ID, không trùng. Status cần isActive đích. Mỗi item commit độc lập, có status/outcome/reason/version và dependencyCounts khi có dữ liệu chi tiết. Không coi toàn batch thành công chỉ vì HTTP 200; đọc kết quả từng item.

GET `/admin/service-categories/operations/{operationId}` chỉ cho Admin tạo operation. Nếu Processing, giữ nguyên key/payload và gửi lại POST để tiếp tục các item chưa có kết quả. Kết quả đã commit được giữ, kể cả item thất bại. Muốn thử lại item lỗi sau khi sửa dữ liệu phải tạo operation mới. GET chỉ đọc và phục hồi kết quả child đã commit, không thực thi lại batch.

## Nối dịch vụ, proposal và catalog

- Tạo/sửa dịch vụ và Merchant proposal thêm `categoryId`. Tạo thiếu UUID còn tương thích bằng nhóm seed của `serviceType`; update thiếu UUID giữ danh mục hiện tại. FE mới nên luôn gửi UUID lấy từ lookup.
- Duyệt proposal kiểm tra lại danh mục còn active trong transaction. Mở lại danh mục không tự duyệt proposal. Raw JSON đề xuất cũ được giữ nguyên; DTO trả `categoryMappingBasis` để phân biệt Captured/LegacyServiceTypeMapping/LegacyInvalid.
- DTO dịch vụ staff có categoryId/categoryName/categoryCode/categoryIsActive/effectiveIsActive/blockedReason. Không dùng riêng service.isActive để kết luận đang bán.
- POST `/customer/services/filter`, Admin `/wash-service/filter` và bảng dịch vụ Merchant nhận `categoryIds: ["uuid", "uuid"]`, tối đa 100. OR trong mảng, AND với keyword, các khoảng giá và ServiceTypes nếu cùng gửi.
- Catalog công khai/detail và tạo đơn bằng direct ID đều chặn danh mục inactive/deleted. Chatbot đọc cùng catalog và có tên/mã danh mục trong dữ liệu dịch vụ. Quote thanh toán cho đơn đã tồn tại tiếp tục dựa trên số tiền đã chốt, không khóa vì danh mục hiện tại.

## Migration và xác minh

Migration preflight dừng nếu có service hoặc proposal pending mang loại xử lý legacy ngoài 1–5; cần sửa dữ liệu có chủ đích trước khi chạy lại. Migration seed/backfill FK cho dịch vụ, giữ proposal JSON và snapshot lịch sử null. Down bị chặn khi đã có bằng chứng nghiệp vụ mới để tránh mất lịch sử.

Test nối vào `tests/ci/Run-ApiSmoke.ps1`: `WorkflowMetadata` và `service-categories-workflow.ps1`, cùng các regression catalog/lifecycle/storage đang có. Chưa có kết quả PASS cho bản source này; FE tích hợp sau khi CI/migration thành công.
