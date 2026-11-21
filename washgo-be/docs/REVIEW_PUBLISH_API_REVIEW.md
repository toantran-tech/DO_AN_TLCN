# Bàn giao FE — Duyệt và công khai đánh giá

Ngày 21/11/2026. Thực hiện `Docs/cần làm/BACKEND_REVIEW.md`.

## Một phạm vi công khai

- Công khai là **một** quyết định cho mọi người xem: Merchant, Customer và khách chưa đăng nhập. `POST /rating/{id}/publish` với `publishForCustomers` hoặc `publishForGuests` (gửi cả hai như FE đang làm) đặt cùng lúc `contentApprovedAt`, `publicApprovedAt`, `guestApprovedAt` trong một transaction. Không cờ nào = chỉ duyệt nội dung cho Merchant (giữ cho client cũ).
- Danh sách/summary/ảnh public (`GET /customer/reviews`, `/summary`, `/{publicId}/images/{index}`) dùng **cùng điều kiện** cho Customer và khách: không xóa, không ẩn, đã duyệt nội dung, đã công khai (`publicApprovedAt`), có `publicId`, sao 1–5, đơn hoàn tất. DTO public giữ ẩn danh như cũ.
- Ẩn/Hiện lại giữ nguyên các mốc duyệt; không cần duyệt lại.
- Dữ liệu cũ: migration `SyncGuestReviewPublication` chép `publicApprovedAt/By` sang `guestApprovedAt/By` cho đánh giá đã công khai cho Customer trước đây, nên chúng tự hiện với khách. Đánh giá chỉ mới duyệt nội dung cho Merchant **không** bị công khai. `version` của các đánh giá được chép mốc sẽ đổi một lần (FE tải lại nếu nhận 409 `VersionMismatch`).

## Hàng loạt

- `POST /rating/batch` thêm action **`PublishPublic`** = Duyệt và công khai (mọi phạm vi) cho 1–50 đánh giá, mỗi dòng `{ id, expectedVersion }`. Tương đương `action = Publish` kèm `publishForCustomers`/`publishForGuests = true`.
- `Publish` không cờ giữ nghĩa cũ (chỉ duyệt nội dung cho Merchant); operation cũ replay đúng kết quả đã lưu, không mở rộng phạm vi.
- Kết quả từng dòng `outcome` Changed/Unchanged/Skipped/Failed + `reasonCode` (ví dụ `VersionMismatch`, `HiddenRating`), `version` mới, `totals`. Cùng `operationId` + cùng payload trả kết quả cũ; cùng khóa khác action → 409 `OperationIdReused`. Tra cứu `GET /admin-operations/Rating/{operationId}`. Mỗi dòng ghi audit như thao tác đơn lẻ.
