# Checklist hoàn thiện Backend WashGo

## Customer hoàn tất ba mục bổ sung — 14/11/2026

- [x] Slots/detail một locker và giữ availableSlots; HTTP smoke customer-locker-slots PASS (status/deleted/order/PII/full locker/auth).
- [x] Feedback Customer list/summary, public UUID độc lập, predicate chung/phân trang snapshot, sao/phân bố toàn tập, không identity; ảnh proxy JWT/no-store kiểm tra lại approval/Hide, không lộ upload owner.
- [x] PublishForCustomers=true xác nhận riêng; giữ mặc định Merchant-only và replay payload cũ. Không auto public review legacy. Migration AddPublicCustomerReviewApproval thêm 3 cột nullable/index, đã áp DB test tạm từ đầu; model sạch.
- [x] Notification keyword dùng cùng safe title/content SQL/DTO trước count/paging; giữ scope/filter; HTTP smoke chứng minh nội dung bị che không lộ qua kết quả/total.
- [x] Build Release PASS, không thêm warning (clean build còn 5 nullable warnings cũ); customer-public-reviews-notifications HTTP PASS: publish/replay/ẩn danh/media/summary/Hide/Show/validation/auth và safe search. Hai suite Customer đã nối CI.
- [x] Bàn giao FE/Admin: [CUSTOMER_COMPLETION_API_REVIEW.md](docs/CUSTOMER_COMPLETION_API_REVIEW.md); yêu cầu gốc cập nhật trạng thái ở BACKEND_CUSTOMER.md.
- [ ] CI GitHub sau PR, nghiệm thu FE và triển khai container/DB chính do người dùng thực hiện. Image Docker local đã build trước tin nhắn dừng build/test; chưa recreate container/áp migration chính. Từ đây không chạy thêm test rộng/build Docker local theo yêu cầu tối ưu.

## Customer — chi tiết trạng thái ô tủ — 14/11/2026

- [x] Mục 1 [BACKEND_CUSTOMER.md](docs/BACKEND_CUSTOMER.md): GET /customer/lockers/{id} trả một locker với slots/status của mọi ô chưa xóa, sắp slotNumber rồi ID; chỉ public metadata. Giữ shape availableSlots; list/count ô trống suy ra từ cùng một lần đọc, hết ô không trả 404. Không thay cơ chế giữ ô khi tạo đơn.
- [x] Build Release PASS, 0 lỗi, 5 nullable warnings cũ; không cần migration. Có smoke riêng cho status/ordering/deleted/privacy/compatibility/full locker/auth và nối CI.
- [x] HTTP smoke riêng PASS; hai mục feedback/search đã hoàn thiện trong cập nhật phía trên. CI/triển khai chính và nghiệm thu FE do người dùng thực hiện.

## Cụm 2 — Shipper self-status, lưu kho tại tiệm và giao lại — 14/11/2026

Nhánh `feat/toantran/storage-lifecycle`, base 26398e6; chưa commit/push. [Plan C–G](docs/NEW_BACKEND_REQUIREMENTS_PLAN_20261113.md), [API FE](docs/STORAGE_WORKFLOW_API_REVIEW.md), [dự thảo Word/điều khoản](docs/STORAGE_TERMS_USECASE_DRAFT.md).

- [x] C: Shipper tự Available/Offline với revision/operation/replay/lookup, capability/lý do; chặn Offline khi còn chuyến và không ghi đè quản lý nhân sự; cùng khóa assignment.
- [x] D: cycle/request, nhiệm vụ StorageTransfer/StorageReturn, trạng thái order 14/15; bảng/index chống trùng, mốc/biên nhận/phí từng vòng; snapshot địa chỉ tiệm, không CRUD kho riêng.
- [x] E: phân công → Shipper collect giải phóng ô → handover có biên nhận mới bắt đầu lưu; bảo vệ mã/lockout, SLA 4h cảnh báo, không reset khi gán lại, không Admin xác nhận thay.
- [x] F: Customer quote ký 5 phút/yêu cầu giao lại/hủy trước xuất; đủ tiền và xác nhận lại phí tăng mới depart; transit không tính phí; place chiếm ô phù hợp hoặc rollback, mã/hạn nhận mới, nhận qua hai bước đang có. Giữ tiền/phí nhiều vòng, không tự thu phí giao lại.
- [x] Tích hợp payment phí, worker quá hạn 7 ngày, task/history/dashboard/busy predicate, cảnh báo SLA kho 4h/24h và notification ẩn danh. Exception giữ custody và audit, không tự hủy/gán lại sau lấy.
- [x] Legacy v0 không đổi tiền/ngày/biên nhận hàng loạt; cần Admin xác minh thực tế ở tiệm trước khi mở giao lại v1. Route shortcut bị chặn v1, v0 giữ tương thích.
- [x] G: nguồn Chatbot/nextStep cập nhật; API review liên kết cho FE và dự thảo Word/điều khoản đã soạn. Không publish điều khoản tự động.
- [x] Build Release 0 lỗi; model EF sạch; migration áp trên DB tạm từ đầu. Storage console PASS. HTTP lifecycle PASS: hai vòng, concurrent request replay/step replay, fee/lockout/no-slot rollback, privacy, self-status, legacy, unpaid worker và dashboard/alerts. Parser 6 script sửa PASS.
- [x] CI thêm Storage console/lifecycle; bốn smoke shortcut cũ chỉ chuyển chính fixture sang v0, không đổi dữ liệu thật.
- [ ] GitHub CI của cụm mới (gồm hồi quy rộng/security); chưa push. Kiểm thử race assignment-vs-Offline/pickup-vs-collect và các tình huống legacy mở rộng chưa phải toàn bộ đã chạy.
- [ ] Áp migration/rebuild Docker chính có backup và kiểm tra legacy; đợt này chỉ dùng source host/DB tạm, không thay container/DB chính.
- [ ] Đưa dự thảo vào Word gốc và duyệt/phát hành điều khoản qua Admin; UAT FE/Gemini thật theo nguồn mới.

Giới hạn build local: 5 nullable warnings cũ và 7 NU1900 do nguồn audit NuGet không truy cập được. Không coi dependency scan của cụm mới đã PASS; CI vẫn giữ security gate.

## Cụm 1 — Review publishing và quyền riêng tư — 13/11/2026

Nhánh: `feat/toantran/review-publishing-privacy` (base 04bb072). Đã commit 26398e6; trạng thái remote/PR do người dùng quản lý. Hợp đồng FE/Swagger: [REVIEW_PUBLISHING_PRIVACY_API.md](docs/REVIEW_PUBLISHING_PRIVACY_API.md); plan: [NEW_BACKEND_REQUIREMENTS_PLAN_20261113.md](docs/NEW_BACKEND_REQUIREMENTS_PLAN_20261113.md).

- [x] Redactor chung sau enrich cho đơn list/detail/create/mutation/conflict/replay; Customer không có identity/contact/ảnh/mã tiệm/kho của nhân sự, staff không có Customer identity/avatar/free text/locker codes. Giữ thông tin vận hành và mã riêng của Customer.
- [x] Customer không suy luận nhân sự qua merchantId/shipperId, column filters/sort, mã tiệm; staff filter/distinct không nhận trường Customer riêng tư.
- [x] Snapshot Shipper cũ che khi đọc cả replay/operation lookup, không rewrite ledger. Biên lai JSON/PDF dùng DTO theo role; thông báo kho Customer che trước lưu/push và khi đọc legacy inbox/mark-read. Key mới và giới hạn tìm title order được ghi trong hợp đồng FE.
- [x] Admin contact theo lô, timestamp/basis hồ sơ hiện tại, tên snapshot giữ nguyên, missing profile null/[]; contact không thuộc version và không đưa vào Customer/Partner DTO.
- [x] Publish riêng, canPublish/reason, version, operation/lookup, batch tối đa 50, audit actor; Hide/Show không tự phê duyệt. Merchant keyword/count/media dùng cùng ContentApprovedAt; Publish không đổi điểm.
- [x] Migration AddRatingContentApproval và backfill có điều kiện (visible, không xóa, có người/thời điểm kiểm duyệt); trước migration local: 1 rating, 0 legacy released. Không tự duyệt hidden/unreviewed.
- [x] Build .NET PASS, model EF không còn thay đổi chưa migrate; Privacy console PASS; parser 83 PowerShell scripts/diff check PASS. 5 nullable warnings cũ.
- [x] Docker CI/HTTP PASS: ABP/JWT, catalog, actual-weight/vòng đời đơn, keyword privacy và review publication. Review HTTP gồm 4 role, foreign owner, withheld filters, conflict DTO, receipt/inbox, contacts/missing profile, single/batch/replay/lookup/limit, concurrent Publish/Hide 200/409, audit và aggregate; fixture/container CI đã dọn. Mock payment và không SMTP ngoài ở CI.
- [x] Đã thêm Privacy console và hai suite review vào workflow/runner CI; chưa có run GitHub của cụm mới cho tới khi push.
- [x] Docker local đã build/recreate bản cuối; Swagger HTTP 200 có POST /rating/{id}/publish. Migration có trong history washgo_main và đủ hai cột mới; số rating giữ nguyên 1. Đã backup DB ra thư mục Temp ngoài repo trước triển khai; không đưa backup/log vào Git. Container CI đã dọn.
- [x] Code C–F và tài liệu G đã triển khai trong cụm 2 phía trên; các bước deploy/CI/UAT chưa tick riêng, không coi nghiệm thu toàn bộ đã xong.

## CI Backend đợt 1 — ngày 13/11/2026

Hướng dẫn: [BACKEND_CI_GUIDE.md](docs/BACKEND_CI_GUIDE.md). Workflow ở gốc repo `.github/workflows/backend-ci.yml`; PR #106 gồm commit 5ac8c29 và bản vá 04bb072. Theo ảnh người dùng, cả 5 check đã PASS; chưa xác nhận merge PR.

- [x] Cấu hình 5 job: build .NET, analysis, regression, secret/dependency scan, Docker package. PR vào develop/main, push develop/main, manual trigger; contents read, action SHA pin, artifact 14 ngày; chưa deploy/publish image.
- [x] Local build/analyzer/helper syntax PASS; actionlint kiểm tra workflow PASS. Analyzer giai đoạn đầu chưa là gate 0 warning/duplication toàn repo.
- [x] Regression tài liệu PDF/DOC/DOCX và email MIME PASS; không gửi email thật.
- [x] API catalog PASS trên Backend/PostgreSQL mới, migrate/seed từ đầu, test visibility/sort/filter/validation/paging và cleanup. Compose CI riêng, tmpfs, không nạp .env hay dùng DB local; chặn .env/.env.* khỏi Docker context.
- [x] Gitleaks ghim v8.30.0/digest quét 143 commit local, chưa phát hiện secret theo bộ luật công cụ; báo cáo redacted. Không tương đương chứng minh mọi rò rỉ đều bằng 0.
- [x] Người dùng chốt mapper không cần key: gỡ AutoMapper/Volo.Abp.AutoMapper, dùng Mapster 10.0.14 MIT qua provider ABP 8.2; chuyển 5 module mapping, giữ ObjectMapper call site, null clearing/in-place update/enrichment ignores. Bỏ wiring license đã thêm, không sửa .env thật. Giữ Caching.Memory 8.0.1, System.Text.Json 8.0.6 và JWT 8.14.0.
- [x] Build và regression trực tiếp 5 module Mapster PASS; đưa Mapping vào CI, thêm HTTP ABP/JWT và order lifecycle/actual-weight vào CI API. Gate giữ nguyên; trạng thái HTTP/security sau chuyển Mapster cập nhật khi chạy xong.
- [x] Sau chuyển Mapster: Security.ps1 PASS (Gitleaks 144 commit không có finding theo scanner; NuGet 0 High/Critical); resolved graph không còn AutoMapper/Volo.Abp.AutoMapper. Không sửa scanner/severity/allowlist.
- [x] Docker build + DB tạm mới PASS: mapping ABP/JWT, catalog và actual-weight đầy đủ vòng đời đơn (3/5/10kg, giá snapshot/phụ phí/ETA, quyền, replay/concurrency, Mock payment, giao nhận/hoàn tất). Container/DB test đã dọn; suite tài liệu/email vẫn PASS.
- [x] Theo ảnh/log người dùng, PR #106 đã có run GitHub-hosted: Build/Analyze/Regression PASS; Security fail đúng 3 High trên graph cũ, Docker package skip.
- [x] Người dùng đã commit/push bản vá vào PR #106; ảnh GitHub xác nhận cả 5 check xanh, gồm Security và Docker package. Các thay đổi review/privacy sau đó cần run CI mới khi được push.
- [ ] Bổ sung unit/coverage rộng, test E2E/NFR, static quality gate đầy đủ; CD/staging/registry/rollback/monitoring là chặng tiếp theo.

## Bộ lọc đánh giá Merchant — ngày 13/11/2026

- [x] Sửa keyword tại `RatingAppService.FilterAsync`: Merchant chỉ khớp Comment đã phát hành; sau cụm Publish dùng `!IsHidden && ContentApprovedAt != null` cho query và Partner DTO.
- [x] Lọc trước Count/Skip/Take nên danh sách và tổng số kết quả dùng cùng predicate; giữ Admin tìm mã đơn/tên khách/nhận xét chưa duyệt.
- [x] Không loại đánh giá chưa duyệt khỏi danh sách không keyword; sao vẫn hiển thị, comment/media tiếp tục theo redaction cũ.
- [x] Cập nhật Swagger summary; build Host PASS (0 lỗi, 5 cảnh báo nullable có sẵn); script PowerShell parse PASS; `git diff --check` PASS.
- [x] Thêm regression script `tests/smoke/rating-keyword-privacy.ps1`: fixture user/Merchant/order/rating riêng, chưa duyệt/đã phát hành/ẩn/xóa/khác Merchant, keyword chuẩn hóa, phân trang, scope và quyền HTTP; cleanup trong finally, không sửa seed. DB hiện tại không có email Merchant seed nên test dùng tài khoản tạm riêng.
- [x] Regression HTTP/PostgreSQL PASS trên container thật, DB `washgo_main`: keyword chỉ khớp nhận xét đã phát hành, không lộ qua tổng/phân trang; Admin vẫn tìm được nhận xét chưa duyệt và mã đơn; không keyword vẫn có sao chưa duyệt nhưng comment bị che; 401/403 đúng. Cleanup xác minh: 8 orders, 1 rating, 1 Merchant như trước test; fixture users/orders còn 0.
- [x] Docker build/recreate PASS sau khi người dùng khôi phục engine; container dùng đúng image mới. Swagger HTTP 200, `/rating/partner/filter` có summary mới. Không phát sinh migration; build giữ 5 cảnh báo nullable có sẵn.

Khi Docker engine đã hoạt động, tại thư mục gốc DO_AN chạy `docker compose up -d --build washgo-be`, chờ Swagger sẵn sàng rồi chạy `powershell -NoProfile -File .\washgo-be\tests\smoke\rating-keyword-privacy.ps1`.
Script mặc định dùng DB `washgo_db`; nếu Backend dùng DB khác qua `BACKEND_DB_NAME`, truyền `-Database <tên DB Backend>` (lượt này dùng `washgo_main`). Cần seed `admin@washgo.vn` với mật khẩu local `Admin@123`, các role Admin/Merchant/Customer và ít nhất một Merchant/một đơn làm mẫu clone. Test dùng tài khoản tạm, không phụ thuộc email Merchant/Customer seed. Không thay đổi provider thanh toán để chạy test này.

Phạm vi này chưa hoàn thành toàn bộ chặng A (ẩn danh đơn) hoặc chặng B (Publish riêng).

## Chatbot AI — triển khai ngày 13/11/2026

Nguồn: UC 1.5.1–1.5.6. [Plan](docs/CHATBOT_AI_IMPLEMENTATION_PLAN.md), [API review FE và cấu hình/test Gemini](docs/CHATBOT_API_REVIEW.md).

- [x] Hội thoại/tin nhắn riêng Customer; tạo cùng tin đầu, chat tiếp, xóa khỏi lịch sử, lazy load cursor có chữ ký/owner/resource.
- [x] 6 operations dưới `/customer/chatbot`; chỉ Customer, kiểm tra sở hữu cả history/send/lookup/delete; không lưu hội thoại rỗng.
- [x] RequestId/fingerprint, lookup/replay, quota, lease, transaction ngắn; không giữ transaction khi gọi AI.
- [x] Công cụ đọc dịch vụ, locker, đơn/ETA/quote, chính sách và hướng dẫn versioned; cards/sources do server sinh, allowlist dữ liệu, không đưa QR/PIN vào model.
- [x] Gemini generateContent adapter, giữ thought signatures, giới hạn response, error/timeout mapping; không có production Mock fallback.
- [x] Lịch sử 90 ngày, worker purge mỗi 30 phút; smoke xác minh ẩn và purge fixture hết hạn. Xóa hội thoại không reset quota hoặc bị response muộn làm sống lại.
- [x] `chatbot.ps1` PASS qua host test riêng: scopes, cursor/snapshot, replay, nguồn dữ liệu/privacy, lỗi provider, concurrent/busy/delete race, stale lease, purge và quota sau delete.
- [x] HTTP contract Gemini/cursor không gọi mạng PASS; model EF không còn pending changes. Migration `20261113025410_AddCustomerChatbot` đã áp dụng local.
- [x] Docker build/recreate PASS; Swagger 5 paths/6 operations; container thật thiếu key trả HTTP 503 `AiNotConfigured`.
- [x] Có .env/.env.example/compose mapping Gemini; key rỗng và .env bị ignore. Đã có API review/setup cho FE.
- [x] Gemini key đã nạp Docker; 4 live test HTTP 200/Completed: hướng dẫn nhận đồ, dịch vụ/giá, locker chưa có vị trí và có tọa độ thử. Nguồn/cards đúng; key không in ra, chat thử đã ẩn.
- [ ] Nghiệm thu rộng model thật: đơn/ETA/snapshot policy, prompt-injection ở lời trả lời, câu hỏi nhiều nghĩa, độ trễ/quota Google.
- [ ] Kiểm thử FE lazy load/giữ cuộn và trải nghiệm chatbot trên trình duyệt; đợt này chỉ làm BE.
- [ ] Kiểm thử chuyển mốc quota ngày thực tế và quan sát worker theo lịch; test hiện có gọi trực tiếp repository purge.
- [x] Tách nhánh/commit theo nhiệm vụ: using refactor và chatbot Gemini, xem [hướng dẫn push/PR](docs/CHATBOT_GIT_PUSH_PLAN.md).
- [ ] Người dùng push và tạo PR vào develop theo thứ tự; agent chưa push hoặc tạo PR.

Kết quả fixture và Gemini thật được ghi riêng. Fixture dùng project `tests/smoke/ChatbotHost`; container Backend đã gọi Gemini thật, không bật Mock.

## Shipper — triển khai ngày 11/11/2026

Hợp đồng và breaking changes FE: [SHIPPER_API_REVIEW.md](docs/SHIPPER_API_REVIEW.md). Plan gốc: [SHIPPER_IMPLEMENTATION_PLAN.md](docs/SHIPPER_IMPLEMENTATION_PLAN.md). Đợt này được chia thành 3 nhánh nối tiếp; xem [hướng dẫn PR](docs/SHIPPER_GIT_PUSH_PLAN.md).

- [x] Mở rộng DeliveryTask sẵn có: liên kết completion event/actor, snapshot địa điểm/ô, SLA; migration `20261111021910_AddShipperTaskSnapshots` đã áp dụng local. Dữ liệu cũ có nhãn LegacyCurrentResource, không bịa mốc thiếu.
- [x] API nhiệm vụ Shipper: filter/summary/detail/credential/lookup operation; preview đúng trạm, scope JWT, batch enrich, PIN/QR tách khỏi list/detail mặc định.
- [x] Collect/return có taskId/assignmentVersion/revision/operationId cho Shipper; dùng lại OrderOperation, fingerprint HMAC, replay kết quả cũ sau phân công lại; không cộng ô/lượt lần hai.
- [x] Lịch sử theo actor, eventId và snapshot; summary/keyword/type/date/locker/detail/CSV scoped. Tách trạng thái hiện tại và sự kiện; không skip dòng sau count.
- [x] Dashboard snapshot VN: pending hiện tại, hoàn tất trong kỳ, KPI/sample/coverage/kỳ trước, chart và preview; không có mẫu trả null.
- [x] Hồ sơ có revision bao phủ User+Shipper, transaction/row lock, idempotency/lookup; liên hệ Merchant, ngày tham gia, lỗi có mã; không bịa ratingCount. Nhật ký self-service theo ActorId.
- [x] Smoke `shipper-workspace.ps1` PASS: scope, profile replay/version, concurrent profile 200/409, concurrent delivery/replay, credential isolation, history/reassignment, dashboard/CSV, no-box rollback; fixture riêng, không SMTP/cổng thanh toán.
- [x] Hồi quy `shipper-profile.ps1`, `shipper-tasks.ps1`, `shipper-assignment.ps1` PASS sau cập nhật hợp đồng guard/credentials.
- [x] `qr-pin-flow.ps1` PASS trọn vòng giặt/QR/PIN/nhận đồ hai bước/chuyển kho, dùng Mock chỉ cho test local.
- [x] `order-cancellation.ps1` PASS: phí hủy/hoàn tiền, đồ còn trong tủ/tiệm, Shipper trả đơn hủy, nhận đồ và chạy đua cancel/deposit.
- [x] Migration `20261111024338_ReconcileLegacyShipperCompletions` đã áp dụng: ghép lại 21 cặp legacy lệch phần lẻ giây, giữ task gốc/event, không còn cặp trùng tương ứng. Chẩn đoán read-only: 0 tài khoản Shipper đang hoạt động thiếu hồ sơ.
- [x] Model không còn thay đổi chưa có migration; Docker build PASS, Swagger/OpenAPI tải được với 14 đường dẫn Shipper. Đã khôi phục `Payment__Provider=Live` theo `.env`; Mock/SMTP tắt chỉ dùng trong hồi quy local, không kiểm thử giao dịch thật.

Không tick đã làm: cửa tủ IoT, routing traffic, thu nhập Shipper, 2FA, tự Available/Offline, tự nhận/từ chối và task chuyển kho mới. SLA Return đơn hủy chưa chốt mốc riêng thì deadline=null. Không thay đổi nghiệp vụ Word cho các hợp đồng kỹ thuật này, không thêm Use Case trùng.

Cập nhật: 09/11/2026. Nguồn: 13 tài liệu `BACKEND_*.md` tại `C:/Users/PC/Desktop/HK1_2027/cần làm`. Đây là checklist chính trong BE. Hướng dẫn hợp đồng API: [ADMIN_COMPLETION_REVIEW.md](docs/ADMIN_COMPLETION_REVIEW.md).

Dấu [x] xác nhận phần cốt lõi đã triển khai và có kiểm thử dưới đây; không bao gồm những mở rộng ghi riêng ở cuối. Không coi có endpoint hoặc có script là đã kiểm thử thành công. Đợt 4 nhánh trước đã được nhóm merge vào develop. Đợt nghiệp vụ mới ở feat/toantran/agreed-business-rules-20261109 chưa commit/push; xem mục 10.

## Bổ sung email 10/11/2026

- [x] Email cấp tài khoản Shipper/Merchant dùng lại template HTML và logo CID hiện có.
- [x] Thông báo liên kết Google gửi sau commit đến email tài khoản WashGo; không gửi lại khi danh tính không đổi.
- [x] HTML encode các giá trị động; kiểm tra MIME/logo và loại bỏ nội dung OTP khỏi thư cấp tài khoản/liên kết Google bằng smoke EmailLogo.

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
- [x] Quote và quoteToken; khóa kỳ chồng lấn, biên UTC gồm hai đầu. Kỳ thiếu tỷ lệ bị chặn để đối chiếu; theo quyết định mới, kỳ âm được CarryForward và kỳ 0 NoPayout (xem mục 10).
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

## 10. Phạm vi đã chốt và hoàn thiện ngày 09/11/2026

Căn cứ quyết định thành viên: [AGREED_BUSINESS_RULES_20261109.md](docs/AGREED_BUSINESS_RULES_20261109.md). Phân biệt chức năng đã triển khai với tính năng nhóm loại khỏi phạm vi.

- [x] Khiếu nại Pending quá 48 giờ chỉ cảnh báo Admin; không assignee/reopen. Admin được nhắn sau từ chối, khách gửi khiếu nại mới nếu còn hạn 3 ngày và không có Pending khác.
- [x] Khách khai itemValue, hư hỏng/mất đồ bắt buộc ảnh; lưu declaredItemValue riêng với giá trị Admin xác minh. Bồi thường lưu mã tham chiếu, tùy chọn 0–3 ảnh, compensationPaidBy/compensationPaidAt và audit/replay.
- [x] Hợp đồng bao gồm hết ngày kết thúc theo giờ VN; hết hạn chặn phân công mới, cảnh báo trước 30 ngày/đã hết hạn; không khóa tài khoản hoặc chặn xử lý đơn cũ. Null là không thời hạn.
- [x] Gán tự động dùng chấm điểm 35/25/25/15, chưa có đánh giá công khai dùng 3/5 trong tính điểm; trả điểm từng thành phần, thứ tự ổn định, xác nhận vẫn khóa sức chứa.
- [x] Dòng đối soát snapshot count/page tại PostgreSQL; report/CSV đồng bộ giới hạn 366 ngày và 25.000 bản ghi mỗi nguồn, quá mức trả ReportTooLarge.
- [x] Giao dịch thu tiền giặt/refund gắn settlementId đúng một kỳ; hoàn phát sinh sau vào kỳ kế, giữ snapshot kỳ trước. Kỳ âm CarryForward, kỳ 0 NoPayout; không cho xác nhận chuyển tiền giả; số âm bù một lần vào kỳ sau qua liên kết kỳ nguồn/đích.
- [x] byService giữ subtotal theo giá niêm yết trước giảm/phụ phí/hoàn, mô tả basis rõ; tổng thực thu ở totalRevenue, không phân bổ xuống từng dịch vụ.
- [x] Sort Promotion, bộ lọc đối soát và lịch sử voucher có phân trang đã PASS từ đợt trước.
- [x] Đặc tả Word đã đồng bộ tại các UC liên quan; sửa mâu thuẫn phí hủy UC 3.9.4 theo UC 1.2.7, giữ nguyên số mục và có bản sao lưu trước chỉnh.

Ngoài phạm vi theo quyết định nhóm (không phải chức năng đã code): 2FA, hẹn hiệu lực cấu hình, dự báo/học máy, assignee/reopen khiếu nại, quy trình hai người duyệt bồi thường, export nền/ledger riêng, tự động chuyển khoản ngân hàng, phân bổ thực thu từng dịch vụ. Merchant dừng hợp tác còn âm được giữ công nợ để xử lý ngoài hệ thống.

### Kiểm chứng đợt nghiệp vụ nhóm chốt

- Smoke mới `agreed-business-rules.ps1` PASS: boundary hợp đồng VN; cảnh báo; gợi ý trung lập; bù trừ âm/0/dương không lặp; cạnh tranh chốt kỳ 201/409; SLA khiếu nại; ảnh/người chi trả và khiếu nại mới sau từ chối.
- Hồi quy Finance, phân công và Customer khiếu nại đã PASS; chi tiết và giới hạn ở tài liệu nghiệp vụ mới. Không gọi chuyển tiền ngân hàng hoặc gửi SMTP ngoài trong smoke.
- Có migration AddAgreedContractsClaimsAndCarryForward, áp dụng trên PostgreSQL local. Giấy tờ lịch sử không tự gán người chi hoặc giá trị khách khai khi không có căn cứ.
- Đợt này đang ở nhánh feat/toantran/agreed-business-rules-20261109, chưa commit/push. Không sửa FE.

### Đợt bổ sung bốn mục có thể code ngay — 09/11/2026

- `tests/smoke/admin-filter-pagination.ps1` PASS: sort số/chuỗi/nhiều tiêu chí, trang kế tiếp, reject sort sai; usage joins/hoàn lượt và quyền; lọc kỳ/ngày chi trả và chống vượt scope; snapshot lines trang 2 và trang ngoài phạm vi.
- Kiểm thử report/CSV với 25.001 giao dịch trả 413; đúng 25.000 giao dịch trả đầy đủ tổng 25.000, không bị truncate. Fixture SQL riêng đã được xóa trong finally; không gọi cổng thanh toán hoặc SMTP.
- `finance-write-guards.ps1` PASS lại, bổ sung kiểm tra lines của snapshot do chính API tạo: tổng số dòng/OrderId/Collected khớp, không chỉ kiểm tra snapshot fixture.
- Không thay schema trong đợt bổ sung này; không cần migration mới. Các thay đổi đã chia commit theo nhóm, chưa push.

Smoke dùng fixture local, có SQL phục vụ chuẩn bị/cleanup; không chạy trên production. Thanh toán tự động trong smoke dùng Mock nội bộ và tắt SMTP; không phải bằng chứng cổng VNPay/MoMo chính thức đã được test lại. Parser tài liệu không thay thế quét malware. Fixture/audit test có thể còn lịch sử đã vô hiệu hóa.
