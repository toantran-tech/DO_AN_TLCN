# Checklist hoàn thiện Backend WashGo

Cập nhật: 09/11/2026. Nguồn: 13 tài liệu `BACKEND_*.md` tại `C:/Users/PC/Desktop/HK1_2027/cần làm`. Đây là checklist chính trong BE. Hướng dẫn hợp đồng API: [ADMIN_COMPLETION_REVIEW.md](docs/ADMIN_COMPLETION_REVIEW.md).

Dấu [x] xác nhận phần cốt lõi đã triển khai và có kiểm thử dưới đây; không bao gồm những mở rộng ghi riêng ở cuối. Không coi có endpoint hoặc có script là đã kiểm thử thành công. Đợt hiện tại đã chia commit thành 4 nhánh nối tiếp, chưa push. Xem [GIT_PUSH_PLAN.md](docs/GIT_PUSH_PLAN.md).

## 1. Các phần đã có trước đợt này

- [x] Báo cáo Merchant: tổng hợp DB, coverage hai kỳ, CSV tạm tính và lifetime.
- [x] Đơn Merchant/Admin: scope, ảnh/liên hệ, ghi chú, operation/revision, phân công, chuyển kho, summary và ẩn mã quản trị.
- [x] Dashboard/hồ sơ Admin, bảo mật mật khẩu/phiên, audit chung; chưa có 2FA.
- [x] Dashboard Merchant snapshot, SLA phân trang/UTC; hồ sơ MST/giấy phép chuỗi và cặp tọa độ.

## 2. Merchant và tài liệu riêng tư

- [x] Hồ sơ/status/duyệt có version bắt buộc, khóa và operation replay/lookup. Tạo Merchant có clientRequestId, khóa email/MST, unique constraint được ánh xạ 409.
- [x] Tạo account/profile/operation/audit nguyên tử; trạng thái gửi mật khẩu Unknown/Sent/Failed lưu riêng. Retry không gửi lại mật khẩu; không lưu/trả mật khẩu tạm. Sent chỉ nghĩa là SMTP chấp nhận.
- [x] Summary snapshot, nhiều tiêu chí sort whitelist + Id, ngày tạo, publicRatingCount và totalRevenueBasis.
- [x] Upload PDF/DOC/DOCX tối đa 10 MB, kiểm tra cấu trúc, metadata, quyền gán/xem/tải và volume riêng.
- [x] File ID theo loại; null giữ nguyên, gỡ tường minh; đọc được dữ liệu chuỗi legacy. Admin quản lý SLA/hợp đồng; Merchant chỉ xem/tải và đề xuất giấy phép qua duyệt.
- [x] Gửi đề xuất có replay/khóa chống hai Pending. Giữ file lịch sử kể cả đề xuất bị từ chối; bản nháp chưa gán có TTL 24 giờ.

## 3. Finance Admin

- [x] Giao dịch/hoàn lưu Merchant và tỷ lệ snapshot. Hoàn khác kỳ đảo hoa hồng ở kỳ hoàn, không sửa kỳ đã trả; không cắt tiền hoàn bằng tổng thu trong kỳ B.
- [x] Quote và quoteToken; khóa kỳ chồng lấn, biên UTC gồm hai đầu. Chặn kỳ thực nhận 0/âm hoặc thiếu tỷ lệ để đối chiếu.
- [x] Tạo/chi trả/review refund/confirm refund có version, operation replay và lookup. Chứng từ khác sau xác nhận trả 409; tổng hoàn không vượt số được hoàn, partial refund vẫn giữ Paid.
- [x] Chi tiết/snapshot dòng đối soát, ID giao dịch và refund nguồn cho kỳ mới; refund filter/paging tại DB; thông tin liên hệ Admin theo batch; CSV chống công thức và metadata/basis rõ.
- [x] Dòng đối soát legacy không có snapshot được đánh dấu; không gọi số tính lại là lịch sử đã chốt.

## 4. Cài đặt hệ thống

- [x] Missing field không biến thành 0; version/transaction singleton; operation replay/lookup; nguồn default/DB, audit và history phân trang.
- [x] Validation range/precision/order có field errors. Đơn mới snapshot chính sách SLA/phí; đơn legacy ghi rõ migration-baseline thay vì suy ra lịch sử không tồn tại.
- [x] Đổi cấu hình không thay hạn/chính sách đơn cũ. Bồi thường lưu tỷ lệ và version cấu hình khi xử lý.

## 5. Locker Admin và Merchant

- [x] CRUD/status/bảo trì có version, operation, audit, validation và CanDelete theo dữ liệu thực.
- [x] Khóa locker trước ô; tạo đơn/reserve/giao nhận và xóa trạm dùng cùng cơ chế, không xóa khi còn đồ đơn hủy.
- [x] Lịch sử gửi/trả query/count/page ở PostgreSQL, giữ scope và usageId.
- [x] Batch 1–50 ID với kết quả từng ID; lưu intent và key thao tác con để retry sau mất kết nối, hỗ trợ partial success/replay/lookup.

## 6. Dịch vụ Admin

- [x] Ảnh list/detail, ownership ảnh mới, null giữ/[] gỡ.
- [x] Version/operation CRUD và duyệt; khóa catalog đồng bộ với tạo đơn, chặn ghi dịch vụ đã xóa mềm.
- [x] Validation, summary, thứ tự trang ổn định và batch trạng thái/xóa 1–50 ID.

## 7. Promotion Admin

- [x] Status đích thay toggle; version/operation/lookup; khóa đồng bộ update/delete với consume/release khi tạo/hủy đơn.
- [x] Giữ usage/history; không xóa voucher từng có usage kể cả đã revert. CanDelete của list/detail dựa trên history.
- [x] Validation ngày/enum/precision, summary và thống kê tổng/ngày bằng SQL.
- [x] Batch trạng thái/xóa 1–100 ID, partial success, audit/replay.

## 8. Khiếu nại và upload

- [x] Revision bao gồm thread/minh chứng; operation cho xử lý/từ chối/message/chi trả; chứng từ khác bị chặn.
- [x] Snapshot cấu hình bồi thường, precision, thông báo cùng transaction; audit tạo/sửa/rút khiếu nại.
- [x] Summary/filter theo quan hệ và compensationStatus; sort whitelist + Id; enrich liên hệ hiện tại theo batch, chỉ Admin thấy PII bổ sung.
- [x] Messages phân trang; registry upload 24 giờ, attach và cleanup cùng khóa/transaction; file đã tham chiếu giữ lịch sử, legacy không tự xóa.

## 9. Kết quả kiểm thử thực tế

| Script/project | Kết quả và phạm vi |
| --- | --- |
| merchant-admin-profile-guards / merchant-create-safety | PASS đợt trước: version/concurrency, email/MST, replay và delivery bằng Mailpit nội bộ. Không gửi email ngoài khi test. |
| merchant-private-documents.ps1 | PASS: upload/quyền/gán/gỡ, đề xuất giấy phép, lịch sử, TTL. |
| PrivateDocuments console | PASS: PDF/DOC/DOCX thật, chặn file giả/sai phần mở rộng, DOC mã hóa, DTD/macro DOCX. |
| system-setting-write-guards.ps1 | PASS: required, 0%, stale version, concurrent 200/409, replay/lookup/history; khôi phục cấu hình. |
| order-policy-snapshot.ps1 | PASS: đơn cũ giữ hạn/chính sách; đơn mới nhận cấu hình 45 phút và SLA 6/36 giờ; khôi phục cấu hình. |
| finance-write-guards.ps1 / finance.ps1 | PASS: quote, kỳ trùng, partial/cumulative refund, hoàn khác kỳ âm, snapshot kỳ không đổi, scope/chứng từ/CSV. |
| locker-admin.ps1 | PASS: validation, ảnh, sửa/bảo trì, usage theo ô, chặn xóa còn đồ và soft delete. |
| service-proposals.ps1 | PASS: đề xuất/duyệt/từ chối, scope, catalog dùng chung, CRUD và chặn xóa khi đơn chưa xong. |
| promotions.ps1 | PASS: CRUD/filter/quote, giới hạn usage đồng thời, hủy trả lượt, phí trước giảm giá, thống kê/CSV. |
| admin-resource-guards.ps1 | PASS: batch dịch vụ partial success/replay/lookup; status version; incident relation/compensation filter/sort, thread/payout và PII theo quyền. |
| upload-lifecycle.ps1 | PASS: expired attach trả 400 và rollback; orphan được dọn; file từng tham chiếu được giữ. |
| actual-weight.ps1 / order-cancellation.ps1 | PASS: cân/chốt giá và vòng đời bình thường; phí hủy, giữ đồ, nhận lại và hoàn trừ phí. |
| qr-pin-flow.ps1 | PASS lượt cuối: chặn đường tắt, lockout, phân công hai chuyến, QR/PIN, nhận hai bước, replay, phí và nhận tại kho không giải phóng ô lần hai. |

- [x] Build .NET thành công; còn 5 nullable warnings cũ, không có compile error.
- [x] EF kiểm tra không có thay đổi model chưa tạo migration.
- [x] Cả 6 migration mới đã có trong migration history PostgreSQL; model sạch. Docker build/recreate thành công, volume giấy tờ riêng được gắn; Swagger HTTP 200. Đã khôi phục compose chính, `Payment__Provider=Live` (sandbox chính thức theo `.env`), không giữ Mock sau hồi quy.

## 10. Giới hạn và mở rộng chưa triển khai

- [ ] 2FA, cấu hình hẹn hiệu lực, dự báo AI; assignee/reopen/SLA riêng cho khiếu nại và payout record/ảnh chứng từ riêng.
- [ ] Ngày hiệu lực hợp đồng có cấu trúc và bộ lọc tương ứng. Bộ lọc Merchant hiện dùng ngày tạo, không suy ngày từ file hợp đồng.
- [x] Dòng đối soát đã có snapshot được count/phân trang trong PostgreSQL; không tải toàn bộ JSON về app ở endpoint lines. Report/CSV đồng bộ giới hạn 25.000 bản ghi cho từng nguồn thu, hoàn và dòng dịch vụ; vượt giới hạn trả 413 `ReportTooLarge`, không cắt số liệu rồi trả báo cáo thiếu. Snapshot legacy thiếu ID giao dịch vẫn trả mảng rỗng.
- [ ] Export job bất đồng bộ/ledger quy mô lớn chưa triển khai. Kỳ legacy chưa có snapshot vẫn dùng breakdown tính lại có giới hạn; không gọi đó là snapshot bất biến.
- [ ] Tự động chuyển khoản ngân hàng/hoàn qua API ngân hàng và tự bù trừ kỳ âm; hiện ghi nhận chứng từ thủ công, kỳ âm/thiếu snapshot bị chặn để đối chiếu.
- [ ] Thu nhập thực thu theo từng dịch vụ: ByService hiện là subtotal trước phân bổ giảm giá/phụ phí/hoàn, có basis mô tả. CSV là bản mới tại thời điểm export, không đảm bảo tái xuất màn hình cũ.
- [x] Sort Promotion nhiều tiêu chí: Code/CreatedOn/StartAt/EndAt/UsageLimit/UsedCount/Id, có Id phụ; tối đa 8 tiêu chí, sai field trả 400.
- [x] Bộ lọc đối soát: kỳ giao nhau, ngày chi trả, keyword Merchant/chứng từ, sort whitelist và phân trang; Merchant luôn bị ép scope của mình.
- [x] `POST /promotion/{id}/usages/filter` Admin-only: lọc đơn/khách/trạng thái hoàn lượt/ngày dùng, sort và count/page tại DB; giữ cả usage reverted. Lý do hoàn lượt cũ chưa lưu trả null, không tự dựng lịch sử.

### Đợt bổ sung bốn mục có thể code ngay — 09/11/2026

- `tests/smoke/admin-filter-pagination.ps1` PASS: sort số/chuỗi/nhiều tiêu chí, trang kế tiếp, reject sort sai; usage joins/hoàn lượt và quyền; lọc kỳ/ngày chi trả và chống vượt scope; snapshot lines trang 2 và trang ngoài phạm vi.
- Kiểm thử report/CSV với 25.001 giao dịch trả 413; đúng 25.000 giao dịch trả đầy đủ tổng 25.000, không bị truncate. Fixture SQL riêng đã được xóa trong finally; không gọi cổng thanh toán hoặc SMTP.
- `finance-write-guards.ps1` PASS lại, bổ sung kiểm tra lines của snapshot do chính API tạo: tổng số dòng/OrderId/Collected khớp, không chỉ kiểm tra snapshot fixture.
- Không thay schema trong đợt bổ sung này; không cần migration mới. Các thay đổi đã chia commit theo nhóm, chưa push.

Smoke dùng fixture local, có SQL phục vụ chuẩn bị/cleanup; không chạy trên production. Thanh toán tự động trong smoke dùng Mock nội bộ và tắt SMTP; không phải bằng chứng cổng VNPay/MoMo chính thức đã được test lại. Parser tài liệu không thay thế quét malware. Fixture/audit test có thể còn lịch sử đã vô hiệu hóa.
