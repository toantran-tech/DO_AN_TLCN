# Kế hoạch hoàn thiện Backend

Cập nhật: 09/11/2026. Đây là checklist chính để theo dõi và cập nhật tiến độ trong washgo-be. Nguồn yêu cầu: 13 tài liệu BACKEND tại `C:/Users/PC/Desktop/HK1_2027/cần làm` và source develop (mốc rà soát 2e96148). Không coi việc có endpoint hay có script test là bằng chứng đã chạy thành công.

## Cách nghiệm thu

- Làm theo từng đợt bên dưới. Mỗi đợt ghi rõ thay đổi, hợp đồng API, test đã chạy và giới hạn còn lại.
- Chỉ đánh dấu hoàn tất khi đối chiếu đủ yêu cầu, build và kiểm thử phù hợp. Thay đổi schema phải có migration và xác minh áp dụng DB.
- Không xóa tài liệu yêu cầu gốc. Đánh dấu tiến độ ở đây và NEXT_IMPLEMENTATION_PLAN.md để giữ căn cứ đối chiếu.
- Các mục “nếu cần”, dự báo AI, 2FA, hẹn giờ cấu hình, thêm/sửa cấu hình ô và mở lại khiếu nại là mở rộng cần chốt phạm vi; không mặc định đã được nghiệm thu.
- Không commit/push hoặc gửi email bên ngoài trong quá trình kiểm thử nếu chưa được yêu cầu.

## Phần đã có trước đợt này

- [x] Báo cáo Merchant: tổng hợp DB, coverage cả hai kỳ, CSV tạm tính, lifetime.
- [x] Đơn Merchant: xóa mềm trong summary, fingerprint ghi chú, thứ tự trang, ảnh/liên hệ, tài chính.
- [x] Đơn Admin: khóa tải phân công, operation/revision, chuyển kho, hủy Pending quá hạn, media và ẩn mã quản trị.
- [x] Dashboard/hồ sơ Admin, bảo mật mật khẩu/phiên và audit tập trung; 2FA vẫn chưa có.
- [x] Dashboard Merchant snapshot, SLA phân trang/UTC; hồ sơ MST/giấy phép chuỗi và cặp tọa độ.

Các dấu trên xác nhận source đã triển khai; nhật ký test cũ ở NEXT_IMPLEMENTATION_PLAN.md, không phải kết quả chạy lại hôm nay.

## 1. Admin quản lý Merchant

- [x] Chặn thay trạng thái bằng API sửa hồ sơ; giữ metadata phân vùng khi danh sách không đổi; thêm Id phụ khi phân trang; kiểm tra precision hoa hồng/ký quỹ và độ dài city/district trước ghi. Docker build và smoke merchant-admin-profile-guards.ps1 PASS ngày 09/11. Chưa phải toàn bộ validation của module.
- [x] Đợt 2 ngày 09/11: version chuỗi bắt buộc cho sửa/khóa và duyệt/từ chối; kiểm tra trong transaction cùng khóa Merchant với phân công đơn. Duyệt kiểm tra snapshot lúc gửi; legacy đối chiếu các trường hiện có. Test cạnh tranh lưu và duyệt/từ chối 200/409 PASS.
- [x] Đợt 2: sửa/khóa/duyệt/từ chối hỗ trợ operationId, fingerprint toàn payload, response gốc lưu cùng transaction; lookup theo chính Admin. Replay, key khác payload, không trùng audit PASS. Migration 20261108181756_AddMerchantWriteOperations đã áp dụng DB; model sạch.
- [x] Đợt 3 ngày 09/11: tạo Merchant nhận clientRequestId, ghi account/profile/operation/audit nguyên tử; retry cùng key không tạo/gửi mật khẩu lần hai, khác payload 409. Lock email/MST dùng cùng thứ tự với sửa/duyệt; unique constraint email/MST từ DB được ánh xạ 409 và rollback.
- [x] Trạng thái gửi email Merchant lưu DB và trả DTO: Unknown/Sent/Failed, credentialSentAt; Sent chỉ là SMTP chấp nhận, không bảo đảm inbox. Tạo tài khoản thành công độc lập email; replay/lookup Create giữ snapshot hồ sơ và lấy trạng thái delivery mới nhất. Không lưu/trả mật khẩu tạm. Migration AddMerchantCredentialDelivery đã áp dụng. Test tạo/edit đồng thời, lỗi unique DB, delivery và hồi quy PASS với Mailpit nội bộ.
- [ ] Gửi yêu cầu hồ sơ Merchant có khóa chống hai Pending nhưng chưa có operation replay cho thao tác gửi yêu cầu.
- [x] GET /merchant/summary AdminOnly: total/active/inactive/suspended/pendingProfileRequests/generatedAt, cùng repeatable-read snapshot; quyền và thay đổi số liệu khi khóa fixture đã test PASS.
- [ ] Sort/date theo hợp đồng, rating count và định nghĩa chỉ số tài chính.
- [ ] Upload giấy tờ riêng tư và liên kết hồ sơ theo đợt 2.

## 2. Giấy phép, hợp đồng và SLA

- [ ] Upload PDF/DOC/DOCX có nhận diện nội dung, giới hạn, metadata, quyền xem/tải/gán.
- [ ] File ID riêng cho từng loại; null giữ, gỡ rõ ràng, dữ liệu legacy đọc được.
- [ ] Merchant đề xuất giấy phép qua duyệt. Quyền SLA/hợp đồng đã chốt ngày 09/11: Admin quản lý, Merchant chỉ xem/tải; không nhận thay file SLA/hợp đồng qua update request của Merchant.
- [ ] Giữ lịch sử, không xóa file đang được tham chiếu; TTL upload chưa gán.

## 3. Finance Admin

- [ ] Đối chiếu lại phần snapshot/hoàn khác kỳ đã sửa; bảo đảm reconcile thu/hoàn/hoa hồng/chi trả, không điều chỉnh âm thầm kỳ đã trả.
- [ ] Quote, nguồn giao dịch và snapshot đối soát; khóa kỳ chồng lấn, thống nhất biên thời gian, xử lý kỳ 0/âm.
- [ ] Version/idempotency/lookup cho tạo và confirm-paid, refund review/confirm; partial refund và reference khác phải được xử lý rõ.
- [ ] Detail/lines/filter/paging refund và settlement; coverage, timezone, metadata báo cáo và CSV an toàn.

## 4. Cài đặt hệ thống

- [ ] Missing field không tự thành 0; version/transaction singleton, idempotency/lookup.
- [ ] Audit thay đổi, nguồn default/DB, lịch sử phân trang.
- [ ] Chính sách hiệu lực/snapshot SLA, phí và bồi thường; giữ các hạn đã chốt.

## 5. Locker Admin và Merchant

- [ ] Version/idempotency, validation và allowedActions.
- [ ] Xóa trạm nguyên tử với reservation/giao nhận, kể cả đồ đơn hủy.
- [ ] Query lịch sử gửi/trả tại DB, giữ scope và usageId.
- [ ] Batch trạng thái/xóa 1–50 ID với kết quả từng ID, replay/lookup và audit.

## 6. Dịch vụ Admin

- [ ] Enrich ảnh và lưu ảnh với ownership; dữ liệu Merchant đề xuất đúng ID.
- [ ] Version/idempotency CRUD/review và đồng bộ với tạo đơn/duyệt, chặn ghi dịch vụ xóa mềm.
- [ ] Validation, summary, sort ổn định; batch trạng thái/xóa theo quyền và guard.

## 7. Promotion Admin

- [ ] Status đích thay toggle; version/idempotency/lookup.
- [ ] Đồng bộ update/delete với consume/release và quote/create; giữ usage/history.
- [ ] Validation/date/precision, allowedActions, summary, thống kê/CSV tại DB.
- [ ] Batch trạng thái/xóa 1–100 ID, partial success, audit/replay.

## 8. Khiếu nại Admin

- [ ] Version bao gồm thread/minh chứng; idempotency xử lý/từ chối/message/chi trả.
- [ ] Snapshot cấu hình bồi thường, precision, chứng từ và thông báo xác nhận chi trả.
- [ ] Summary/filter/sort/paging, enrich liên hệ Admin theo batch và audit.
- [ ] Paging thread và vòng đời upload; mở rộng assignee/reopen/SLA chỉ khi chốt phạm vi.

## 9. Kiểm tra chéo và đóng đợt

- [ ] Kiểm tra nguồn audit chung ở các mutation mới; không lộ PIN/token/giấy tờ/secret.
- [ ] Rà 13 tài liệu, gộp yêu cầu trùng về cùng triển khai; giữ các giới hạn chưa làm trong checklist.
- [ ] Hồi quy đặt đơn → gửi → phân công → cân → giặt → thanh toán → trả → nhận; hủy/hoàn/kho; quyền và concurrency.
- [ ] Build Docker, kiểm tra Swagger, migration và cập nhật hướng dẫn/test theo hợp đồng cuối.
