# Duyệt đánh giá và quyền riêng tư đơn hàng — 13/11/2026

Phạm vi cụm: Admin contact/Publish, bộ lọc đánh giá Merchant, response đơn/biên lai theo vai trò và thông báo lưu kho. Không bao gồm self-status Shipper hoặc quy trình chuyển kho/giao lại mới. Kết quả chạy cuối xem `../IMPLEMENTATION_CHECKLIST.md`.

## 1. Admin duyệt nội dung

`GET /rating/{id}` và `POST /rating/filter` chỉ dành cho Admin, bổ sung:

- `customerAvatarUrl`, `customerEmail`, `customerPhoneNumber`.
- `merchantImageUrls` (ảnh cửa hàng), `merchantEmail`, `merchantPhone`.
- `contactBasis`, `contactRetrievedAt`: thông tin hồ sơ hiện tại, nạp theo lô; thiếu hồ sơ trả null/[]; không nằm trong version. `customerName`/`merchantName` vẫn là snapshot của đánh giá.
- `contentApprovedAt`, `contentApprovedBy`, `canPublish`, `publishReasonCode` (`HiddenRating`/`AlreadyPublished`/null).

Sau khi Admin kiểm tra nhận xét và media, gọi:

```http
POST /rating/{id}/publish
Authorization: Bearer <Admin access token>
```

```json
{
  "expectedVersion": "<version từ GET chi tiết mới nhất>",
  "operationId": "<UUID mới cho thao tác>",
  "reason": "Đã kiểm tra nhận xét và ảnh/video"
}
```

- `expectedVersion` bắt buộc, lệch trả 409 kèm dữ liệu mới nhất; tải lại trước khi quyết định tiếp.
- `reason` tùy chọn, tối đa 500 ký tự. FE nên nhắc Admin kiểm tra PII trong cả ảnh/video; BE không tự chứng nhận nội dung sạch bằng regex.
- Đang ẩn: 400 `HiddenRating`. Hiện lại rồi mới Publish nếu chưa được duyệt.
- Đã duyệt với version hiện tại: 200, `meta.outcome=Unchanged`.
- Cùng operationId/nội dung trả kết quả đã ghi, không ghi audit lần hai; cùng ID khác nội dung trả 409.
- Tra cứu: `GET /admin-operations/Rating/{operationId}`. Kết quả ghi là snapshot thời điểm xử lý; GET chi tiết là trạng thái hiện tại.

`POST /rating/batch` thêm `action="Publish"`, dùng cùng danh sách `items=[{id, expectedVersion}]`, operationId bắt buộc, tối đa 50 ID khác nhau. Giữ outcome/totals và lookup hiện có; từng item là giao dịch độc lập.

## 2. Publish khác Hide/Show

| Thao tác | Visibility | Phê duyệt nội dung | Điểm Merchant |
|---|---|---|---|
| Rating mới | Hiển thị sao | Chưa duyệt | Tính sao như trước |
| Publish | Giữ nguyên | Ghi người/thời điểm duyệt | Không đổi |
| Hide | Ẩn | Giữ lại phê duyệt cũ nếu có | Loại sao khỏi aggregate |
| Show | Hiển thị lại | Không tự duyệt | Đưa sao vào aggregate |

Merchant chỉ nhận comment/ảnh/video khi `!isHidden && contentApprovedAt != null`. Keyword được lọc cùng điều kiện **trước Count/paging**. Sao của đánh giá chưa duyệt vẫn hiện theo hợp đồng cũ, nhưng không suy ra được comment qua keyword/count. Customer/Partner DTO không có contact hay reviewer nội bộ.

Migration `20261113094800_AddRatingContentApproval` thêm hai trường nullable. Backfill chỉ các bản không xóa, đang hiển thị và có cả `moderatedAt`/`moderatedBy`; bản hidden hoặc thiếu bằng chứng không tự duyệt. Kiểm tra DB local trước migration: tổng 1, legacy released 0. Không đổi số sao, visibility hoặc tổng điểm khi migrate.

## 3. Response đơn và biên lai

| Vai trò | Được giữ | Được che |
|---|---|---|
| Customer chủ đơn | Mã đơn WashGo, dịch vụ/giá/ETA, locker, mã gửi/nhận của mình | ID/tên/liên hệ/ảnh Merchant/Shipper, mã tiệm, vị trí/mã kho, task/actor nhân sự, hoa hồng |
| Merchant/Shipper đúng phạm vi | Dữ liệu vận hành và liên hệ nội bộ theo quyền hiện có | CustomerId/name/phone/avatar, ghi chú khách/dòng dịch vụ, lý do hủy và free text lịch sử; mã gửi/nhận Customer |
| Admin | Định danh/liên hệ và lịch sử nội bộ | Mã mở tủ trong response thông thường; dùng endpoint access-codes có audit |

`OrderResponsePrivacy` áp dụng sau enrich cho danh sách/chi tiết, khi tạo/hủy/đổi trạng thái/giao nhận, conflict/replay. Snapshot Shipper cũ cũng được che khi đọc, không sửa hàng loạt ledger. Lịch sử ngoài Admin giữ trạng thái/thời gian/vai trò, bỏ note tự do; actor phía bên kia thay bằng WashGo/Khách hàng.

Customer không được lọc/sort theo thông tin nhân sự đã che, kể cả qua `columnFilters`; Merchant/Shipper không được suy luận contact Customer qua filter/distinct. Mã tiệm không còn là từ khóa tìm đơn Customer.

Biên lai Customer ghi bên cung cấp nền tảng là `WashGo`, bỏ mã/địa chỉ/liên hệ tiệm. Merchant không nhận tên/contact người mua. JSON và PDF dùng cùng DTO đã che; số biên lai và tiền không đổi. Đây vẫn là biên lai nội bộ, không tự bổ sung thông tin pháp lý/thuế chưa được xác minh.

## 4. Thông báo

Thông báo order của Customer chỉ giữ các argument công khai cần thiết; bỏ storageLocation/storageReference/reason và identity nội bộ. Merchant/Shipper cũng không nhận reason do Customer nhập hoặc customer identity trong argument thông báo order; vẫn giữ địa chỉ/mã lưu kho cho nhiệm vụ nội bộ theo quyền. Áp dụng trước lưu/push SignalR và lúc đọc inbox/mark-read để bảo vệ bản legacy.

Thông báo lưu kho dùng `messageKey=storage.customer.private`, nội dung trung lập WashGo và link vào đơn; không mời khách đến tiệm/kho. **FE cần thêm bản dịch key này hoặc dùng `content` làm fallback.** Đây không phải xác nhận API giao lại đã được triển khai.

Keyword trên thông báo order của Customer chỉ tìm title; không tìm nội dung legacy đang bị che để tránh rò qua tổng kết quả. Các nhóm khác giữ cách tìm cũ. Không thể thu hồi email/file/notification mà thiết bị đã tải trước bản sửa này.

## 5. Kiểm chứng và phần tiếp theo

- `tests/smoke/Privacy`: projection ba vai trò, mã Customer được giữ, nested PII, snapshot operation legacy và notification kho.
- `tests/smoke/rating-keyword-privacy.ps1`: pending/approved/hidden/deleted/foreign, keyword/count/paging.
- `tests/smoke/review-publishing.ps1`: contacts/snapshot, quyền HTTP, list/detail/conflict/receipt/inbox, Publish/Hide/Show, replay/lookup/batch/limit, race và aggregate, missing profile; fixture riêng và cleanup.
- CI Regression chạy Privacy console và hai suite HTTP trên Compose/PostgreSQL riêng, sau vòng đời đơn để có dữ liệu nền. Giữ rate limit thật; script chỉ chờ và thử lại riêng login khi chạm giới hạn chung IP.

Self-status, vòng lưu kho tại tiệm, bàn giao/giao lại, SLA/phí theo vòng và đồng bộ Word/điều khoản/AI theo luồng kho mới còn ở các chặng C–G của plan. Không ghi nhận toàn bộ plan đã hoàn tất.

## 6. Commit/push cụm này

Nhánh đã tách: `feat/toantran/review-publishing-privacy`, base là bản vá CI `04bb072`. Merge PR #106 vào develop trước PR cụm này nếu chưa merge; không push trực tiếp vào develop/main. Chưa có commit mới do agent tạo.

Tại thư mục `washgo-be`, sau khi xem diff:

```bash
git add ../.github/workflows/backend-ci.yml src
git add tests/ci/Run-ApiSmoke.ps1 tests/smoke/Privacy tests/smoke/rating-keyword-privacy.ps1 tests/smoke/review-publishing.ps1
git add IMPLEMENTATION_CHECKLIST.md NEXT_IMPLEMENTATION_PLAN.md docs/NEW_BACKEND_REQUIREMENTS_PLAN_20261113.md docs/REVIEW_PUBLISHING_PRIVACY_API.md docs/BACKEND_CI_GUIDE.md
git --no-pager diff --cached --stat
git diff --cached --check
git commit -m "feat(reviews): add content publishing and enforce order privacy"
git push -u origin feat/toantran/review-publishing-privacy
```

Danh sách này không có `.env`, backup, log/bin/obj, FE hoặc thay đổi kho chưa triển khai. Các tài liệu plan/checklist đã có thay đổi trước cụm này được giữ và cập nhật, không discard. Sau push, chờ run CI mới trước khi merge PR.
