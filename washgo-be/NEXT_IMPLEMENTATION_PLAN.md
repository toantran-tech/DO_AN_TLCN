> Staging 14/11/2026: đã bổ sung Brevo HTTPS (template/logo hiện có), Supabase private storage cho media/tài liệu, health endpoints và trang payment return không xác nhận giao dịch. Test hợp đồng provider đã nối vào suite EmailLogo của CI. Chưa chạy test/build/Docker local theo yêu cầu; chưa có key Brevo/Supabase và chưa chuyển file cũ lên cloud. Cấu hình/việc còn lại: [Render staging](docs/RENDER_STAGING_SETUP.md), [mẫu env](docs/render-staging.env.example).

> Staging 14/11/2026: đã chuyển backup mới vào Neon, đối chiếu đủ 45 bảng và Admin; local DB không đổi. Đã thêm guard tắt seed mặc định ngoài Development, chờ commit/CI/merge để Render dùng. Web Render/mail/storage chưa hoàn tất. Xem [RENDER_STAGING_SETUP.md](docs/RENDER_STAGING_SETUP.md) và checklist.

> Customer hoàn tất 14/11/2026: cả 3 mục BACKEND_CUSTOMER.md đã code, build/model và HTTP trên DB tạm PASS; CI thêm hai smoke Customer. Bàn giao [CUSTOMER_COMPLETION_API_REVIEW.md](docs/CUSTOMER_COMPLETION_API_REVIEW.md), xem checklist hiện hành. Image local đã build nhưng chưa recreate container/migrate DB chính. Người dùng yêu cầu để họ/CI tự test và build/deploy tiếp; không chạy thêm kiểm thử rộng local.

> Customer bổ sung 14/11/2026: đã sửa mục 1 [BACKEND_CUSTOMER.md](docs/BACKEND_CUSTOMER.md): detail một locker có đủ slots/status, giữ availableSlots và trả 200 khi hết ô. Build PASS; smoke đã nối CI, chưa chạy HTTP/rebuild Docker bản này. Không migration. Mục feedback công khai và search notification chưa làm. Xem [checklist](IMPLEMENTATION_CHECKLIST.md).

> Cập nhật 14/11/2026: code C–F self-status/lưu kho tại tiệm/giao lại đã triển khai; Storage unit và HTTP hai vòng + replay/rollback/worker/legacy/dashboard PASS trên DB tạm. G đã có API review, nguồn AI và dự thảo Word/điều khoản. Chưa deploy DB/Docker chính, chưa chạy CI GitHub của cụm mới/UAT, chưa sửa Word gốc/publish policy. Theo dõi [cụm 2 checklist](IMPLEMENTATION_CHECKLIST.md), [API FE](docs/STORAGE_WORKFLOW_API_REVIEW.md), [dự thảo](docs/STORAGE_TERMS_USECASE_DRAFT.md). Nhánh `feat/toantran/storage-lifecycle`, chưa commit/push.

> Cụm 1 A/B ngày 13/11/2026: đã hoàn thiện Publish đánh giá/contacts Admin và bảo vệ dữ liệu đơn/biên lai/thông báo theo role. Docker CI HTTP, race/replay/aggregate, Privacy console, build và model check PASS. Đã commit 26398e6; trạng thái cụm C–G xem cập nhật 14/11 phía trên. Chi tiết hiện hành: [checklist](IMPLEMENTATION_CHECKLIST.md), [plan bổ sung](docs/NEW_BACKEND_REQUIREMENTS_PLAN_20261113.md), [API bàn giao](docs/REVIEW_PUBLISHING_PRIVACY_API.md).

> Nghiệp vụ thành viên chốt ngày 09/11/2026 đã triển khai: cảnh báo khiếu nại 48h, chứng từ/người chi bồi thường, hợp đồng hết hạn theo giờ VN, gán đơn theo điểm trung lập, nguồn đối soát chỉ nhận một lần và bù trừ kỳ âm/đóng kỳ 0. Phạm vi và hợp đồng mới tại [AGREED_BUSINESS_RULES_20261109.md](docs/AGREED_BUSINESS_RULES_20261109.md); trạng thái hiện hành xem mục 10 [checklist](IMPLEMENTATION_CHECKLIST.md). Đặc tả DO_AN/Đặc tả usecase.docx đã đồng bộ; chưa commit/push đợt này.

> Cập nhật 09/11/2026: xem kết quả tại [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) và hợp đồng API mới tại [docs/ADMIN_COMPLETION_REVIEW.md](docs/ADMIN_COMPLETION_REVIEW.md). Các mục cũ phía dưới là lịch sử. Đã chia commit thành 4 nhánh, chưa push; xem docs/GIT_PUSH_PLAN.md.

# Tiến độ WashGo theo Đặc tả usecase (Backend trước)

## CI và mapper ngày 13/11/2026

- CI đợt 1 ở PR #106: theo log người dùng, GitHub Build/Analyze/Regression PASS; Security chặn dependency High như thiết kế.
- Phương án người dùng chốt cuối: Mapster 10.0.14 MIT không cần key thay AutoMapper; giữ ObjectMapper ABP 8.2. Chuyển 5 profile, config riêng compile sớm, đăng ký cặp rõ ràng, depth 32; giữ null clearing và cập nhật entity tại chỗ.
- Local Security PASS, không còn AutoMapper trong graph; Docker regression mapping/JWT/catalog và vòng đời đơn/actual-weight PASS trên DB tạm đã dọn. Không hạ gate; còn 5 nullable warnings cũ. Bản vá đã commit/push (04bb072), người dùng gửi ảnh PR #106 cả 5 check PASS; chưa xác nhận đã merge.
- Hướng dẫn stage/commit riêng phần CI/dependency tại [CI_DEPENDENCY_SECURITY_FIX.md](docs/CI_DEPENDENCY_SECURITY_FIX.md); trạng thái tại checklist. Chưa triển khai CD/staging hoặc coi đủ mọi metric rubric.

## Chuẩn hóa using ngày 13/11/2026

- Đã đưa các tham chiếu type đầy đủ trong source BE sang using/alias đầu file; bỏ qua migration/generated files và giữ nguyên chuỗi/comment.
- Build BE PASS, 0 lỗi; còn 5 nullable warnings có sẵn. Không đổi nghiệp vụ hoặc schema DB.

## Chatbot AI ngày 13/11/2026 — BE và nghiệm thu fixture

- Đã triển khai UC 1.5.1–1.5.6: hội thoại/tin nhắn, lazy load cursor, owner checks, gửi lại an toàn, quota/lease và retention 90 ngày.
- Công cụ chỉ đọc nguồn dịch vụ, locker, đơn/ETA/quote, chính sách; hướng dẫn versioned. Không có công cụ ghi trạng thái/đặt đơn/thanh toán, không đưa mã mở tủ vào ngữ cảnh AI.
- Có adapter Gemini/configuration và 6 API Customer. Docker đã build/recreate, Swagger tải được. Migration AddCustomerChatbot áp dụng local, model sạch.
- Smoke fixture PASS gồm concurrency/delete race/purge/privacy; adapter HTTP contract không gọi mạng PASS. Container thật khi thiếu key trả 503 AiNotConfigured.
- Key đã được điền và nạp lại Docker. Gemini thật PASS 4 ca hướng dẫn nhận đồ/dịch vụ/locker có-không vị trí; còn nghiệm thu rộng đơn/ETA/chính sách, prompt-injection và độ trễ/quota. Chưa làm FE. Đã tách nhánh refactor và chatbot; chưa push, xem [hướng dẫn](docs/CHATBOT_GIT_PUSH_PLAN.md).
- Chi tiết: [checklist](IMPLEMENTATION_CHECKLIST.md), [API review, cấu hình key và chạy test](docs/CHATBOT_API_REVIEW.md).

## Shipper ngày 11/11/2026 — hoàn tất BE cốt lõi và nghiệm thu local

Đã triển khai phần BE cốt lõi của sáu chặng theo [SHIPPER_IMPLEMENTATION_PLAN.md](docs/SHIPPER_IMPLEMENTATION_PLAN.md). Hợp đồng API/guard và phần ngoài phạm vi tại [SHIPPER_API_REVIEW.md](docs/SHIPPER_API_REVIEW.md); kết quả build/migration/smoke và việc còn kiểm tra tại [checklist](IMPLEMENTATION_CHECKLIST.md), mục Shipper 11/11. Không coi IoT/2FA/thu nhập/tự nhận việc là đã triển khai. Đợt này được chia thành 3 nhánh nối tiếp; xem [hướng dẫn PR](docs/SHIPPER_GIT_PUSH_PLAN.md).

## Email ngày 10/11/2026

- Email cấp tài khoản Shipper/Merchant dùng chung khung HTML, màu thương hiệu và logo PNG nhúng CID của email OTP hiện có; không gửi kèm hướng dẫn OTP cho thư cấp tài khoản.
- Liên kết Google thành công gửi thông báo đến email của tài khoản WashGo sau khi transaction commit. Liên kết lại cùng danh tính không gửi thư trùng; lỗi SMTP không hủy liên kết đã lưu.
- Nội dung tên, email và mật khẩu tạm thời được HTML encode. Không thay đổi luồng cấp tài khoản, không thêm email chào mừng Customer/đặt lại mật khẩu thành công trong đợt này.
- Kiểm tra cục bộ: `dotnet run --project tests/smoke/EmailLogo/EmailLogo.csproj`; tạo MIME trong thư mục tạm, không gửi email thật.
Cập nhật lần cuối: 09/11/2026

## Merchant đợt 3 ngày 09/11/2026 — tạo tài khoản và email

- [x] Nhánh riêng `feat/toantran/merchant-create-safety`, từ develop đã merge PR #77. POST `/merchant/` nhận clientRequestId GUID tùy chọn (nên dùng): chuẩn hóa email/MST, khóa key và định danh, kiểm tra dưới khóa, ghi User/Merchant/ảnh/vùng/Create audit/operation trong cùng transaction. Replay không tạo lại, không sinh/gửi lại mật khẩu; khác payload trả 409 OperationIdReused. GET `/merchant/operations/{clientRequestId}` nhận cả Create, scope Admin actor; snapshot hồ sơ giữ nguyên, delivery đọc trạng thái mới nhất. Thêm ClientRequestId trên command với WhenWritingNull để không đổi fingerprint các operation Update cũ không gửi field này.
- [x] Trùng email/MST lúc tạo trả 409 EmailAlreadyUsed/TaxCodeAlreadyUsed; infrastructure classifier chỉ nhận unique constraint tương ứng (23505), không đổi các lỗi DB khác thành duplicate. Áp dụng wrapper cho create/update/approve; exception xảy ra thì explicit UoW rollback trước khi trả 409. Create/update/approve thống nhất thứ tự khóa email → MST → Merchant khi có ghi định danh. Tên cửa hàng vẫn tối đa 200; tên hiển thị User dẫn xuất giới hạn 100 theo cột tài khoản.
- [x] Email Merchant có subject/nội dung riêng, không dùng nhãn Shipper. Account + operation commit trước SMTP; chỉ request tạo đầu tiên gửi. Merchant lưu CredentialDelivery (0 Unknown, 1 Sent = SMTP chấp nhận, 2 Failed xác định), CredentialSentAt và trả CredentialEmailSent. Khi mất kết nối/ack hoặc crash trước xác nhận là Unknown, không tự resend; tài khoản vẫn tồn tại và có thể dùng Quên mật khẩu. Delivery cập nhật trong transaction riêng, giữ version hồ sơ; không lưu plaintext password vào operation/audit/DTO.
- [x] Migration `20261108184346_AddMerchantCredentialDelivery` đã áp dụng PostgreSQL Docker; legacy = Unknown/null, không suy là đã gửi. Build .NET/Docker thành công. Test `merchant-create-safety.ps1` PASS: hai request cùng key một account/một email; khác key tranh email/MST; create tranh Admin sửa email; trigger fixture ép lỗi unique email/MST thật từ DB trả đúng mã 409 và không còn account/operation dở; không lộ password; recipient 550 giữ account và Failed; transport lỗi giữ account và Unknown; retry sau phục hồi không gửi lại. Trigger được dọn (0 còn lại).
- [x] Hồi quy `merchant-admin-profile-guards.ps1`, `merchant-admin.ps1`, `merchant-profile-legal.ps1` PASS. Test dùng SMTP Mailpit riêng, không gửi email bên ngoài. Hướng dẫn chạy/khôi phục tại `docs/ADMIN_MERCHANT_CREATE.md`. Cập nhật checklist chính trong repo.
- [ ] Còn lại của quản lý Merchant: sort/date/rating count và định nghĩa chỉ số tài chính, upload giấy tờ/SLA/hợp đồng riêng tư; replay cho gửi yêu cầu hồ sơ. Chưa coi toàn folder yêu cầu hoàn tất. Chưa commit/push nhánh mới.


## Merchant đợt 2 ngày 09/11/2026 — version và kết quả thao tác

Folder yêu cầu nằm tại `C:/Users/PC/Desktop/HK1_2027/cần làm`. Checklist chính đã được đưa về [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) trong thư mục gốc washgo-be theo yêu cầu; cập nhật tiến độ ở bản này.

- [x] GET/list Merchant trả version chuỗi; PUT hồ sơ và status bắt buộc expectedVersion. Thiếu 400, cũ 409 kèm hồ sơ mới. Version đơn điệu ở độ chính xác microsecond của PostgreSQL; dùng chuỗi tránh mất chính xác ở JavaScript. Các thao tác ghi khóa Merchant trong transaction bằng cùng capacity lock với phân công đơn.
- [x] operationId tùy chọn cho sửa/khóa/duyệt/từ chối; lock key trước lock Merchant, hash toàn payload, response gốc lưu cùng transaction. Replay đúng key trả snapshot cũ, không ghi/gửi audit/notification lại; khác nội dung trả 409. GET `/merchant/operations/{operationId}` AdminOnly và scope actor; chỉ kết quả đã commit, 404 không kết luận request đang chạy đã thất bại.
- [x] Gửi yêu cầu hồ sơ khóa cùng Merchant để bảo vệ một Pending và snapshot. Duyệt/từ chối có version yêu cầu; duyệt kiểm tra baseMerchantVersion và Current snapshot trước áp Proposed. Hồ sơ đổi trả 409 MerchantProfileChanged; legacy đối chiếu trường snapshot hiện có. Duyệt/từ chối đồng thời chỉ một thành công; yêu cầu đã xử lý trả 409 AlreadyReviewed. Duyệt nhận expectedVersion/operationId ở query; từ chối ở body. Hướng dẫn `docs/ADMIN_MERCHANT_REVIEW.md`.
- [x] Migration `20261108181756_AddMerchantWriteOperations` đã áp dụng PostgreSQL Docker; chỉ thêm bảng operation. EF has-pending-model-changes sạch. .NET/Docker build thành công, còn các cảnh báo nullable đã có.
- [x] Bản Docker cuối: `merchant-admin-profile-guards.ps1` PASS (save/review đồng thời 200/409, replay/conflict/lookup/audit, stale snapshot, quyền và validation/vùng); `merchant-profile.ps1` PASS; `merchant-profile-legal.ps1` PASS. Fixture riêng, không gửi email bên ngoài. Cập nhật hợp đồng version trong các script merchant-admin, audit-log, system-settings; chưa chạy lại ba script này vì có bước gửi thông tin tài khoản/thiết lập toàn hệ thống.
- [x] Các phần tạo Merchant/idempotency tạo, ánh xạ lỗi unique và email delivery của đợt 2 đã được xử lý ở đợt 3 bên trên. Upload PDF/Word và các mục còn lại vẫn theo checklist, chưa coi toàn module hoặc toàn folder hoàn tất.


## Đợt hoàn thiện folder cần làm ngày 09/11/2026

Checklist tổng: [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md). Giữ nguyên 13 tài liệu yêu cầu để đối chiếu; không đánh dấu toàn module hoàn tất khi mới sửa một phần. Quyền tài liệu đã chốt: **Admin quản lý SLA/hợp đồng, Merchant chỉ xem/tải**; giấy phép Merchant đề xuất qua duyệt.

- [x] Admin Merchant — đợt 1: PUT `/merchant/` không còn thay trạng thái; nếu client gửi status khác hiện tại trả 400 với `meta.code=UseMerchantStatusEndpoint`. Bỏ status hoặc echo trạng thái hiện tại vẫn sửa hồ sơ được; khóa/mở tiếp tục đi PUT `/merchant/status` với guard/thu hồi phiên hiện có. Đây chưa phải sửa concurrency của luồng status.
- [x] Lưu lại cùng vùng giữ nguyên ID/priority/isActive/assignedBy/assignedAt/createdAt/updatedAt; chỉ xóa link bị bỏ và thêm link mới. `null` giữ vùng, `[]` gỡ vùng. Danh sách Merchant thêm ThenBy(Id). Validate commission và deposit tối đa 2 chữ số lẻ, deposit trong decimal(18,2), city/district tối đa 100 trước ghi.
- [x] GET `/merchant/summary` AdminOnly trả tổng Merchant, ba trạng thái và số yêu cầu hồ sơ Pending trong transaction repeatable-read; `generatedAt` UTC, không theo bộ lọc bảng.
- [x] Build .NET và Docker thành công (còn 5 cảnh báo nullable có sẵn); Swagger có API summary. `tests/smoke/merchant-admin-profile-guards.ps1` PASS trên Docker: quyền 401/403, không khóa/mở bằng sửa hồ sơ, endpoint status hoạt động, summary đổi đúng theo trạng thái fixture, validation không ghi dữ liệu sai, hoa hồng 0 hợp lệ, lưu/xóa vùng đúng. Script dùng fixture riêng và dọn sau test, không gọi tạo Merchant/gửi email. Không thay model nên không tạo migration.
- [ ] Tiếp theo: version/idempotency/lookup Merchant, duyệt hồ sơ chống snapshot cũ, tình trạng email, tài liệu riêng tư; sau đó Finance, Settings, Locker, Service, Promotion, Incident theo checklist. Chưa kết luận toàn bộ folder đã hoàn tất.


Ghi nhận ngày 05/11/2026 — URL đăng ký VNPay: form từ chối localhost. `tools/payment-tunnel/compose.yml` và proxy `server.mjs` chỉ công khai GET trang giới thiệu/return, GET `/payment/vnpay-ipn`, POST `/payment/momo-ipn`; các route còn lại trả 404, không công khai Swagger/Auth. Lệnh bật ban đầu bị automatic approval review từ chối vì thiếu đồng ý chuyển IPN qua Cloudflare; sau khi giải thích, người dùng đã đồng ý bật. **Đã chạy tunnel:** `https://impressive-weighted-magnitude-champagne.trycloudflare.com`. Kiểm tra public: trang gốc 200; Swagger/Auth 404; VNPay IPN tới backend (từ chối chữ ký chưa hợp lệ), MoMo IPN giả 400. Đã điền URL IPN/return tương ứng vào .env local. URL tạm có thể đổi khi khởi động lại tunnel; chưa có khóa gateway riêng nên chưa kết luận thanh toán end-to-end thành công.

Ghi nhận ngày 05/11/2026 — chuyển thanh toán khỏi Mock theo yêu cầu: đã thêm cấu hình VNPay/MoMo vào docker-compose.yml, .env.example và .env local; đặt PAYMENT_PROVIDER=Live, URL VNPay sandbox và MoMo test-payment chính thức. Đã recreate backend, xác minh container Provider=Live và đúng hai URL sandbox, Swagger 200. **Chưa hoàn tất thanh toán qua cổng**: người dùng xác nhận chưa có khóa sandbox riêng; hiện thiếu VNPAY_TMN_CODE/VNPAY_HASH_SECRET, MOMO_PARTNER_CODE/MOMO_ACCESS_KEY/MOMO_SECRET_KEY và IPN HTTPS public. Tạo thanh toán sẽ báo thiếu cấu hình, không fallback Mock. Hướng dẫn: docs/PAYMENT_SANDBOX_SETUP.md. VNPAY_IPN_URL cần khai riêng trên portal, không tự đăng ký qua compose; MoMo gửi ipnUrl khi tạo giao dịch. Chưa gửi yêu cầu tạo giao dịch ra cổng hoặc xác nhận IPN thật trong lượt này. Cấu hình Live hiện khiến các smoke test yêu cầu Mock cần đổi chế độ rõ ràng khi chạy lại.

Ghi nhận ngày 05/11/2026 — lọc dịch vụ nhiều lựa chọn: `POST /customer/services/filter` thay trường đơn `ServiceType`, `MinPrice`, `MaxPrice` bằng `ServiceTypes[]` và `PriceRanges[]` (mỗi phần tử có MinPrice/MaxPrice). Các loại dịch vụ ghép OR; các khoảng giá ghép OR; giữa loại, giá và keyword ghép AND. Mảng bỏ qua/null/rỗng không lọc tiêu chí đó. Cho phép một đầu mút; từ chối enum không hợp lệ, khoảng null/rỗng, giá âm hoặc min > max. Khoảng giá bao gồm hai đầu mút; khoảng chồng lấn và loại lặp không nhân bản kết quả. Predicate lọc thực hiện ở DB trước total/sort/paging. Client phải đổi request sang mảng; các field đơn cũ bị trả 400 để tránh âm thầm bỏ bộ lọc. Không đổi schema/DB. Docker rebuild và smoke test `customer-service-filter.ps1` PASS, gồm hai khoảng 30.000–50.000/90.000–100.000 loại bỏ dịch vụ 60.000, chọn nhiều loại, AND giữa tiêu chí, OR trong tiêu chí, khoảng chồng lấn, mảng rỗng/null, dữ liệu sai và phân trang. Fixture tự dọn.

Đồng bộ đặc tả ngày 05/11/2026: đã khôi phục `../Đặc tả usecase.docx` từ bản cập nhật luồng hủy đơn lưu ngày 31/10, sau đó **đã áp dụng các thay đổi 03–05/11 vào file Word chính** theo yêu cầu tiếp tục của người dùng. Sửa 5 bảng: đăng ký (điện thoại 10 số), quên mật khẩu (gửi OTP → xác minh → nhập mật khẩu mới, phiên 10 phút dùng một lần), xem/cập nhật hồ sơ (bỏ District, bắt buộc phone/address/city, giới hạn 500/100 ký tự và quyền avatar), tạo đơn (tìm kiếm/lọc loại/khoảng giá, sắp tên/giá, phân trang dịch vụ). Giữ nguyên 73/78 bảng khác và mọi phần package ngoài word/document.xml, không thêm tiêu đề use case trùng. Đã xác minh nội dung XML, cấu trúc ZIP và SHA256 bản chính khớp bản sửa. Bản trước khi đồng bộ: `%TEMP%/washgo-spec-before-sync-20261105-3045d46a9ccd457388008770cd46cc6e.docx`. **Chưa xác minh bố cục trang bằng hình ảnh/PDF**: runtime renderer đóng gói không có, Word ExportAsFixedFormat bị treo; không kết luận tài liệu bị hỏng. Không sửa bản gốc Downloads hay các backup đã commit.

Ghi nhận ngày 05/11/2026 — đồng bộ hồ sơ Customer và đăng ký: `CustomerPhoneRules` dùng chung cho PUT `/customer/me`, gửi OTP đăng ký và hoàn tất đăng ký. Bắt buộc đúng 10 số ASCII bắt đầu bằng 0; trim hai đầu nhưng không tự loại chữ/dấu phân cách. PUT hồ sơ bắt buộc điện thoại, địa chỉ, tỉnh/thành phố; địa chỉ tối đa 500, thành phố tối đa 100 ký tự. Thiếu/null/rỗng/không hợp lệ trả 400 và không đổi dữ liệu; AvatarUrl vẫn giữ semantics bỏ qua=giữ, chuỗi rỗng=xóa. DTO dùng JsonRequired để thiếu trường trả 400 tại JSON binding, tránh AbpValidationException 500 của Required trên Minimal API hiện tại. Hồ sơ được tạo bởi đăng ký/Google vẫn có thể thiếu địa chỉ/city ban đầu; chưa thêm chặn tạo đơn theo độ đầy đủ hồ sơ.

Đã bỏ District khỏi Customer entity/command/result, EF configuration và seeder; migration `20261105144241_RemoveCustomerDistrict` đã áp dụng lên DB Docker (bỏ cột customers.district), không tác động District của Merchant/Locker/ServiceArea. Trước khi bỏ cột đã sao lưu bảng customers tại `%TEMP%/washgo-customers-before-remove-district-0fa1d6f3a54241d9bb2a5782f28ad80d.sql`. Docker rebuild thành công; EF báo không có model changes còn thiếu. Smoke test `customer-profile.ps1` PASS cho điện thoại, bắt buộc/độ dài địa chỉ-city, User đồng bộ, avatar, quyền sở hữu/401/403, tổng đơn/chi tiêu; kiểm tra phone tại register/send-otp trước bước xác minh, không gửi email thật. Script giữ tài khoản test có tiền tố profile-smoke theo hành vi hiện có.

Chốt API dịch vụ Customer ngày 04/11/2026: **đã bỏ `GET /customer/services`** theo yêu cầu. Danh sách/tìm kiếm/lọc/sắp xếp/phân trang chỉ dùng `POST /customer/services/filter` với `CustomerServiceFilterQuery : BaseFilterQuery`; giữ `GET /customer/services/{id}` cho chi tiết. Ghi nhận giữ GET tương thích bên dưới là lịch sử trước quyết định này. Đã chuyển các smoke script dùng danh sách dịch vụ sang POST. Docker rebuild thành công; smoke test catalog PASS (POST, giá/tên/nhiều tiêu chí, khoảng giá, phân trang, GET cũ trả 404, GET chi tiết vẫn hoạt động); Swagger xác nhận đúng hai route còn lại.

Ghi nhận ngày 04/11/2026 — thống nhất query dịch vụ Customer: thêm `CustomerServiceFilterQuery : BaseFilterQuery` tại `Application.Contracts/Features/CustomerContracts/Queries`, tái sử dụng `Keyword`, `Page`, `Take`, `SortBy`; bổ sung `ServiceType`, `MinPrice`, `MaxPrice`. DTO mặc định page=1/take=20. `POST /customer/services/filter` nhận JSON, SortBy dùng Field=Name hoặc UnitPrice (chấp nhận price để tương thích), IsDescending; hỗ trợ nhiều tiêu chí theo thứ tự và thêm Id để phân trang ổn định. GET hiện có chuyển tham số vào cùng DTO/service, giữ tương thích. Các trường base khác (SelectFields/FromDate/ToDate/Filters/ColumnFilters) chưa triển khai cho catalog này. Không sửa base dùng chung và không đổi DB. Docker rebuild thành công; smoke test `customer-service-filter.ps1` PASS cho cả GET/POST, mặc định phân trang, nhiều tiêu chí sắp xếp, lọc khoảng giá, kiểm tra dữ liệu không hợp lệ và quyền truy cập. Fixture đã tự dọn.

Ghi nhận ngày 04/11/2026 — danh mục dịch vụ Customer: `GET /customer/services` nhận thêm `sortBy=name|price`, `isDescending` (mặc định false), `minPrice`, `maxPrice`. Giá dùng `UnitPrice`; khoảng lọc bao gồm hai đầu mút, cho phép chỉ truyền một đầu. Bỏ sortBy giữ tên tăng dần; thêm Id làm tiêu chí phụ để phân trang ổn định. Lọc trước khi tính total và phân trang, vẫn chỉ hiển thị dịch vụ active/approved/chưa xóa/giá dương. Trả 400 khi sortBy không hợp lệ, giá âm hoặc minPrice > maxPrice. Không đổi BaseFilterQuery/schema. Docker rebuild thành công; `tests/smoke/customer-service-filter.ps1` PASS (tên/giá tăng giảm, giá bằng nhau, một đầu/giá thập phân/rỗng, kết hợp keyword/serviceType, phân trang, dữ liệu ẩn và JWT). Fixture đã tự dọn.

Ghi nhận ngày 04/11/2026 (sửa logo email): thay SVG inline bằng PNG 400×400 nhúng trong assembly, hiển thị 54×54 qua `cid:washgo-logo`. SMTP gửi HTML AlternateView kèm LinkedResource image/png (multipart/related), không cần URL ảnh công khai. Giữ hình giọt nước và màu xanh từ SVG gốc; công cụ tái tạo asset: `tools/Export-WashGoEmailLogo.ps1`. Test `dotnet run --project tests/smoke/EmailLogo/EmailLogo.csproj` PASS: kiểm tra PNG embedded, CID trùng HTML, MIME email thực tế có phần ảnh. Test lưu .eml cục bộ rồi tự dọn, không gửi thư thật; cần gửi OTP mới để xác nhận hiển thị ở hộp thư người dùng.

Ghi nhận ngày 04/11/2026: đồng bộ `../styles/theme.css` với CSS xanh dương được cung cấp (light/dark, primary/ring, typography). Email OTP dùng logo giọt nước nền xanh #2563EB, header và khung mã OTP cùng màu thương hiệu; giữ màu cảnh báo theo theme. Sửa escape SVG trong chuỗi C# để template biên dịch hợp lệ. CSS đã đối chiếu khớp bản cung cấp (bỏ qua khoảng trắng).

Ghi nhận ngày 03/11/2026 — UC 1.1.3 Quên mật khẩu: chuyển sang `send-otp` (purpose = 2) → `verify-otp` → `reset-password`. Xác minh OTP trả `verificationToken` và `expiresAt`; token ngẫu nhiên 256 bit, lưu BCrypt kèm dấu phân biệt mục đích trên bản ghi OTP, hết hạn sau 10 phút và chỉ dùng một lần. `reset-password` nhận token thay cho `code`, kiểm tra email/mục đích, đổi mật khẩu và thu hồi phiên trong cùng transaction. Khóa dòng OTP để chống xác minh/reset đồng thời. Không đổi schema, không cần migration. Kịch bản Swagger: `docs/AUTH_FORGOT_PASSWORD.md`; kiểm thử hồi quy: `tests/smoke/forgot-password.ps1`.

Xác minh 03/11/2026: build local 0 warning/0 error; Docker rebuild thành công; Swagger phản ánh đúng contract mới. Smoke test PASS với tài khoản riêng: OTP sai ghi nhận lượt thử, không xác minh lại OTP đã dùng, hash token, sai email/token/mục đích, mật khẩu xác nhận không khớp, token hết hạn, reset thành công rồi chống dùng lại, mật khẩu cũ bị từ chối, JWT phiên cũ bị thu hồi, hai request reset đồng thời chỉ một request thành công. Đã dọn fixture sau test. Test seed OTP local, **chưa xác minh lại gửi/nhận thư SMTP thật trong lượt này**; không triển khai FE.

Nguồn đối chiếu: `../Đặc tả usecase.docx`. Trạng thái dưới đây đánh giá **nghiệp vụ Backend theo use case**, không đồng nghĩa có giao diện Customer hoặc đã kiểm thử với cổng thanh toán thật. “Một phần” nghĩa là đã có entity/API hoặc một nhánh xử lý nhưng chưa đủ điều kiện, quyền, dữ liệu hay kết quả mà use case yêu cầu. Các mục `[x]` ở nhật ký kỹ thuật phía dưới chỉ xác nhận phần việc kỹ thuật được ghi tại đó, **không** xác nhận toàn bộ use case đã hoàn thành.

Ghi nhận ngày 30/10/2026: bổ sung .WithSummary(...) tiếng Việt cho toàn bộ 59 endpoint trong 8 nhóm API để hiển thị mục đích trên Swagger.

Ghi nhận ngày 30/10/2026: PUT /merchant/ giữ trạng thái cũ khi bỏ hoặc gửi null cho status; hai API cập nhật hồ sơ/trạng thái Merchant chặn giá trị enum ngoài 1–3.

## 0. Tổng kết nhanh (cập nhật 03/11/2026)

Chỉ tính **Backend**; FE chưa làm. Chi tiết từng UC ở bảng mục I và các mục nhật ký bên dưới.

### ✅ Đã xong BE (có smoke test PASS trong `tests/smoke`)

| Nhóm | Use case |
| --- | --- |
| Customer | 1.1.1–1.1.3 Đăng ký, đăng nhập, quên mật khẩu; 1.1.4–1.1.5 Xem/cập nhật hồ sơ (avatar, tổng đơn, tổng chi tiêu); 1.2.1 Tạo đơn; 1.2.2 Xem danh sách locker (khoảng cách, gần nhất trước); 1.2.4 Thanh toán VNPay (chạy thật trên sandbox) + MoMo (đã tích hợp, chờ khóa test riêng để chạy thật), quá hạn 7 ngày chuyển lưu kho; 1.2.6 Theo dõi đơn; 1.2.7 Hủy đơn; 1.2.8 Lịch sử đơn; 1.3 Gửi/nhận đồ bằng QR/PIN; 1.4.1–1.4.3 Đánh giá, báo cáo sự cố, danh sách khiếu nại |
| Merchant | 2.1.1 Đăng nhập; 2.1.2 Cập nhật hồ sơ (chờ Admin duyệt); 2.3.1–2.3.2 Quản lý/chi tiết đơn; 2.3.4 Cập nhật số kg thực tế; 2.4.1–2.4.4 Quản lý và phân công Shipper; 2.5.1–2.5.3 Xem locker/ô/lịch sử; 2.6.1–2.6.3 Dịch vụ và đề xuất |
| Admin | 3.2.1–3.2.5 Quản lý Merchant + vùng phục vụ; 3.3.1–3.3.3 Phân công đơn cho Merchant (thủ công + gợi ý tự động); 3.4.1–3.4.5 Quản lý Locker (nhiều Merchant phụ trách); 3.5.1–3.5.6 Quản lý và duyệt dịch vụ; 3.6.1–3.6.5 Khuyến mãi; 3.7.1–3.7.3 Khiếu nại và đánh giá; 3.8.1–3.8.3 Tài chính; 3.9.1–3.9.4 Cài đặt hệ thống |
| Shipper | 4.1.1 Đăng nhập; 4.1.3–4.1.4 Xem/cập nhật hồ sơ; 4.3.1–4.3.2 Đơn được gán/chi tiết nhiệm vụ; 4.3.3–4.3.5 Lấy/trả đồ bằng QR; 4.3.6 Lịch sử giao nhận |
| Thông báo | 5.1–5.3 Xem, đánh dấu đã đọc, xóa (SignalR realtime); nhắc nhở theo thời gian bằng worker định kỳ |

### 🟡 Một phần

Không còn UC "một phần" ở phía BE (03/11/2026).

### ❌ Chưa làm

Không còn UC nào chưa làm ở phía BE (03/11/2026).


### Ngoài phạm vi BE / cần lưu ý

- Giao diện (Customer, Merchant, Shipper, Admin) chưa làm; `washgo-admin` đã được gỡ khỏi repo ngày 01/11.
- Chưa chạy với SMTP thật (email OTP, mật khẩu tạm). VNPay đã chạy thật trên **sandbox** (02/11), MoMo đã tích hợp API sandbox `test-payment.momo.vn` (03/11, chưa chạy thật vì khóa test công khai bị từ chối); muốn thu tiền thật cần tài khoản production (doanh nghiệp ký hợp đồng).
- Từ 03/11: `Payment:Provider` = `Mock` (mặc định, smoke test) hoặc `Live` (VNPay/MoMo sandbox thật; giá trị cũ `VnPay` vẫn hiểu là `Live`). Khách chọn `paymentMethod` 2 = VNPay, 3 = MoMo cho tiền giặt, phí lưu kho và phí hủy.
- API tạo Merchant (`POST /merchant/`) không còn nhận `userId`; FE mới phải gửi `email` để hệ thống tự tạo tài khoản.
- Từ 03/11: locker gửi `merchantIds` (nhiều Merchant, theo thứ tự ưu tiên) và trả `merchants` thay cho `merchantId`/`merchantName`; dịch vụ là bảng chung (`GET /customer/services` không còn `merchantId`/`merchantName`); đơn mới có `merchantId = null` cho tới khi Admin phân công qua `/order-assignment`.
- Đăng nhập có thêm trường tùy chọn `portal` (`Customer`/`Merchant`/`Shipper`/`Admin`): FE từng trang nên gửi để nhận "Bạn không có quyền truy cập trang này." khi sai vai trò.
- Điểm tích lũy (1.1.4) đang hiển thị giá trị lưu trong DB; đặc tả chưa có quy tắc cộng điểm nên chưa tính.
- 1.2.1 bước 6 "ô trống kèm kích thước và giá": giá đơn tính theo dịch vụ (không có giá riêng theo ô) — FE hiển thị kích thước ô + giá dịch vụ đã chọn.
- 1.2.6 bước 7 "tự động cập nhật trạng thái": mỗi lần đơn đổi trạng thái, khách nhận thông báo realtime (SignalR, `linkType = "order"`, `linkId` = id đơn) — FE nghe sự kiện này để tải lại đơn.

## I. Bản đồ use case và tiến độ hiện tại

### 1. Customer

| Use case trong đặc tả | Tiến độ BE | Đã có / phần còn thiếu |
| --- | --- | --- |
| 1.1.1 Đăng ký; 1.1.2 Đăng nhập; 1.1.3 Quên mật khẩu | Đã có BE | Email/OTP, mật khẩu, Google Customer, refresh/logout đã có; SMTP thực tế và Customer UI chưa triển khai. |
| 1.1.4 Xem hồ sơ; 1.1.5 Cập nhật hồ sơ | Đã có BE | Ngày 02/11: `GET/PUT /customer/me` có avatar (ảnh tự upload; bỏ trường = giữ, rỗng = xóa), tổng đơn và tổng chi tiêu tính từ đơn thực tế, bắt buộc địa chỉ ("Vui lòng nhập địa chỉ."), "Số điện thoại không hợp lệ.", "Cập nhật hồ sơ thành công!". |
| 1.2.1 Tạo đơn | Đã có BE | Tạo đơn, tính giá từ server, giữ ô, QR gửi đồ, hạn gửi 30 phút; báo giá + mã khả dụng (`POST /wash-order/apply-voucher`), áp mã và ghi lượt dùng trong cùng transaction; thông báo "Đơn hàng đã tạo". Ngày 02/11 đối chiếu luồng thay thế: 4a "Hiện tại không có locker khả dụng.", 9a, 11a, 14a "Ô này vừa được đặt. Vui lòng chọn ô khác."; sinh mã đơn Merchant. |
| 1.2.2 Xem danh sách locker | Đã có BE | `GET /customer/lockers` trả tọa độ, địa chỉ, số ô trống; ngày 03/11 thêm `latitude`/`longitude` của khách → `distanceKm` (Haversine tính trong SQL) và sắp xếp gần nhất trước, kể cả khi phân trang; rỗng: "Hiện tại không có locker khả dụng.". Vẽ bản đồ/định vị là phần FE. |
| 1.2.4 Thanh toán | Đã có BE | Tạo link VNPay 2.1.0 ký HMAC-SHA512, IPN kiểm tra chữ ký/mã website/số tiền, chống xử lý trùng, khóa `Paid` ở API đổi trạng thái. Ngày 02/11 chạy thật trên VNPay sandbox (Terminal mới, IPN qua Cloudflare Tunnel): thanh toán thẻ NCB → VNPay gọi IPN → đơn Paid, timeline + thông báo. Ngày 03/11: **MoMo** (API v2, ký HMAC-SHA256, `POST /payment/momo-ipn`), bước 9 báo Merchant khi khách thanh toán, câu 7a theo đặc tả, **luồng 7b** quá hạn thanh toán 7 ngày → lưu kho tại tiệm. Chế độ Mock vẫn dùng cho smoke test. |
| 1.2.6 Theo dõi đơn; 1.2.8 Lịch sử đơn | Đã có BE | `POST /wash-order/mine` lọc trạng thái + khoảng thời gian (`fromDate`/`toDate`), mỗi đơn có tên dịch vụ, kích thước ô; chi tiết + timeline + QR; rỗng: "Bạn chưa có đơn hàng nào."; cập nhật realtime qua thông báo SignalR (02/11). |
| 1.2.7 Hủy đơn | Đã có BE | Báo phí + hủy Pending 0%/30%, Deposited 30%, Collected đã cân 50%; thanh toán phí riêng và hoàn tiền có đối chiếu. Giữ ô có đồ, cho khách nhận lại hoặc Shipper trả từ tiệm qua locker. Không đổi đơn hủy sang Completed. Thông báo hủy cho khách, Merchant và Shipper có nhiệm vụ bị hủy đã có từ 01/11. |
| 1.3 Gửi đồ / nhận đồ bằng QR | BE mô phỏng đã kiểm thử | Có endpoint gửi, mở ô nhận, xác nhận nhận và nhận tại kho. Tách QR gửi/nhận; xác nhận nhận tại locker cần token phiên 10 phút, hoàn tất đơn trong transaction. Không tích hợp phần cứng. |
| 1.4.1 Đánh giá; 1.4.2 Báo cáo sự cố; 1.4.3 Danh sách khiếu nại | Đã có BE | Ngày 01/11: upload ảnh `POST /files/images`, `POST /rating`, `POST /incident` (hạn 3 ngày, ≥1 ảnh), `GET /incident/mine`; thông báo cho Admin khi có sự cố mới và cho khách khi xử lý xong. Customer UI chưa làm. |

### 2. Merchant

| Use case trong đặc tả | Tiến độ BE | Đã có / phần còn thiếu |
| --- | --- | --- |
| 2.1.1 Đăng nhập; 2.1.2 Cập nhật hồ sơ | Đã có BE | Ngày 01/11: `/merchant-profile` Merchant xem hồ sơ cửa hàng và gửi yêu cầu cập nhật (chờ Admin duyệt, báo Admin); `/merchant-profile-request` Admin duyệt/từ chối có lý do. |
| 2.3.1 Quản lý đơn; 2.3.2 Xem chi tiết đơn | Đã có BE | Phạm vi theo Merchant, ẩn thông tin khách/PIN/QR. Ngày 02/11: mã đơn Merchant (`MC…`, đơn cũ được điền bù), tên dịch vụ, kích thước ô, cờ `isNew` (đã gửi đồ, chưa phân công Shipper), QR nhận diện đơn không chứa mã mở tủ (`orderQrString`), thông báo rỗng. |
| 2.3.4 Cập nhật số kg thực tế | Đã có BE cân/chốt giá | `PUT /wash-order/{id}/actual-weight` dành cho Merchant của đơn: tính phí/ETA theo mức 3/5/10 kg, lưu giá và thời lượng gói khi đặt đơn, chống phí trùng. Theo quyết định mới: cân xong giữ `Collected`, giặt xong mới `AwaitingPayment`. Thông báo xử lý/chờ thanh toán cho khách đã có (module 5). |
| 2.4.1–2.4.3 Quản lý Shipper | Đã có BE | Ngày 01/11: `/shipper` cho Merchant (tiệm của mình) và Admin: danh sách/lọc, thêm (tạo tài khoản Shipper, gửi mật khẩu tạm qua email), sửa, Đang làm việc/Tạm nghỉ; đếm số đơn đã giao. |
| 2.4.4 Phân công Shipper | Đã có BE | Ngày 29/10: Merchant/Admin xem Shipper khả dụng theo đơn và phân công đúng tiệm, đúng giai đoạn, tài khoản đang hoạt động/khả dụng. Lưu riêng nhiệm vụ lấy/trả và thời điểm; chống phân công đồng thời. Shipper nhận thông báo nhiệm vụ mới (module 5, 01/11). |
| 2.5.1–2.5.3 Xem locker/ô/lịch sử | Đã có BE | Merchant xem các locker mình phụ trách (một locker có thể có nhiều Merchant, 03/11), chi tiết ô có cờ `hasItems`; tạo/sửa locker và đổi trạng thái ô chỉ Admin; `GET /locker/{id}/usage-history` lịch sử từng lượt dùng ô. |
| 2.6.1–2.6.3 Dịch vụ và đề xuất thay đổi | Đã có BE | `/merchant-service` Merchant xem **bảng dịch vụ chung** (03/11: dịch vụ và giá dùng chung cho mọi Merchant), đề xuất thêm dịch vụ / sửa giá; Admin duyệt thì áp dụng chung cho cả hệ thống. Tên dịch vụ không trùng trong toàn bảng (kể cả đề xuất đang chờ), mỗi dịch vụ một đề xuất chờ duyệt. |

### 3. Admin

| Use case trong đặc tả | Tiến độ BE | Đã có / phần còn thiếu |
| --- | --- | --- |
| 3.2.1–3.2.5 Quản lý Merchant | Đã có BE | Ngày 01/11: tạo Merchant kèm tài khoản + email mật khẩu tạm, kiểm tra email/MST/tọa độ/SĐT/chiết khấu; sửa đủ trường hợp đồng/SLA/ảnh; khóa cần lý do + xác nhận khi còn đơn, chặn đăng nhập; thiết lập vùng phục vụ với độ ưu tiên. |
| 3.3.1–3.3.3 Phân công đơn cho Merchant | Đã có BE | Ngày 03/11 (mô hình nhóm chốt): đơn tạo ra chưa có Merchant; sau khi khách gửi đồ, Admin chọn một trong các Merchant phụ trách locker. `/order-assignment`: danh sách đơn chờ (highlight chờ quá 60 phút, nhắc Admin), Merchant có thể nhận (độ ưu tiên, đánh giá, tải lượng X/Y, khoảng cách), gán thủ công, gợi ý tự động + xác nhận theo lô. |
| 3.4.1–3.4.5 Quản lý locker | Đã có BE | Danh sách có Merchant phụ trách/khu vực/số ô trống, lọc khu vực; tạo/sửa kiểm tra tên trùng, tọa độ, khu vực, ảnh và **nhiều Merchant phụ trách theo thứ tự ưu tiên** (03/11, `merchantIds`); xóa mềm khi không còn đơn; ô tủ chỉ chuyển Trống ↔ Bảo trì; lịch sử theo từng ô. |
| 3.5.1–3.5.6 Quản lý và duyệt dịch vụ | Đã có BE | `/wash-service` chỉ Admin, quản lý bảng dịch vụ chung (`merchantId` chỉ ghi Merchant đã đề xuất, không bắt buộc), chặn tên trùng toàn bảng, xóa mềm (chặn khi còn đơn chưa xong); `/service-proposal` duyệt (tạo/cập nhật dịch vụ chung) hoặc từ chối có lý do, báo Merchant. |
| 3.6.1–3.6.5 Khuyến mãi | Đã có BE | Ngày 01/11: CRUD `/promotion`, lọc theo trạng thái/thời gian, bật/tắt, xóa khi chưa dùng, thống kê + xuất CSV. Chi tiết ở mục khuyến mãi bên dưới. |
| 3.7.1–3.7.3 Khiếu nại và đánh giá | Đã có BE | Ngày 01/11: lọc/xử lý/từ chối khiếu nại, bồi thường 30/70/100/200% do server tính, ghi nhận chi trả; lọc/ẩn đánh giá, cập nhật điểm Merchant. |
| 3.8.1–3.8.3 Tài chính | Đã có BE | Ngày 01/11: `/finance` báo cáo doanh thu (tổng, theo Merchant, theo dịch vụ, theo ngày, xuất CSV), hoa hồng theo kỳ, kỳ đối soát + xác nhận chi trả; danh sách hoàn tiền lọc trạng thái, có mã đơn/tên khách và chi tiết. |
| 3.9.1–3.9.4 Cài đặt hệ thống | Đã có BE | Ngày 01/11: `/system-setting` cấu hình SLA, % hoa hồng mặc định, phí quá hạn/lưu kho, % phí hủy, % bồi thường; các luồng đơn đọc cấu hình thay cho số viết cứng. |

### 4. Shipper và 5. Thông báo

| Use case trong đặc tả | Tiến độ BE | Đã có / phần còn thiếu |
| --- | --- | --- |
| 4.1.1 Đăng nhập; 4.1.3–4.1.4 Hồ sơ | Đã có BE | Ngày 02/11: `/shipper-profile` xem (Merchant phụ trách, tổng đơn đã giao, đánh giá) và sửa họ tên, SĐT, avatar, phương tiện, biển số; đăng nhập báo khóa "…liên hệ Merchant." và kiểm tra `portal`. |
| 4.3.1–4.3.2 Xem đơn được gán/chi tiết | Đã có BE | Ngày 02/11: `/wash-order/filter` của Shipper lọc `taskType`, `pendingOnly`, cờ `isNew` cho nhiệm vụ chưa làm, thông báo rỗng; chi tiết có locker + địa chỉ + ô của nhiệm vụ, ghi chú từ Merchant (`shipperNote`, nhập khi phân công), nút hành động `nextAction`. |
| 4.3.3–4.3.5 Lấy/trả đồ bằng QR, cập nhật giao nhận | Đã xong BE | Đã hoàn tất 6 endpoint mô phỏng QR/PIN: Customer gửi đồ, Shipper lấy đồ, Shipper trả đồ, Customer nhận đồ tại Locker (2 bước mở tủ + xác nhận), Customer nhận đồ tại kho. Có chống brute-force (sai 5 lần khóa 5 phút), mã nhiệm vụ riêng cho Shipper, SLA warning, idempotency, tự chuyển Returned -> Completed. |
| 4.3.6 Lịch sử giao nhận | Đã có BE | Ngày 02/11: `POST /shipper-delivery/history` từ timeline do Shipper ghi khi lấy/trả đồ; lọc thời gian, loại nhiệm vụ; "Bạn chưa có lịch sử giao nhận." |
| 5.1–5.3 Xem, đánh dấu đã đọc, xóa thông báo | Đã có BE | Ngày 01/11: `/notification` (xem, đếm chưa đọc, đọc một/tất cả, xóa một/tất cả), SignalR `/hubs/notifications`, thông báo theo sự kiện cho 4 vai trò, nhắc nhở theo thời gian bằng worker định kỳ. |

## II. Thứ tự triển khai tiếp theo theo đặc tả

1. **Khóa quyền và thanh toán — ĐÃ XONG đợt 29/10/2026:** Chặn `Paid` và `Cancelled` ở API đổi trạng thái công khai; xác nhận thanh toán bằng private method trong `PaymentAppService` cho VNPay IPN/sandbox, kiểm tra trạng thái giao dịch/đơn, số tiền và ghi timeline trong transaction. Ép phạm vi Merchant/Shipper ở danh sách, bộ lọc cột, xem chi tiết, đổi trạng thái và chuyển kho; Merchant chỉ đổi các bước xử lý giặt, Shipper chỉ đổi bước thu gom/trả đồ trên đơn được gán. Response nhân viên che tên/điện thoại/ID khách, PIN/QR và thông tin khách trong timeline. Lấy actor từ JWT.
2. **2.4.4 Phân công Shipper — ĐÃ XONG BE đợt 29/10/2026 (thông báo Shipper bổ sung ngày 01/11):** Có danh sách Shipper khả dụng và phân công theo quyền Merchant/Admin; kiểm tra đơn, tiệm, tài khoản, trạng thái sẵn sàng và chuyến đang chờ xử lý. Khóa order/shipper trong transaction để chống hai đơn tranh một Shipper. Chi tiết và giới hạn ở phần xác minh bên dưới.
3. **2.3.4 Cân kg — ĐÃ TRIỂN KHAI BE ngày 29/10/2026:** API cân/chốt giá cho Merchant, mức bao gồm 3/5/10 kg, phụ phí 15.000đ/kg vượt thực tế, thời gian cơ bản của gói + 2 giờ x ceil(kg vượt). Giá gói được lưu khi đặt đơn, không nhân thêm kg ước tính và không cộng dồn phụ phí khi sửa cân nặng. Người dùng chốt lại luồng: cân và chốt giá trước, giữ `Collected`; giặt xong mới yêu cầu thanh toán ở `AwaitingPayment`. Word đã được cập nhật theo quyết định này.
4. **1.3 và 4.3 QR/PIN — ĐÃ HOÀN TẤT BE ngày 31/10/2026:** Hoàn tất trọn bộ 6 endpoint chuyên biệt theo từng hành động với kiểm tra mã, quyền, hạn giờ, chống brute-force và cập nhật trạng thái ô tủ trong cùng transaction. Khách gửi đồ (`POST /deposit`) trong 30 phút. Shipper lấy đồ (`POST /shipper-collect`) giải phóng ô gửi, kiểm tra SLA 4h. Shipper trả đồ (`POST /shipper-return`) tự tìm ô kích thước phù hợp, kiểm tra SLA 24h, bắt buộc đơn đã thanh toán (Paid). Khách nhận đồ tại tủ 2 bước: mở tủ (`POST /open-pickup-box`) cấp token 10 phút, xác nhận đóng tủ (`POST /confirm-pickup`) giải phóng ô, ghi `Returned` rồi tự động `Completed` trong cùng transaction. Khách nhận đồ tại kho (`POST /pickup-storage`) tính đúng phí lưu kho (100k/ngày) mà không đụng vào ô tủ. Chặn toàn bộ đường tắt qua API status chung.

5. **1.2.7 Hủy đơn — triển khai ngày 31/10/2026:** Một API hủy tính phí 0%/30%/50%, báo phí trước xác nhận, khóa order, giữ ô có đồ, thu phí qua sandbox/VNPay trước khi khách nhận lại. Đồ tại tiệm được phân công Shipper trả về locker. Chỉ giải phóng ô Reserved khi hủy Pending; ô Occupied được giải phóng sau QR/PIN + phiên xác nhận nhận lại. Hoàn tiền tự tạo khi có thanh toán gốc hợp lệ, trừ phí đã giữ lại; còn module thông báo.
6. **3.6 Voucher — ĐÃ XONG BE ngày 01/11/2026** (xem mục bên dưới).
7. **1.4.1–1.4.3/3.7 Đánh giá, sự cố–khiếu nại — ĐÃ XONG BE ngày 01/11/2026** (xem mục bên dưới). 
8. **5.1–5.3 Thông báo — ĐÃ XONG BE ngày 01/11/2026** (xem mục bên dưới). 
9. **3.9 Cài đặt hệ thống, nhắc nhở theo thời gian, 2.4.1–2.4.3 Quản lý Shipper — ĐÃ XONG BE ngày 01/11/2026** (xem các mục bên dưới). 
10. **3.8 Tài chính, 2.5 Locker theo phạm vi Merchant, 2.6/3.5 Đề xuất – duyệt dịch vụ, 2.1.2 Merchant cập nhật hồ sơ — ĐÃ XONG BE ngày 01/11/2026** (xem các mục bên dưới).
11. **3.2 Quản lý Merchant + vùng phục vụ, 3.4 Quản lý Locker — ĐÃ XONG BE ngày 01/11/2026** sau khi đối chiếu lại đặc tả (xem các mục bên dưới).
12. **Ngày 02/11/2026 — ĐÃ XONG BE:** 4.1.3–4.1.4 Hồ sơ Shipper, 4.3.1–4.3.2 nhiệm vụ của Shipper, 4.3.6 Lịch sử giao nhận, 2.3.1–2.3.2 đơn của Merchant, 1.2.6/1.2.8 đơn của Customer, 1.1.4–1.1.5 hồ sơ Customer, đối chiếu 1.2.1 và câu thông báo đăng nhập; tách lại contract hồ sơ Merchant theo cấu trúc chung (xem mục bên dưới).
13. **Ngày 03/11/2026 — ĐÃ XONG BE:** 1.2.2 khoảng cách locker; bảng dịch vụ chung; locker nhiều Merchant + 3.3 Phân công đơn cho Merchant (xem mục bên dưới).
14. **Ngày 03/11/2026 — ĐÃ XONG BE:** 1.2.4 MoMo, luồng 7b quá hạn thanh toán 7 ngày, báo Merchant khi khách thanh toán (xem mục bên dưới). Không còn UC BE nào chưa làm.

**Đồng bộ đặc tả Word ngày 31/10/2026:** Đã cập nhật UC Customer gửi đồ (quá 30 phút chỉ từ chối, không tự hủy), Customer nhận tại locker hai bước và phiên 10 phút, SLA Shipper lấy 4 giờ từ DepositedAt/trả 24 giờ từ PaidAt (trễ vẫn cho giao nhận), mã riêng và khóa 5 lần/5 phút. Bổ sung UC Nhận đồ tại kho với phí locker đã chốt + 100.000đ/mỗi ngày kho bắt đầu, không giải phóng ô lần hai. Giữ yêu cầu thông báo/đánh giá trong đặc tả; đây vẫn là phần chưa triển khai. Đã lưu bản dự phòng `../Đặc tả usecase.backup-20261031-021336.docx`; kiểm tra nội dung và ZIP chỉ đổi `word/document.xml`. Chưa xác minh bố cục trang: không có Python runtime đóng gói để chạy renderer; thử xuất Word sang PDF bị timeout 25 giây.
**Dọn mục trùng ngày 31/10/2026:** Xóa đúng 3 bảng Hiển thị QR Code giống hệt cùng tiêu đề đi kèm trong bản project; giữ bảng QR còn lại trong nhóm Customer và bảng QR khác nội dung ở mục khác. Nội dung tất cả bảng còn lại được đối chiếu giữ nguyên; đánh lại mục 1.3.1–1.3.4. Bản Downloads không chỉnh sửa. Backup: `../Đặc tả usecase.backup-dedup-20261031-023310.docx`. Chưa render lại bố cục do giới hạn công cụ xem trước đã ghi ở trên.

### Locker theo phạm vi Merchant ngày 01/11/2026 — UC 2.5.1–2.5.3

- [x] **Phạm vi:** `POST /locker/filter`, `GET /locker/{id}`, bộ lọc cột chỉ trả locker của Merchant đang đăng nhập (Admin xem tất cả); locker của tiệm khác trả 404. Bỏ qua locker đã xóa mềm. Merchant chưa có locker: "Bạn chưa được gán locker nào."
- [x] **Chỉ xem:** `POST /locker/`, `PUT /locker/`, `PUT /locker/box-status` chuyển sang `AdminOnly` (trước đây Merchant gọi được).
- [x] **Trạng thái ô (2.5.2):** chi tiết locker trả từng ô với trạng thái Trống/Đang có đồ/Đang giữ chỗ/Bảo trì và cờ `hasItems` cho ô đang có đồ.
- [x] **Lịch sử sử dụng (2.5.3):** `GET /locker/{id}/usage-history?fromDate&toDate&page&take` — mỗi lượt dùng ô: mã đơn, số ô, loại (1 = khách gửi, 2 = Shipper trả), thời gian đặt vào, thời gian lấy ra (null nếu đồ còn trong ô), trạng thái đơn; mốc thời gian lấy từ lịch sử trạng thái đơn (gửi/thu gom/trả/nhận), có nhánh đơn hủy và chuyển kho; không lộ thông tin khách. Chưa dùng: "Locker này chưa được sử dụng."
- [x] `tests/smoke/locker-scope.ps1` PASS: thông báo Merchant chưa có locker, phạm vi danh sách/chi tiết/lịch sử, Merchant bị chặn 403 khi tạo/sửa/đổi trạng thái ô, ô giữ chỗ và ô có đồ, lịch sử đầy đủ thời gian sau một vòng đơn, đơn chưa gửi không xuất hiện, lọc theo thời gian.

### Đợt 07/11/2026 — bổ sung cho Customer (theo `Docs/những cái cần làm cho customer.docx`)

Mỗi mục một nhánh `feat/ngochuu/...` → PR vào `develop` (các nhánh nối tiếp nhau, merge theo thứ tự PR #38 → #46 rồi PR của mục 9). Đã làm đủ 10 mục của file.

- [x] **1. Điều khoản dịch vụ / lần đăng nhập đầu** (`feat/ngochuu/customer_terms`): bảng `terms_policies` (phiên bản, tiêu đề, nội dung, ngày hiệu lực; seed bản 1.0) và `terms_acceptances` (tài khoản, phiên bản, thời điểm, IP, trình duyệt; mỗi tài khoản một dòng/phiên bản). `GET /policy/current` công khai: điều khoản + quy định đang áp dụng lấy từ cài đặt hệ thống (hạn gửi đồ, % phí hủy, phí quá hạn/lưu kho, 7 ngày chưa thanh toán, % bồi thường) + `serverNow`. `GET /policy/customer` thêm `accepted`, `acceptedAt`, `acceptedVersion`, `requiresAcceptance`; `POST /policy/customer/acknowledge {version}` (sai phiên bản → 400, gọi lại không tạo dòng mới). Kết quả đăng nhập/Google/làm mới phiên/`/auth/me` có `firstLogin`, `requiresTermsAcceptance`, `termsVersion` → hiện điều khoản đúng lần đầu trên mọi thiết bị, và hiện lại khi Admin phát hành phiên bản mới (`POST /policy`, `GET /policy/versions`). Test `customer-terms.ps1` PASS.
- [x] **2. Thông báo Customer** (`feat/ngochuu/notification_sync_filter`): đọc/đánh dấu tất cả/xóa/xóa tất cả đẩy sự kiện SignalR `notificationState` {action, ids, unreadCount} tới các thiết bị khác của cùng tài khoản (sau khi commit). `POST /notification/filter`: từ khóa (tiêu đề/nội dung), nhóm `order/payment/promotion/feedback/system`, loại, chưa đọc, khoảng ngày (Unix giây), sắp xếp `newest/oldest/unreadFirst`, phân trang; kèm `counts` tổng/chưa đọc theo nhóm và `unreadTotal` của cả hộp thư. Đa ngôn ngữ: cột mới `message_key`, `message_args` (jsonb, migration `AddNotificationMessageKeys`); mọi thông báo đơn hàng của Customer/Merchant/Shipper/Admin có khóa riêng (vd. `orderCreated.customer`, `paymentSucceeded.storage.merchant`) và tham số (mã đơn, locker, ô, số tiền, hạn lấy…); thông báo cũ/khác dùng khóa mặc định `notification.<loại>`. `title/content` tiếng Việt giữ nguyên. Push end-to-end bằng giao dịch thật đã có trong `notifications.ps1` (tạo đơn thật → SignalR). Nhóm khuyến mãi chưa có loại thông báo nào (đếm 0). Test `notification-filter-sync.ps1` PASS; `notifications`, `reminders` PASS.
- [x] **3 + 10. Hạn gửi đồ và thông báo quá hạn gửi** (`feat/ngochuu/order_deposit_deadline`): `orders.deposit_deadline` được chốt lúc tạo đơn = giờ tạo + "thời gian gửi đồ tối đa" đang cấu hình (làm tròn giây); Admin đổi SLA chỉ áp dụng cho đơn tạo sau. Kiểm tra gửi đồ, phí hủy Pending trễ, lời nhắc gửi đồ dùng chung mốc này (`OrderDepositRules`). Migration `BackfillDepositDeadlines` đặt hạn cho đơn cũ. Response tạo đơn/chi tiết/danh sách có `depositDeadline` (UTC), `depositWindowMinutes`, `serverNow`, `isDepositExpired`, `depositSecondsLeft`; Customer đọc thời gian giữ ô qua `GET /policy/current`. Worker gửi **một lần** thông báo mới `DepositExpired` (16) "Đơn #… đã quá hạn gửi đồ… Vui lòng hủy đơn để giải phóng ô và tạo đơn mới." (không tự hủy, đúng UC 1.3.2 6b); câu "gửi đồ trong N phút" khi tạo đơn lấy theo hạn thật. Test `deposit-deadline.ps1` PASS; các test cũ giả lập quá hạn bằng `created_on` được dời cả `deposit_deadline`; `order-cancellation`, `promotions`, `system-settings`, `reminders`, `qr-pin-flow` PASS.
- [x] **4. Tủ nhận đồ trong chi tiết đơn** (`feat/ngochuu/order_return_locker`): `GET /wash-order/{id}` thêm `returnLockerName`, `returnLockerAddress`, `returnLockerLatitude`, `returnLockerLongitude` (tủ trả; khách không chọn thì là tủ gửi) và `lockerAddress`, `lockerLatitude`, `lockerLongitude` của tủ gửi, để FE chỉ đường trước khi mở phiên nhận đồ. Luồng nhận lại đồ sau hủy và nhận đồ tại kho đã có BE từ trước. Test `return-locker-info.ps1` PASS; `shipper-tasks`, `order-list` PASS.
- [x] **5. Video cho đánh giá** (+ video minh chứng cho khiếu nại của mục 6) (`feat/ngochuu/feedback_video`): `POST /files/videos` (form field `file`) nhận MP4/MOV/WEBM theo chữ ký file, tối đa 50MB (nâng giới hạn body riêng cho endpoint), lưu cùng thư mục upload của tài khoản và phục vụ qua `/uploads/...`. Đánh giá và khiếu nại có `videoUrls` (tối đa 2, chỉ video do chính mình upload; migration `AddFeedbackVideos`, cột `text[]` mặc định rỗng). Khiếu nại cần ít nhất 1 ảnh **hoặc** 1 video. Test `feedback.ps1` mở rộng PASS.
- [x] **6. Khiếu nại phía Customer** (`feat/ngochuu/customer_incident`): service mới `CustomerIncidentAppService`. `POST /incident/mine/filter` (trạng thái/loại nhiều lựa chọn, mã đơn, khoảng ngày, sắp xếp newest/oldest/status). `GET /incident/eligibility/{orderId}` (`canReport`, `reason`, `completedAt`, `reportDeadline` = nhận đồ + 3 ngày, `pendingIncidentId`, `incidentIds`). Chống gửi trùng: `clientRequestId` (≤64 ký tự, unique theo khách) — gửi lại trả khiếu nại cũ; `GET /incident/mine/request/{clientRequestId}`. Sửa khi còn chờ xử lý `PUT /incident/{id}`; rút `POST /incident/{id}/withdraw` (trạng thái mới `Withdrawn` = 4, gọi lại không lỗi, sau khi rút không sửa/duyệt được và đơn báo cáo lại được). Trao đổi/bổ sung minh chứng: bảng `incident_messages`, `GET/POST /incident/{id}/messages` (Customer khi còn chờ xử lý, Admin bất cứ lúc nào), thông báo loại mới `IncidentMessage` (17) cho bên kia. Migration `AddCustomerIncidentActions`. Test `incident-customer.ps1` PASS; `feedback.ps1` PASS.
- [x] **7. Tiến độ thực tế** (`feat/ngochuu/order_progress`): danh sách và chi tiết đơn có `progressPercent` và `workflowSteps` [{status, name, isApplicable, isCompleted, isCurrent, timestamp}] (`OrderProgressCalculator`). Bước xử lý áp dụng theo loại dịch vụ của đơn: giặt thường/chăn = giặt, sấy, gấp; giặt hấp/chỉ ủi = giặt, ủi, gấp; giày = giặt, sấy (Merchant vẫn có thể bỏ qua hoặc thêm bước — bước đã đi qua luôn được tính). Phần trăm = số bước áp dụng đã tới / tổng bước áp dụng (bước hiện tại tính là đã tới; hoàn tất = 100). Bước "Lưu kho" chỉ áp dụng khi đơn đã chuyển kho; đơn hủy giữ phần trăm lúc hủy, các bước sau không áp dụng, bước "Đã hủy" là bước hiện tại. Test `order-progress.ps1` PASS; `order-list`, `shipper-tasks` PASS.
- [x] **8. Dự đoán ETA** (`feat/ngochuu/eta_prediction`): chọn cách thống kê từ đơn cũ (không gọi AI bên ngoài). `EtaPredictor` lấy các đơn 90 ngày gần nhất đã giặt xong: trung vị thời gian xử lý tại tiệm (lấy đồ → chờ thanh toán) cùng tiệm + cùng loại dịch vụ chính (≥ 3 mẫu), không đủ thì toàn hệ thống cùng loại dịch vụ, không đủ nữa thì dùng ETA quy tắc. Đơn chưa lấy đồ cộng thêm trung vị thời gian chờ lấy (hoặc SLA lấy đồ). Hệ số tải: mỗi đơn tiệm đang xử lý vượt 5 đơn +5%, tối đa +50%. Chi tiết đơn có `predictedFinishAt`, `predictionStatus` (predicted/rule/done/notApplicable), `predictionSource` (history-merchant/history-service/rule/actual), `predictionSampleSize`, `predictionLoadFactor`, `predictionUpdatedAt`; `estimatedFinishAt`/`expectedDeliveryTime` vẫn là ETA theo quy tắc. Test `eta-prediction.ps1` PASS; `order-security` PASS.
- [x] **9. Biên lai** (`feat/ngochuu/order_receipt`): nhóm chọn biên lai nội bộ + PDF (hóa đơn GTGT điện tử thật cần nhà cung cấp hóa đơn điện tử — ngoài phạm vi đồ án). `GET /wash-order/{id}/receipt` (JSON) và `GET /wash-order/{id}/receipt/pdf` (A5) cho khách chủ đơn, Merchant của đơn, Admin; đơn phải có giao dịch thanh toán thành công. Nội dung: số biên lai `BL` + mã đơn và ngày xuất (cố định từ lần đầu, cột mới `receipt_number`/`receipt_issued_at`, migration `AddOrderReceipt`), bên bán (Merchant, MST, địa chỉ, ĐT), bên mua, dòng dịch vụ, phụ phí vượt cân, giảm giá, tiền giặt, phí lưu kho/hủy đã thu, tổng đã thanh toán, giao dịch; `vatRate = null` và ghi chú "không phải hóa đơn GTGT". PDF dùng QuestPDF (Community) + font Be Vietnam Pro (SIL OFL, nhúng trong assembly). Test `order-receipt.ps1` PASS.

### Đợt 07/11/2026 (2) — Merchant quản lý đơn và Shipper (theo `Docs/BACKEND_MERCHANT_ORDER_MANAGEMENT.md`, `Docs/BACKEND_MERCHANT_SHIPPER_MANAGEMENT.md`)

Chỉ làm BE. Mỗi phần một nhánh nối tiếp nhau, merge theo thứ tự PR.

- [x] **Sửa merge** (`fix/ngochuu/restore_order_progress_dto`): merge `de9b851` làm mất `ProgressPercent`/`WorkflowSteps`/`OrderWorkflowStepDto` khỏi `WashOrderResultDto` nên `develop` không build; thêm lại.
- [x] **Thống kê đơn + lọc ngày + mã nội bộ + hạn thanh toán** (`feat/ngochuu/merchant_order_summary`): `POST /wash-order/summary` trả `total`, `statusCounts` (14 trạng thái + `byStatus`), `fromDate/toDate`, `generatedAt` bằng một truy vấn GROUP BY, cùng bộ lọc của danh sách (keyword, paymentStatus, fromDate/toDate…) nhưng bỏ qua status/trang/sắp xếp; scope theo JWT. `fromDate/toDate` (Unix giây, tính cả hai đầu, 0 = không giới hạn) được kiểm tra ở `/filter`, `/mine`, `/summary`, `/column-distinct-values`: âm, quá năm 9999 hoặc khoảng ngược trả 400. Keyword tìm thêm `merchantOrderCode` (không mở tìm tên/SĐT khách cho Merchant/Shipper). DTO đơn thêm `paymentDeadline`, `isPaymentOverdue`, `paymentSecondsLeft`, `isPickupOverdue`, `overdueTypes` (`DepositExpired`/`PaymentOverdue`/`PickupOverdue`). Test `merchant-order-summary.ps1` PASS; `order-list` PASS.
- [x] **Thao tác được phép** (`feat/ngochuu/merchant_order_actions`): chi tiết đơn (Merchant/Admin) có `allowedActions`: `allowedNextStatuses`, `statusOptions` (Washing…AwaitingPayment, mỗi bước có `allowed`/`reasonCode`/`reason`), `canUpdateWeight` + lý do + `weightAlreadySet`, `canAssignShipper` + `assignTaskType` + lý do + `hasAssignedShipper`. Luật nằm chung trong `OrderActionRules` và được chính `PUT /wash-order/status`, `PUT /actual-weight`, `PUT /assign-shipper`, `GET /available-shippers` dùng, nên metadata và kiểm tra khi ghi không lệch nhau. Mã lý do: `RoleNotAllowed`, `InvalidTransition`, `WeightRequired`, `WrongStage`, `LegacyPricing`, `NoWeightService`, `NoMerchant`. Kiểm tra chuyển trạng thái sai đứng trước kiểm tra thiếu cân. Customer/Shipper nhận `null`. Test `merchant-order-actions.ps1` PASS; `actual-weight`, `shipper-assignment`, `order-security` PASS.
- [x] **Ghi đơn an toàn** (`feat/ngochuu/merchant_order_write_safety`): cột `orders.revision` do trigger `trg_orders_revision` tăng ở mọi UPDATE (EF, SQL thô, worker), trả trong DTO đơn. `PUT /wash-order/status`, `PUT /{id}/actual-weight`, `PUT /assign-shipper` nhận thêm (tùy chọn) `expectedRevision` (hoặc header `If-Match`), `expectedStatus`, `operationId` (hoặc header `Idempotency-Key`). Lệch phiên bản/trạng thái → 409 + đơn hiện tại + `meta` (`code` RevisionMismatch/StatusMismatch, `currentRevision`, `currentStatus`). `operationId` đã thành công → 200 trả lại kết quả, `meta.replayed = true`, không chuyển trạng thái/tạo nhiệm vụ lần nữa (kể cả 2 request đồng thời, kể cả khi đơn đã đổi sau đó); dùng lại key cho yêu cầu khác → 409 `OperationIdReused`. Thao tác thành công lưu ở bảng `order_operations` cùng transaction; `GET /wash-order/{id}/operations/{operationId}` tra kết quả sau timeout (404 = chưa thực hiện). Migration `AddOrderWriteSafety`. Test `merchant-order-write-safety.ps1` PASS.
- [x] **Shipper: avatar, sắp xếp theo đánh giá, thống kê đội** (`feat/ngochuu/shipper_list_summary`): `ShipperDto.avatarUrl` (User.Avatar, null khi chưa có, không dùng ảnh CCCD), `AvailableShipperDto.avatarUrl`; `POST /shipper/filter` nhận `sortBy: [{field: "Rating", isDescending}]`, sắp xếp toàn bộ kết quả trước phân trang, phụ FullName rồi Id, Shipper chưa có đánh giá (Rating = 0, `hasRating = false`) luôn đứng cuối; field khác → 400; không truyền giữ thứ tự cũ. `GET /shipper/summary` (Admin thêm `?merchantId=`) trả `total/working/onLeave/available/busy/offline/generatedAt` bằng một truy vấn, không theo bộ lọc bảng. Test `shipper-list-summary.ps1` (+ helper `shipper-fixtures.ps1`).
- [x] **Khóa/mở khóa tài khoản Shipper** (`feat/ngochuu/shipper_account_lock`): `POST /shipper/{id}/lock` (lý do bắt buộc ≤ 500), `POST /shipper/{id}/unlock`, `GET /shipper/{id}/audits`. Khóa = `User.IsActive = false` (khác Tạm nghỉ), khóa dòng shipper (FOR UPDATE, cùng khóa với phân công), còn nhiệm vụ lấy/trả chưa xong → 409 `HasActiveTasks`; thu hồi mọi refresh token, access token cũ bị từ chối ngay (OnTokenValidated kiểm tra phiên + User.IsActive), không đăng nhập/không được phân công; khóa lại → 200 `AlreadyLocked`. Mở khóa không đổi trạng thái làm việc/vận hành. DTO có `accountActive`, `isLocked`, `lockedAt`, `lockReason`; summary có `locked`. Bảng audit `shipper_audits` (ai, vai trò, lúc nào, lý do). Migration `AddShipperAccountLock`. Test `shipper-account-lock.ps1`.
- [x] **Xóa Shipper + khóa/xóa hàng loạt** (`feat/ngochuu/shipper_delete_batch`): `DELETE /shipper/{id}?reason=` xóa mềm (`Shipper : ISoftDelete`, `is_deleted/deleted_at/deleted_by`): ẩn khỏi danh sách/chi tiết/thống kê/phân công, khóa đăng nhập + thu hồi phiên, giữ đơn hàng và lịch sử giao nhận; giấy tờ giữ lại trên bản ghi ẩn để đối soát, không API nào trả ra; email/SĐT của tài khoản đã xóa vẫn bị giữ. Còn nhiệm vụ → 409; xóa lại → `AlreadyDeleted`. `POST /shipper/batch/lock`, `POST /shipper/batch/delete`: 1–50 ID, ID trùng xử lý một lần, mỗi Shipper một transaction (thành công từng phần), kết quả theo từng ID (`Locked/AlreadyLocked/Deleted/AlreadyDeleted/HasActiveTasks/NotFound/NotShipper`), gửi lại an toàn. Migration `AddShipperSoftDelete`. Test `shipper-delete-batch.ps1`.
- [x] **CCCD hai mặt** (`feat/ngochuu/shipper_identity_sides`): create/update nhận `identityFrontImageUrl` + `identityBackImageUrl` (đủ hai hoặc không có, khác nhau, ảnh do chính Merchant/Admin upload); update thêm `identityNumber` (null = giữ, "" = xóa) và ảnh (null = giữ, "" = xóa); đặt ảnh hai mặt thay thế ảnh cũ không rõ mặt. DTO có `identityFrontImageUrl`, `identityBackImageUrl`, `identityImageSides` (`None`/`FrontBack`/`Unspecified` cho ảnh cũ dạng mảng, không tự gán mặt); `identityImageUrls` giữ tương thích. Mỗi lần đổi giấy tờ ghi audit `IdentityUpdated` với tên trường đã đổi (không ghi giá trị). Migration `AddShipperIdentitySides`. Test `shipper-identity.ps1`.
- [x] **Lịch sử nhiệm vụ theo Shipper cho Merchant** (`feat/ngochuu/shipper_task_history`): bảng mới `delivery_tasks` ghi mốc thật của mỗi nhiệm vụ lấy/trả: phân công (`assignedAt`, người phân công), hoàn tất (`completedAt` khi Shipper lấy đồ/đặt đồ vào tủ), phân công lại (`Reassigned` + `endedAt`), đơn hủy khi nhiệm vụ chưa xong (`Cancelled`). Migration `AddDeliveryTasks` backfill: nhiệm vụ đã hoàn tất từ timeline của Shipper (không có giờ phân công → null), nhiệm vụ đang mở từ đơn hiện tại. `POST /shipper/{id}/tasks` (Merchant của Shipper/Admin, cả Shipper đã xóa; Merchant chỉ thấy đơn của tiệm mình): lọc `taskType`/`status`, phân trang BE, trả mã đơn, loại, trạng thái nhiệm vụ, các mốc, tủ/ô, trạng thái đơn. API Shipper `/shipper-delivery/history` giữ nguyên. Test `shipper-task-history.ps1`.
- [x] **Ghi Shipper an toàn** (`feat/ngochuu/shipper_write_safety`): `POST /shipper` nhận `clientRequestId` (hoặc header `Idempotency-Key`), lưu `create_request_id` (unique theo Merchant): gửi lại trả Shipper đã tạo (200, `meta.replayed`), không tạo thêm tài khoản, không gửi lại email; cùng key cho dữ liệu khác → 409; 2 request đồng thời chỉ tạo một tài khoản. Kết quả gửi email lưu thành dữ liệu: `credentialDelivery` (0 Unknown/1 Sent/2 Failed), `credentialEmailSent`, `credentialSentAt` (không trả mật khẩu). `shippers.revision` do trigger tăng ở mọi UPDATE; `PUT /shipper/{id}` nhận `expectedRevision` (hoặc `If-Match`), lệch → 409 kèm dữ liệu hiện tại; update chạy trong transaction và khóa dòng shipper nên Tạm nghỉ/khóa/xóa không chéo với phân công. Migration `AddShipperWriteSafety`. Test `shipper-write-safety.ps1`.
- [x] **Locker của Merchant** (`feat/ngochuu/merchant_locker_management`, theo `Docs/BACKEND_MERCHANT_LOCKER_MANAGEMENT.md`): `POST /locker/filter` trả `imageUrls` (đúng thứ tự lưu) và `thumbnailUrl`; `GET /locker/summary` (Admin thêm `?merchantId=`) trả số locker theo trạng thái và số ô chưa xóa theo trạng thái trong một transaction repeatable-read, locker dùng chung tính một lần. Lịch sử sử dụng: locker dùng chung → Merchant chỉ thấy lượt của đơn thuộc tiệm mình (Admin thấy hết); mỗi dòng có `usageId` (`{orderId}:{usageType}`) và `boxId`; lọc ô theo `boxId` (ô đã xóa vẫn xem được), `boxId` không thuộc locker → 400; `fromDate/toDate` âm, quá năm 9999 hoặc ngược → 400; sắp xếp ổn định (thời gian, mã đơn, loại). Test `merchant-locker-management.ps1`.
- [x] **Dịch vụ của Merchant** (`feat/ngochuu/merchant_service_management`, theo `Docs/BACKEND_MERCHANT_SERVICE_MANAGEMENT.md`): `GET /merchant-service/{id}` (dịch vụ chung đã duyệt, kể cả tạm ngừng; 404 khi không có) và danh sách trả `imageUrls`/`thumbnailUrl`, `version` (hash nội dung có thể đề xuất), `hasPendingProposal`, `myPendingProposalId`, `canProposeUpdate` + `proposeBlockedCode/Reason`. `GET /merchant-service/summary`: tổng/đang cung cấp/tạm ngừng của danh mục chung và số đề xuất Pending/Approved/Rejected của chính Merchant (repeatable-read). Danh mục sắp xếp whitelist (Name, UnitPrice, EstimatedDurationHours, CreatedOn; phụ theo Id), field khác → 400. Đề xuất: `imageUrls` null = giữ ảnh, [] = xóa hết, ảnh đang thuộc dịch vụ được giữ, ảnh mới phải do Merchant upload; `expectedVersion` lệch → 409 `StaleVersion`; `clientRequestId`/`Idempotency-Key` gửi lại trả đề xuất cũ, không báo Admin lần nữa; gửi đề xuất chạy dưới `pg_advisory_xact_lock` nên kiểm tra trùng tên/một đề xuất chờ duyệt mỗi dịch vụ không bị đua; duyệt/từ chối khóa theo từng đề xuất; duyệt đề xuất sửa khi dịch vụ đã đổi so với snapshot lúc gửi → 409 `StaleVersion`. Giá (cả giá theo kg/chiếc) > 0, ≤ 100 triệu, ≤ 2 chữ số thập phân. Lỗi có `meta.code` (InvalidPrice, DuplicateName, PendingProposal, AlreadyReviewed, MerchantInactive), giữ HTTP 400 như cũ. Lọc đề xuất thêm `keyword` (tên đề xuất/mã dịch vụ) và `serviceId`, thứ tự phụ theo Id. Migration `AddServiceProposalSafety` (cột `proposed_name` backfill từ JSON, `client_request_id` unique theo Merchant). Test `merchant-service-management.ps1`.
- [x] **Báo cáo kinh doanh Merchant** (`feat/ngochuu/merchant_report`, theo `Docs/BACKEND_MERCHANT_REPORT.md`): `GET /merchant-reports/overview` và `GET /merchant-reports/export` (CSV), MerchantOnly, phạm vi theo JWT; endpoint cũ `/merchant/revenue-report` giữ nguyên. Giờ Việt Nam (UTC+7), `period` today/week (thứ Hai→nay)/month (mùng 1→nay)/custom (`from`,`to` yyyy-MM-dd, tính cả hai ngày, tối đa 366 ngày), `granularity` hour/day/week/month (tự chọn nếu bỏ trống, tối đa 400 điểm). Kỳ so sánh liền trước cùng độ dài, kỳ chưa kết thúc so cùng tiến độ; trả khoảng cụ thể, `incomplete`, `asOf`, `generatedAt`, `definitions`. KPI có current/previous/changePercent (null + `isNew` khi kỳ trước = 0): tiền giặt (theo ngày giao dịch thành công), hoàn tiền (theo ngày hoàn), doanh thu thuần, hoa hồng theo tỷ lệ chốt trên từng đơn (hoàn tiền giảm hoa hồng cùng tỷ lệ; đơn cũ chưa có tỷ lệ đếm riêng, không dùng tỷ lệ hiện tại), thực nhận, phí hủy/lưu kho (báo riêng, không tính vào doanh thu tiệm), đơn tạo/hoàn thành (mốc Completed thật)/hủy, tỷ lệ đúng hạn có tử/mẫu số và chênh lệch điểm phần trăm. Chuỗi current/previous cùng vị trí bucket, bucket sau `asOf` đánh dấu `computed = false`. Dịch vụ bán chạy theo số đơn khác nhau (rồi giá trị, rồi Id), có kỳ trước, ảnh/tên hiện tại, `listValue` theo giá snapshot (ghi rõ trước phụ phí/giảm giá). Mọi số liệu đọc trong một transaction repeatable-read. CSV UTF-8 BOM, escape, chống formula injection, mục thiếu ghi `unavailable`. Lỗi `meta.code`: InvalidPeriod, InvalidRange, InvalidGranularity, TooManyBuckets. Test `merchant-report.ps1`.

### Đợt 12/11/2026 — phần còn lại trong `Docs/cần làm` (Admin, Merchant, đánh giá)

Chỉ làm BE; mỗi phần một nhánh nối tiếp → PR vào `develop`.

- [x] **Hồ sơ/phiên/nhật ký Admin** (`feat/ngochuu/admin_session_audit_fixes`): `version` hồ sơ Admin là chuỗi opaque (vẫn nhận số cũ); phiên đăng nhập có id logic ổn định qua làm mới token (`refresh_tokens.session_key`), `GET /auth/sessions?page=&take=` trả total/hasMore/current, DELETE thu hồi cả chuỗi token của phiên và báo `stale` khi phiên đã kết thúc; mọi thao tác ghi bị từ chối (4xx) của người đã đăng nhập được ghi nhật ký Failed/Denied kèm mã lỗi qua endpoint filter chung; nhật ký che token/URL ký số/dãy 9–12 chữ số trong lý do, mã đối tượng, trường đổi và chi tiết; cảnh báo Locker gần đầy đếm và phân trang tại DB. Test `admin-profile-security`, `audit-log`, `admin-dashboard-alerts` PASS (cập nhật test theo hợp đồng cấu hình/locker mới).
- [x] **Tài chính và hồ sơ Merchant thống nhất** (`feat/ngochuu/merchant_finance_consistency`): một quy tắc hoa hồng `CommissionMath` cho báo cáo, đối soát, breakdown và chi tiết đơn: tính theo từng khoản thu/hoàn với tỷ lệ chốt của chính giao dịch, làm tròn đến đồng, nửa đồng ra xa 0 (giống `round(numeric)` của PostgreSQL); `financialStatus` của đơn dựa trên khoản thu/hoàn thực sự gắn kỳ đối soát đã chi trả (Settled/Adjusted/Collected) kèm `settledNetAmount`/`unsettledNetAmount`; tổng lũy kế và báo cáo doanh thu cũ đọc trong snapshot repeatable-read; đếm đơn thiếu tỷ lệ bằng `COUNT` trên `UNION` tại DB; KPI tạm tính không bao giờ là "mới"; xếp hạng dịch vụ dùng Merchant được chụp trên giao dịch như số tiền. Hồ sơ Merchant trả `version` và bắt buộc `expectedMerchantVersion` khi gửi yêu cầu cập nhật (lệch → 409); `legacyBusinessLicense` phân loại giá trị giấy phép cũ (Number/Url/Text, đã có tài liệu mới hay chưa). Swagger khai báo response DTO của các API đơn chính. Backfill tỷ lệ lịch sử không chạy lại: đơn thiếu bằng chứng vẫn để thiếu snapshot và báo coverage.
- [x] **Admin Finance/Locker/Merchant/Đơn/Khuyến mãi/Dịch vụ** (`feat/ngochuu/admin_resources_followup`): kết quả batch (Locker, Promotion, Service) có `outcome` Changed/Unchanged/Skipped/Failed + `reasonCode` từng dòng và `totals` (Succeeded = Changed + Unchanged), cả ở response và tra cứu operation; đổi trạng thái ô tủ bắt buộc `expectedBoxStatus` (lệch → 409 `BoxStatusChanged`, kiểm tra dưới cùng khóa với giữ ô); `SlaDocUrl` ngừng ghi (chỉ đọc dữ liệu cũ, tài liệu mới qua `slaDocumentId`); `GET /merchant/cities` distinct tại DB; tab chờ phân công có `lockerImageUrls/lockerThumbnailUrl` và `serviceItems` kèm ảnh dịch vụ (nạp theo lô); khuyến mãi có `canChangeCode/changeCodeReasonCode`, thống kê mặc định 30 ngày và tối đa 366 ngày, metadata `generatedAt/fromDate/toDate/timeZone/basis`, CSV đủ cột TotalOrderAmount/PaidRevenue kèm kỳ và căn cứ; dịch vụ có `canEdit/canChangeAvailability/canDelete/deleteReasonCode`, summary có `pendingProposals` cùng snapshot; tài chính: dòng đối soát phân trang kèm `isSnapshot/basis/generatedAt`, CSV ghi biên kỳ giờ Việt Nam có `+07:00` và `asOf`, `POST /finance/commissions/paged` (tổng toàn kỳ độc lập trang), `granularity=hour` cho kỳ ≤ 48 giờ. Test `admin-resources-followup.ps1`.
- [x] **Đánh giá: ẩn danh hai chiều và quản lý Admin** (`feat/ngochuu/rating_anonymity_admin`): Customer nhận `CustomerRatingDto` (không có Merchant/Shipper/kiểm duyệt), gửi đánh giá có `operationId` (gửi lại trả kết quả cũ, khác nội dung → 409; `GET /rating/operations/{id}`), đơn đã đánh giá → 409 `AlreadyRated`; Merchant chỉ thấy đánh giá đang hiển thị của tiệm, không có mã đơn/định danh khách, nhận xét và media chỉ chuyển sau khi Admin kiểm duyệt (`contentReleased`, cột `moderated_at/by`); Admin: `GET /rating/{id}`, `POST /rating/summary` (cùng bộ lọc, một snapshot: tổng/hiển thị/ẩn/phân bố 1-5 sao/điểm trung bình hiển thị), lọc thêm `orderId/customerId`, kiểm tra sao/ngày/keyword, sắp CreatedOn rồi Id; `PATCH /rating/{id}/visibility` bắt buộc `expectedVersion`, có `operationId`, trạng thái đích (đã đúng → Unchanged), lý do ẩn 1–500, khóa điểm Merchant, ghi nhật ký giữ lý do ẩn cũ; `POST /rating/batch` Hide/Show 1–50 dòng với outcome từng dòng và tra cứu `/admin-operations/Rating/{id}`. Điểm Merchant tính AVG/COUNT tại DB, DTO Merchant trả `rating = null` + `hasRating` khi chưa có đánh giá công khai, dashboard làm tròn 1 chữ số như hồ sơ; sao Shipper ghi chú không còn dùng, seed không gán 5 sao. Sửa helper test đọc mật khẩu tạm theo template email mới. Test `feedback.ps1` mở rộng.
- [x] **Đối chiếu hoa hồng lịch sử + audit 403** (`feat/ngochuu/commission_review_denied_audit`): cột `orders.commission_snapshot_source` (Assignment/Legacy/LegacyUnverified/AdminReconciled); migration `AddCommissionSnapshotSource` không đoán tỷ lệ: 0% cũ (chốt trước migration nullable) chưa thuộc kỳ đối soát nào chuyển về thiếu tỷ lệ + `LegacyUnverified` (kể cả tỷ lệ chụp trên khoản thu/hoàn chưa đối soát). `GET /finance/commission-snapshots/review` liệt kê đơn cần đối chiếu (tỷ lệ hiện tại chỉ tham khảo); `POST /finance/commission-snapshots/{orderId}/reconcile` Admin xác nhận tỷ lệ kèm căn cứ 10–1000 ký tự, kiểm tra tỷ lệ lúc xem (409 RateChanged), chặn đơn đã vào kỳ đối soát (409 AlreadySettled), cập nhật cả khoản thu/hoàn chưa đối soát, ghi nhật ký trước/sau, operationId. Request bị policy chặn (403, trước khi vào endpoint) của người đã đăng nhập ghi nhật ký Denied qua `IAuthorizationMiddlewareResultHandler`. Test `commission-snapshot`, `audit-log` mở rộng.
- [x] **Rà soát lại 12 file — phần còn thiếu** (cùng nhánh `feat/ngochuu/commission_review_denied_audit`): mỗi dòng kết quả batch trả `version` mới và `isHidden`/`isActive` sau thao tác; không còn endpoint trả 5 sao mặc định cho Merchant chưa có đánh giá (gợi ý phân công, Merchant theo vùng: `rating = null`, `publicRatingCount`, `hasRating`); sắp xếp Shipper theo sao ngừng hỗ trợ (sortBy Rating cũ bị bỏ qua); số tiền từng đơn với Merchant chỉ gồm khoản thu/hoàn chụp cho chính Merchant đó khi đơn từng phân công lại (Admin thấy đủ). Test: hai Admin ẩn cùng một đánh giá cùng version (một 200, một 409), tạo đánh giá song song với lúc Admin hiển thị lại (điểm Merchant vẫn đúng), batch trả version, phạm vi tiền khi phân công lại; helper test tự sinh `code` khi tạo locker.
- [x] **Sửa lỗi tạo Shipper đồng thời + đồng bộ test** (cùng nhánh): khóa chống tạo trùng Shipper giờ giữ trong transaction tới khi tài khoản được ghi và kiểm tra lại replay sau khi lấy khóa, nên hai request cùng `Idempotency-Key` gửi đồng thời trả 201 + 200 (replay) thay vì báo trùng SĐT. Test duyệt/từ chối đề xuất dịch vụ gửi `expectedVersion`; `system-settings.ps1` bật tự điền version (không ảnh hưởng test kiểm tra thiếu version). Full regression 12/11: 67/73 → còn lại `customer-profile` (chỉ lỗi qua wrapper, chạy trực tiếp PASS), `vnpay-sandbox` (SKIP khi Mock), `merchant-create-safety` (cần SMTP override riêng của bạn tôi); các test đỏ khác đã sửa và PASS.
- [x] **Hoàn thiện 12 file — các điểm cuối** (`feat/ngochuu/full_doc_compliance`): nhật ký chỉ giữ các khóa `details` được phép theo từng module (whitelist tại nguồn, module chưa khai báo không lưu details) rồi mới lọc bí mật; điểm Merchant giữ bằng tổng/số đánh giá công khai (`merchants.public_rating_sum/count`, migration `AddMerchantRatingAggregate` tính lại từ đánh giá hiện có) và cộng/trừ đúng một lượt mỗi lần tạo/ẩn/hiện, kể cả trong batch, không tính lại cả tập; Merchant xem đánh giá qua `POST /rating/partner/filter` với DTO riêng `PartnerRatingDto`, `/rating/filter` chỉ Admin; Swagger khai báo response cho toàn bộ API đơn. Test thêm: batch bị ngắt sau khi các dòng đã ghi (gửi lại hoàn tất, không ghi lặp nhật ký/điểm), hơn 100 phiên đăng nhập, hai phiên lưu hồ sơ Admin cùng version, whitelist chi tiết nhật ký, replay đổi trạng thái ô tủ, Merchant khác đọc dòng đối soát, biên ngày/năm của CSV.
- [x] **Kiểm thử còn lại của 12 file** (`doc-scenarios.ps1`, cùng PR #99): làm tròn hoa hồng theo từng giao dịch (2 khoản 1đ ở 50% = 2đ, 1,5đ → 2đ) cho kết quả giống nhau ở tổng lũy kế, hoa hồng Admin và chi tiết đơn; hoàn nhiều lần, hoàn khác kỳ, hoàn sau kỳ đã chi trả (Adjusted, phần đã/chưa đối soát); đơn chỉ có hoàn và đơn thiếu tỷ lệ cả hai phía chỉ đếm một lần; xếp hạng dịch vụ theo Merchant chụp trên thanh toán (đơn đã đổi Merchant); ghi hoàn song song khi đang đọc báo cáo vẫn giữ net = gross − refunds, payout = net − commission; thao tác bị từ chối ở từng module (duyệt hồ sơ, đề xuất dịch vụ, phân công, tài chính, cấu hình) đều vào nhật ký, không có bản ghi thành công; URL ký số bị che; khách giữ ô giữa lúc Admin đọc và ghi → 409; tab chờ phân công với trạm dùng chung, ảnh thiếu/hỏng, trạm xóa mềm; biên tháng CSV theo +07:00.

### Đợt 08/11/2026 — Admin quản lý đơn/dashboard/hồ sơ và rà soát Merchant (theo 5 file `Docs/BACKEND_*.md` mới)

Chỉ làm BE; mỗi phần một nhánh nối tiếp → PR vào `develop`.

- [x] **Rà soát quản lý đơn** (`fix/ngochuu/merchant_order_review`): `POST /wash-order/summary` (và bộ lọc dùng chung) loại đơn xóa mềm như danh sách (WashOrder dùng ISoftDelete riêng, không có global filter). Fingerprint của `operationId` gồm cả ghi chú (`OrderWriteGuard.Fingerprint`: trường chính + SHA-256 rút gọn của note) cho status và assign-shipper: cùng mã khác ghi chú → 409 `OperationIdReused`. `MesRepository.FilterProjectedDataAsync` thêm thứ tự phụ `Id` cho mọi danh sách có sắp xếp; danh sách đơn chờ phân công thêm `ThenBy(Id)`. Test `merchant-order-review.ps1` PASS; `merchant-order-write-safety`, `order-list` PASS.
- [x] **Ảnh và liên hệ trên đơn** (`feat/ngochuu/order_media_contacts`): DTO đơn (danh sách + chi tiết) có `services` (serviceId, serviceName, serviceThumbnailUrl), `lockerImageUrls/lockerThumbnailUrl`, `returnLockerImageUrls/returnLockerThumbnailUrl` (theo returnLockerId, mặc định trạm gửi), `merchantPhoneNumber`, `merchantImageUrls/merchantThumbnailUrl`, `customerAvatarUrl` (chỉ Admin), liên hệ Shipper được giao `shipperAvatarUrl/Phone/Email/VehicleType/VehiclePlate` (chỉ Admin/Merchant; Shipper đã xóa → null); items chi tiết có `serviceImageUrls/serviceThumbnailUrl` (cả dịch vụ đã xóa mềm). Nạp theo lô cho cả trang (`OrderMediaEnricher`), ảnh là ảnh hiện tại. `AvailableShipperDto.email`. Danh sách/chi tiết của Admin không còn mã mở tủ/mã nhiệm vụ; Admin xem qua `GET /wash-order/{id}/access-codes` (AdminOnly), mỗi lần xem ghi vào lịch sử đơn. Test `order-media-contacts.ps1` PASS; `actual-weight`, `order-security`, `qr-pin-flow` PASS.
- [x] **Phân công Merchant an toàn + chuyển kho** (`feat/ngochuu/admin_order_assignment_safety`): `POST /order-assignment/{id}/assign` và `/auto-assign` khóa sức chứa theo Merchant (`pg_advisory_xact_lock`, lô khóa các Merchant theo thứ tự Id trước khi khóa đơn) nên nhiều Admin cùng lúc không vượt tải; Active/link trạm/tải đọc lại sau khi khóa. Nhận `expectedRevision`, `expectedCommissionRate` (409 `RevisionMismatch`/`CommissionChanged`), `operationId`/`Idempotency-Key` cho đơn lẻ và cả lô (lưu ở `order_operations` với `group_id`; gửi lại trả kết quả cũ, khác cặp đơn–Merchant → 409 `OperationIdReused`); `GET /order-assignment/operations/{operationId}` tra kết quả. Lỗi có `meta.code` (NotWaiting, NoMerchantInArea, MerchantNotEligible, CapacityFull…). Gợi ý Merchant có `publicRatingCount`/`hasPublicRating`. `POST /wash-order/move-to-storage` nhận expectedRevision/expectedStatus/operationId như các thao tác khác (tra cứu qua `/wash-order/{id}/operations/{operationId}`); `allowedActions` thêm `canMoveToStorage`, `moveToStorageReasonCode/Reason`, `transferToStorageEligibleAt` từ cùng luật. Migration `AddOrderOperationGroup`. Test `admin-order-assignment-safety.ps1` PASS; `order-assignment`, `payment-overdue-storage`, `merchant-order-actions` PASS.
- [x] **Admin hủy đơn quá hạn gửi đồ** (`feat/ngochuu/admin_cancel_expired_deposit`): `GET /wash-order/{id}/admin-cancellation-quote` (AdminOnly, chỉ đọc: canCancel + reasonCode, hạn gửi, giờ server, phí theo `OrderCancellationPolicy` hiện hành, tiền đã thu được xác minh, hoàn, còn thiếu, revision) và `POST /wash-order/{id}/cancel-expired-deposit` (lý do 1–500, expectedRevision/expectedStatus/`expectedFeeAmount` → 409 `QuoteChanged`, operationId/Idempotency-Key; fingerprint gồm lý do). Chỉ đơn Pending quá `depositDeadline` theo giờ server; khóa dòng đơn như luồng gửi đồ; chỉ giải phóng ô đang Reserved của đơn (không được → 409 `BoxNotReserved`, rollback); chặn khi có yêu cầu hoàn đang xử lý hoặc đã trả tiền mà không có giao dịch xác minh; ghi Cancelled/CancelledByType Admin/lý do/timeline, hoàn voucher, tạo yêu cầu hoàn nếu có, báo khách một lần. `allowedActions.canCancelExpiredDeposit` + reason code. Bộ lọc `overdueType` (DepositExpired, PaymentOverdue, PickupOverdue) cho danh sách và summary. Test `admin-cancel-expired-deposit.ps1` PASS; `order-cancellation`, `deposit-deadline` PASS.
- [x] **Hoa hồng chốt theo đơn** (`feat/ngochuu/commission_snapshot`): `orders.commission_rate_applied` nullable + `commission_locked_at`; null = đơn cũ chưa có snapshot, 0 = tỷ lệ 0% hợp lệ. Migration `NullableCommissionSnapshot` chỉ giữ 0 khi Merchant đang có tỷ lệ 0 (có thể là thật), còn lại 0 → null; giờ chốt lấy từ lịch sử phân công. Bỏ fallback sang tỷ lệ hiện tại ở `/finance/commissions` (thêm `ordersWithoutCommissionSnapshot`, `uncoveredNetAmount`, `commissionComplete`; hoàn tiền giảm hoa hồng cùng tỷ lệ, kể cả hoàn ở kỳ sau), `/merchant/revenue-report` (tính lại theo giao dịch/hoàn tiền qua `MerchantFinanceAggregator` tổng hợp tại DB, cùng định nghĩa với `/merchant-reports`; thực nhận trừ hoàn tiền; `growthRate` null + `isNewRevenue` khi kỳ trước = 0 hoặc hoa hồng chưa đủ; mốc thời gian trả UTC) và báo cáo mới. DTO đơn: `commissionStatus` (Locked/Missing), hoa hồng/thực nhận tạm tính null khi Missing; Admin/Merchant có thêm `collectedLaundryAmount`, `refundedAmount`, `commissionOnCollected`, `merchantPayoutOnCollected`, `financialStatus` (Unpaid/Collected/Adjusted/Settled). `GET /finance/settlements/{id}/orders`: chi tiết kỳ đối soát theo đơn. Test `commission-snapshot.ps1` PASS; cập nhật `merchant-features.ps1` theo hợp đồng mới (đơn Missing có hoa hồng null, thực nhận trừ hoàn tiền) — PASS; `finance`, `merchant-report`, `order-assignment` PASS.
- [x] **Báo cáo Merchant tổng hợp tại DB + tổng lũy kế** (`feat/ngochuu/merchant_report_aggregate`): `/merchant-reports/overview` và `/export` không còn tải dòng giao dịch/đơn/timeline/items về bộ nhớ: tổng tiền, hoa hồng, phí, đơn đã thu, đơn thiếu snapshot (`MerchantFinanceAggregator`), số đơn tạo/hoàn thành theo giờ/ngày địa phương, đơn hủy, tỷ lệ đúng hạn (so sánh ETA tại DB) và xếp hạng dịch vụ (đếm distinct đơn, chỉ lấy top N + số kỳ trước của các dịch vụ đó) đều là truy vấn gộp trong cùng transaction repeatable-read; bucket ghép từ kết quả gộp. `kpis.commissionCoverage` cho cả hai kỳ (đủ/chưa, số đơn và doanh thu thuần chưa xác định hoa hồng, tính trên cả khoản thu và khoản hoàn); thiếu thì `commission`/`merchantPayout` có `isEstimated = true` và `changePercent = null`. CSV ghi "tạm tính" và "thu nhập phát sinh (chưa phải số đã chi trả)". `GET /merchant-reports/lifetime`: tổng tài chính toàn lịch sử cho hồ sơ cửa hàng. Test `merchant-report.ps1`, `commission-snapshot.ps1` PASS.
- [x] **Hồ sơ Merchant: MST, giấy phép, cặp tọa độ** (`feat/ngochuu/merchant_profile_legal`): `GET /merchant-profile` trả `businessLicense` (giá trị Admin đã lưu). `POST /merchant-profile/update-requests` nhận `taxCode` (10 số hoặc 10 số-3 số, không trùng cửa hàng khác) và `businessLicense` (số giấy phép ≤ 100 ký tự hoặc ảnh do chính Merchant upload): null = giữ, chuỗi rỗng = xóa; current/proposed/changedFields có hai trường; chỉ áp dụng khi Admin duyệt (kiểm tra lại trùng MST lúc duyệt); yêu cầu cũ thiếu hai trường giữ nguyên giá trị. Vĩ độ/kinh độ phải đi cùng nhau; đổi địa chỉ hoặc thành phố bắt buộc gửi vị trí mới. Test `merchant-profile-legal.ps1` PASS; `merchant-profile`, `merchant-admin` PASS.
- [x] **Dashboard Merchant: snapshot, SLA phân trang, giờ UTC** (`feat/ngochuu/merchant_dashboard_sla`): `GET /merchant/dashboard` đọc counts/đơn mới/kỳ đối soát trong một transaction repeatable-read. `GET /merchant/sla-alerts?page=&take=&alertType=`: ba loại cảnh báo đếm tại DB trên toàn phạm vi Merchant, danh sách ưu tiên ghép bằng UNION, sắp hạn chót sớm nhất (quá hạn lâu nhất) rồi Id, chỉ tải trang yêu cầu; thêm `page`, `take`, `priorityTotal`. Mốc thời gian của dashboard/báo cáo cũ/SLA trả UTC có `Z` thay cho "cộng 7 giờ nhưng vẫn gắn Z" (FE tự đổi sang giờ Việt Nam). Test `merchant-dashboard-sla.ps1` PASS; `merchant-features` PASS.
- [x] **Nhật ký hoạt động** (`feat/ngochuu/audit_log`): bảng `audit_logs` chỉ ghi thêm (thời điểm UTC, người thao tác id/tên/vai trò, module, thao tác, đối tượng id/mã, kết quả Success/Failed/Denied, lý do/mã lỗi, tên trường đã đổi, chi tiết đã lọc mật khẩu/OTP/token/mã mở tủ/giấy tờ, correlationId). Ghi: đăng nhập thành công/thất bại/sai cổng, đăng xuất; Merchant tạo/sửa/khóa/mở khóa; Shipper tạo/sửa/khóa/mở khóa/xóa; duyệt/từ chối yêu cầu hồ sơ và đề xuất dịch vụ; phân công Merchant (đơn lẻ/tự động); mọi thao tác đơn qua write guard (trạng thái, cân nặng, giao Shipper, chuyển kho, Admin hủy quá hạn gửi đồ); Admin xem mã mở tủ; duyệt/từ chối/xác nhận hoàn tiền; tạo/chi trả kỳ đối soát; đổi cấu hình hệ thống (trường đổi + giá trị trước/sau). API Admin: `POST /admin/audit-logs/filter`, `GET /admin/audit-logs/{id}`, `GET /admin/me/activities`; không có API sửa/xóa. Test `audit-log.ps1`.
- [x] **Hồ sơ Admin và bảo mật tài khoản** (`feat/ngochuu/admin_profile_security`): `GET/PUT /admin/profile` (AdminOnly, theo JWT): email, họ tên, SĐT, ảnh, ngày tham gia, trạng thái tài khoản, lần đăng nhập gần nhất, `version`; chỉ sửa họ tên/SĐT/ảnh (ảnh tự upload, `""` xóa, null giữ), bắt buộc `expectedVersion`, lệch → 409 kèm hồ sơ mới nhất, lưu lại cùng nội dung không đổi version. `GET /auth/security` (có mật khẩu, lần đổi mật khẩu, Google, số phiên; 2FA chưa hỗ trợ). `POST /auth/change-password` (mật khẩu hiện tại, chính sách mật khẩu, khác mật khẩu cũ, mặc định đăng xuất phiên khác và giữ phiên hiện tại; sai 5 lần/15 phút → 429). `GET /auth/sessions`, `POST /auth/sessions/revoke-others` (giữ phiên hiện tại), `DELETE /auth/sessions/{id}`. Phiên lưu thời điểm đăng nhập (giữ qua làm mới token) và User-Agent; `users.password_changed_at`. Ghi nhật ký cho mọi thay đổi. Test `admin-profile-security.ps1` PASS.
- [x] **Dashboard và cảnh báo Admin** (`feat/ngochuu/admin_dashboard_alerts`): `GET /admin/dashboard?period=today|week|month|custom|all&topMerchants=` đọc một snapshot repeatable-read, có `generatedAt` và mốc UTC: Customer đang hoạt động/bị khóa/mới trong kỳ, Merchant theo trạng thái và mới trong kỳ, đơn tạo (ngày tạo)/hoàn thành (lịch sử trạng thái)/hủy (thời điểm hủy) trong kỳ so với kỳ trước cùng độ dài (kỳ trước 0 → `changePercent` null + `isNew`; `all` không so sánh), hiện trạng chờ phân công/quá 60 phút/đang xử lý/theo trạng thái, doanh thu theo thời điểm giao dịch (cùng công thức `/finance/revenue-report`), Locker/ô trống/sắp đầy, top Merchant (số đơn hoàn thành, bị hủy sau phân công, mẫu số, tỷ lệ hoàn thành, điểm đánh giá công khai + số lượt, tiền giặt), `definitions` cho từng chỉ số. `GET /admin/alerts?type=&severity=&page=&take=`: cảnh báo theo quy tắc (`source = Rule`): chờ phân công quá hạn, Merchant trễ lấy/trả, quá thời gian giặt, khách quá hạn lấy đồ/thanh toán, quá hạn gửi đồ, Locker còn ≤ 10% ô; `summary` theo loại trên toàn hệ thống, sắp mức độ rồi hạn chót, phân trang tại DB. `GET /admin/lookups/merchants`, `/admin/lookups/lockers` (id/tên/mã/trạng thái, keyword, take ≤ 50). Test `admin-dashboard-alerts.ps1` PASS.

### Đợt 03/11/2026 — hoàn thiện UC 1.2.4 Thanh toán

- [x] **MoMo (bước 3–7):** `MomoGateway` gọi API v2 `test-payment.momo.vn/v2/gateway/api/create` (ký HMAC-SHA256 theo thứ tự trường của MoMo, `requestType` mặc định `payWithMethod`), lưu `payUrl` vào giao dịch. `paymentMethod = 3` dùng được cho tiền giặt, phí lưu kho, phí hủy (mã giao dịch MoMo ≤ 50 ký tự). `POST /payment/momo-ipn` (ẩn danh): sai chữ ký/partner → 400; số tiền sai → ghi log, không cập nhật đơn (ngoại lệ 7a); kết quả lỗi → giao dịch Failed + "Thanh toán thất bại. Vui lòng thử lại."; thành công → đơn Paid, lưu `transId`; gọi lại → 204 không đổi gì. Phần ghi nhận kết quả dùng chung với VNPay IPN (`ApplyGatewayResultAsync`). Cấu hình `Payment:Momo:{PartnerCode, AccessKey, SecretKey, Endpoint, RequestType, RedirectUrl, IpnUrl}` chỉ để trong `docker-compose.override.yml` local; MoMo gọi IPN theo `IpnUrl` gửi kèm từng giao dịch (không phải khai trên portal).
- [x] **Bước 9:** khách thanh toán xong, Merchant nhận "Khách đã thanh toán đơn #… Vui lòng phân công Shipper trả đồ về locker."; câu 7a đổi thành "Thanh toán thất bại. Vui lòng thử lại."
- [x] **Luồng 7b:** Merchant chuyển đơn sang Chờ thanh toán → `payment_deadline` = +7 ngày (migration `BackfillPaymentDeadlines` đặt hạn cho đơn cũ đang chờ). Worker nhắc khách 24 giờ trước hạn (một lần); quá hạn chưa thanh toán → đơn `MovedToStorage`, lưu tại "Kho tiệm <tên Merchant>", mã lưu kho = mã đơn Merchant, không tính phí locker, ghi lịch sử, báo khách + Merchant. Khách vẫn thanh toán tiền giặt được (đơn giữ trạng thái lưu kho, thông báo nhận tại kho), sau đó trả phí lưu kho ("Vui lòng thanh toán tiền giặt trước khi thanh toán phí lưu kho.") và nhận đồ tại kho bằng `POST /wash-order/{id}/pickup-storage` như luồng chuyển kho cũ. Đơn đã thanh toán không bị chuyển.
- [x] Test mới `payment-overdue-storage.ps1` (Mock) PASS; `momo-sandbox.ps1` PASS ở chế độ Mock: IPN ký đúng thuật toán MoMo, từ chối chữ ký sai (400), bỏ qua số tiền sai, ghi nhận một lần và lưu `transId`. **Chưa thanh toán MoMo thật được:** bộ khóa test công khai (`MOMO`) bị MoMo sandbox từ chối mọi yêu cầu (`resultCode=99`, kể cả khi cố tình ký sai) — cần đăng ký khóa test riêng tại business.momo.vn rồi đặt `MOMO_PARTNER_CODE/MOMO_ACCESS_KEY/MOMO_SECRET_KEY`, chạy lại test ở chế độ `Live`. `vnpay-sandbox.ps1` chạy lại PASS sau khi gom code IPN. `notifications.ps1` kiểm tra thêm Merchant nhận thông báo thanh toán.

### Đợt 03/11/2026 — nhóm chốt mô hình mới cho UC 3.3

Quyết định của nhóm: đơn luôn có Merchant; một locker có thể có nhiều Merchant phụ trách; Admin chọn Merchant cho từng đơn sau khi khách gửi đồ; **giá là bảng giá chung**, Merchant đề xuất giá thì Admin duyệt.

- [x] **Bảng dịch vụ chung:** `services.merchant_id` thành không bắt buộc, chỉ ghi Merchant đã đề xuất (migration `ShareWashServiceCatalog`, khóa ngoại SET NULL). Tính giá đơn không còn kiểm tra dịch vụ thuộc Merchant của tủ ("Dịch vụ đã chọn không còn được cung cấp."). `GET /customer/services` trả bảng chung (bỏ tham số `merchantId`, bỏ `merchantName`). `/merchant-service/filter` trả bảng chung cho mọi Merchant; đề xuất sửa được gửi cho bất kỳ dịch vụ chung nào; tên không trùng toàn bảng kể cả đề xuất chờ của Merchant khác; mỗi dịch vụ chỉ một đề xuất chờ. Admin tạo dịch vụ không cần chọn Merchant. Test `service-proposals.ps1` cập nhật PASS; hồi quy `qr-pin-flow`, `finance` PASS.

- [x] **Locker nhiều Merchant:** bảng mới `locker_merchants` (locker, Merchant, độ ưu tiên; migration `AddLockerMerchants` chuyển `lockers.merchant_id` cũ thành Merchant ưu tiên 1 rồi xóa cột). Tạo/sửa locker nhận `merchantIds` theo thứ tự ưu tiên (`merchantId` đơn lẻ vẫn dùng được), chặn trùng/Merchant không hoạt động, "Vui lòng chọn ít nhất một Merchant phụ trách locker."; chi tiết/danh sách trả `merchants` [{merchantId, businessName, priority}]. Merchant thấy mọi locker mình phụ trách. Khách chỉ thấy locker còn ít nhất một Merchant hoạt động.
- [x] **UC 3.3 Phân công đơn cho Merchant:** đơn tạo ra **chưa có Merchant** (giá lấy từ bảng chung); tủ trả khác tủ gửi phải có chung Merchant phụ trách. Khách gửi đồ → Admin nhận "Đơn mới chờ phân công". `POST /order-assignment/filter` (đơn Deposited chưa có Merchant; lọc quận/thành phố/vùng phục vụ/thời gian gửi/chỉ quá hạn; highlight chờ quá 60 phút; "Không có đơn hàng nào chờ phân công."), `GET /order-assignment/{id}/candidates` (Merchant hoạt động phụ trách tủ gửi và tủ trả; độ ưu tiên, đánh giá, tải lượng X/Y, khoảng cách, điểm; "Không có Merchant nào phục vụ khu vực này."), `POST /order-assignment/{id}/assign` (khóa đơn, kiểm tra còn chờ, Merchant thuộc danh sách, còn sức chứa; ghi lịch sử; báo Merchant; "Phân công thành công!"), `POST /order-assignment/auto-suggest` (điểm = 35 ưu tiên + 25 tải trống + 25 đánh giá + 15 khoảng cách; bỏ Merchant đủ tải, tính cả đơn đã gợi ý trong lượt; "Không tìm thấy Merchant phù hợp cho đơn này."), `POST /order-assignment/auto-assign` (xác nhận theo lô, tất cả hoặc không; "Phân công tự động thành công!"). Merchant chỉ thấy đơn sau khi được gán; phân công Shipper trước khi có Merchant bị chặn ("Đơn chưa được Admin phân công cho Merchant."). Worker nhắc Admin khi đơn chờ phân công quá 60 phút.
- [x] Test mới `order-assignment.ps1` PASS. Helper `Invoke-TestDelivery … deposit` tự gán Merchant ưu tiên 1 khi test có token `$admin` (đặt `$NoAutoAssign` để tắt), nên các test luồng đơn cũ vẫn dùng được.
- [x] Đặc tả `Docs/Đặc tả usecase.docx` đã sửa theo code (sao lưu `Đặc tả usecase.backup-20261103-uc33.docx`): 1.2.1 (bảng dịch vụ chung, đơn chưa có Merchant đến khi Admin phân công), 2.5.1, 2.6.1–2.6.3, 3.3.1–3.3.3 (Merchant phụ trách locker, độ ưu tiên, chờ quá 60 phút), 3.4.2–3.4.3 (Merchant phụ trách theo thứ tự ưu tiên, 4c), 3.5.1–3.5.2, 3.5.5.

### Đợt 02/11/2026 — hoàn thiện các UC "một phần"

- [x] **Cấu trúc contract:** `MerchantProfileContracts` tách thành `Commands/Queries/Results/Services` như các module khác; `IMerchantProfileAppService` kế thừa `IApplicationService`.
- [x] **Hồ sơ Shipper (4.1.3–4.1.4):** policy mới `ShipperOnly`; `GET/PUT /shipper-profile`; kiểm tra họ tên/SĐT/biển số dùng chung `ShipperRules` với quản lý Shipper (2.4); SĐT trùng bị chặn; avatar phải là ảnh tự upload ("Không thể upload ảnh. Vui lòng thử lại."); email và Merchant phụ trách không đổi; "Cập nhật hồ sơ thành công!".
- [x] **Danh sách đơn (2.3.1–2.3.2, 1.2.6, 1.2.8):** `OrderListEnricher` bổ sung `serviceNames`, `boxSize`, `isNew`; DTO thêm `merchantOrderCode`, `orderQrString`; thông báo rỗng theo vai trò; lọc thời gian dùng sẵn `fromDate`/`toDate` của `BaseFilterQuery`. `MerchantOrderCode` (trước đây luôn null) được sinh khi tạo đơn `MC` + phần số mã đơn; migration `BackfillMerchantOrderCodes` điền cho đơn cũ.
- [x] **Nhiệm vụ Shipper (4.3.1–4.3.2):** lọc `taskType`, `pendingOnly` (dùng `DeliveryTaskRules.ActiveDeliveries`), `isNew` cho nhiệm vụ chưa làm; chi tiết có `taskLockerName`, `taskLockerAddress`, `taskBoxNumber`, `nextAction`. Lệnh phân công Shipper nhận `note` (≤ 500 ký tự) lưu vào cột mới `orders.shipper_note` (migration `AddOrderShipperNote`), đặt lại mỗi lần phân công, khách không thấy.
- [x] **Lịch sử giao nhận (4.3.6):** `POST /shipper-delivery/history` đọc timeline do chính Shipper ghi (lấy đồ = Collected; trả đồ = ReadyToReturn hoặc trả đồ đơn hủy), nên vẫn đúng khi đơn được phân công cho người khác; trả mã đơn, locker, ô, loại nhiệm vụ, thời gian, trạng thái đơn hiện tại.
- [x] **Hồ sơ Customer (1.1.4–1.1.5):** avatar, tổng đơn/tổng chi tiêu tính từ đơn (các cột lưu sẵn chưa từng được cập nhật), địa chỉ bắt buộc, câu thông báo theo đặc tả.
- [x] **Câu thông báo theo đặc tả:** đăng nhập sai "Email hoặc mật khẩu không đúng."; kiểm tra mật khẩu trước rồi mới báo khóa (không lộ trạng thái khóa khi sai mật khẩu); khóa "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin." (Shipper: "…liên hệ Merchant."); `portal` sai vai trò "Bạn không có quyền truy cập trang này."; tạo đơn 4a, 14a.
- [x] **VNPay sandbox (1.2.4):** `tests/smoke/vnpay-sandbox.ps1` (chỉ chạy khi BE bật `Payment__Provider=VnPay`, đọc `HashSecret` từ container) PASS: link dùng đúng Terminal và chữ ký, VNPay sandbox mở trang chọn phương thức, IPN từ chối chữ ký/số tiền sai, ghi nhận một lần, gọi lại trả `02`, chặn callback giả lập. Thanh toán thật bằng thẻ NCB qua trình duyệt: VNPay gọi IPN về qua tunnel, đơn `WG2611026743` chuyển Paid (mã GD VNPay `15723198`). Hai Terminal cũ bị VNPay báo `code=71` (terminal chưa duyệt) nên đăng ký Terminal mới. Cấu hình VNPay chỉ nằm trong `docker-compose.override.yml` local (không commit).
- [x] Smoke test mới PASS: `shipper-profile.ps1`, `order-list.ps1`, `shipper-tasks.ps1`, `spec-messages.ps1`; cập nhật `customer-profile.ps1`. Hồi quy `shipper-management`, `order-security`, `qr-pin-flow`, `shipper-assignment`, `order-cancellation` PASS; chạy lại toàn bộ 17 bộ còn lại (cân kg, tài chính, thông báo, nhắc nhở, khuyến mãi, phản hồi, cài đặt, locker, Merchant, dịch vụ, vùng phục vụ, hồ sơ, danh sách đơn, Shipper) PASS.

### Admin quản lý Merchant ngày 01/11/2026 — UC 3.2.1–3.2.5

- [x] **Danh sách (3.2.1):** `POST /merchant/filter` trả email, tổng đơn (trừ đơn hủy), doanh thu (đơn đã thanh toán), số đơn chưa xử lý, ảnh, vùng phục vụ — tính từ đơn hàng thực tế; lọc thêm `serviceAreaId`. Rỗng: "Chưa có Merchant nào."
- [x] **Thêm mới (3.2.2):** `POST /merchant/` không còn nhận `userId` có sẵn: hệ thống tạo tài khoản role Merchant (id tài khoản = id Merchant), mật khẩu tạm gửi email (gửi lỗi chỉ ghi log). Form: tên, MST, giấy phép kinh doanh, địa chỉ, quận, thành phố, tọa độ, SĐT, email, giờ mở cửa, ảnh, hoa hồng, bán kính, sức chứa, hợp đồng, SLA, vùng phục vụ. Lỗi: "Email này đã được sử dụng.", "Mã số thuế này đã được sử dụng.", "Tọa độ không hợp lệ.", "Số điện thoại không hợp lệ.", "Tỷ lệ chiết khấu phải từ 0-100%.". "Thêm Merchant thành công!"
- [x] **Chỉnh sửa (3.2.3):** cùng kiểm tra; đổi email đăng nhập; bỏ `commissionRate`/`imageUrls`/`serviceAreaIds` = giữ nguyên. "Cập nhật Merchant thành công!" Giờ mở cửa và giấy phép (cột jsonb) được chuẩn hóa thành JSON như UC 2.1.2.
- [x] **Khóa/mở khóa (3.2.4):** `PUT /merchant/status` khóa bắt buộc lý do; còn đơn chưa xử lý mà chưa `confirm` thì trả 409 "Merchant đang có X đơn chưa xử lý. Vẫn muốn khóa?". Khóa: tắt tài khoản, thu hồi refresh token, locker của Merchant ẩn với khách, không tạo được đơn mới, báo Merchant (loại 29): "Đã khóa Merchant thành công!" Mở khóa: "Đã mở khóa Merchant thành công!". `CustomResult` trả đúng HTTP 409.
- [x] **Vùng phục vụ (3.2.5):** `/service-area/filter` có số Merchant mỗi vùng; `GET /service-area/{id}/merchants` liệt kê Merchant đang hoạt động kèm cờ đã gán và độ ưu tiên; `PUT /service-area/{id}/merchants` gán nhiều Merchant với độ ưu tiên 1–100, chặn trùng/Merchant không hoạt động, bỏ chọn tất cả cần `confirmEmpty` (409 "Vùng này chưa có Merchant nào phục vụ. Bạn có chắc muốn lưu?"). "Thiết lập vùng phục vụ thành công!"
- [x] `tests/smoke/merchant-admin.ps1`, `tests/smoke/service-area-merchants.ps1` PASS; cập nhật `system-settings.ps1` theo form tạo Merchant mới; hồi quy `shipper-management.ps1`, `merchant-profile.ps1` PASS.

### Admin quản lý Locker ngày 01/11/2026 — UC 3.4.1–3.4.5

- [x] **Danh sách (3.4.1):** `POST /locker/filter` trả thêm tên Merchant phụ trách, tên khu vực, mô tả, số ô trống (đếm lại từ bảng ô, trước đây luôn 0 trong danh sách); lọc thêm `serviceAreaId`, `merchantId`. Admin không có kết quả: "Chưa có locker nào." Chi tiết trả ảnh và ô sắp theo số.
- [x] **Thêm mới (3.4.2):** tên 1–200 ký tự, không trùng locker chưa xóa (không phân biệt hoa thường): "Tên locker này đã được sử dụng."; tọa độ trong khoảng hợp lệ và khác (0,0): "Tọa độ không hợp lệ."; Merchant phụ trách đang hoạt động; khu vực tồn tại; loại locker hợp lệ; ảnh phải do Admin upload. Số ô: truyền danh sách ô hoặc `boxCount` + `defaultBoxSize` (tạo ô 1..N), 1–200 ô, không trùng số. Mã để trống thì tự sinh `LK_…`, mã trùng (kể cả locker đã xóa) báo lỗi thay vì lỗi 500. "Thêm Locker thành công!"
- [x] **Chỉnh sửa (3.4.3):** cùng kiểm tra; sửa được khu vực và ảnh (bỏ `imageUrls` = giữ ảnh, `[]` = xóa); Merchant phụ trách giữ nguyên. "Cập nhật Locker thành công!"
- [x] **Xóa (3.4.4):** `DELETE /locker/{id}` (AdminOnly) xóa mềm locker và các ô; chặn khi còn ô đang có đồ/giữ chỗ hoặc đơn chưa hoàn tất dùng locker để gửi/trả (kể cả đồ của đơn hủy còn trong tủ): "Không thể xóa locker đang có đơn hàng chưa xử lý." "Xóa Locker thành công!" Locker đã xóa không còn hiện với Admin, Merchant, khách; tên được dùng lại.
- [x] **Ô tủ (3.4.5):** `PUT /locker/box-status` chỉ cho Trống ↔ Bảo trì; ô đang có đồ/giữ chỗ không chuyển được. "Cập nhật ô tủ thành công!" `GET /locker/{id}/usage-history?boxId=` xem lịch sử từng ô.
- [x] `tests/smoke/locker-admin.ps1` PASS; hồi quy `locker-scope.ps1`, `order-cancellation.ps1` PASS.

### Merchant cập nhật hồ sơ ngày 01/11/2026 — UC 2.1.2

- [x] **Xem hồ sơ:** `GET /merchant-profile/` (MerchantOnly): tên cửa hàng, MST, email đăng nhập, địa chỉ, tọa độ, SĐT, giờ mở cửa, ảnh cửa hàng, trạng thái, đánh giá, tổng đơn, hoa hồng, bán kính, sức chứa, hợp đồng và yêu cầu đang chờ duyệt (nếu có).
- [x] **Gửi yêu cầu:** `POST /merchant-profile/update-requests` với tên cửa hàng, địa chỉ, SĐT, giờ mở cửa, hình ảnh (bỏ `imageUrls` = giữ ảnh hiện tại, `[]` = xóa ảnh). Kiểm tra "Số điện thoại không hợp lệ." (9–11 chữ số, lưu dạng chỉ số), tên/địa chỉ không trống, ảnh mới phải do chính Merchant upload ("Không thể upload ảnh. Vui lòng thử lại."), không có thay đổi thì từ chối, mỗi lúc một yêu cầu chờ duyệt. Lưu bảng `merchant_profile_requests` (migration `AddMerchantProfileRequests`) với dữ liệu đề xuất + dữ liệu hiện tại; hồ sơ chưa đổi. Báo Admin (loại 47): "Yêu cầu cập nhật đã được gửi. Vui lòng chờ Admin phê duyệt."
- [x] **Giờ mở cửa:** cột `opening_hours` là jsonb — JSON hợp lệ được giữ nguyên (thu gọn), chữ thường như `07:00-22:00` được lưu thành chuỗi JSON.
- [x] **Admin duyệt:** `POST /merchant-profile-request/filter` (Admin lọc trạng thái/Merchant, chờ duyệt lên trước; Merchant chỉ thấy của mình), `GET /merchant-profile-request/{id}` (kèm `changedFields`), `POST …/{id}/approve` áp dụng vào hồ sơ và thay ảnh cửa hàng (`merchant_images` loại `store`), `POST …/{id}/reject` bắt buộc lý do. Merchant nhận thông báo (loại 27) kèm lý do khi bị từ chối.
- [x] `tests/smoke/merchant-profile.ps1` PASS: xem hồ sơ, 403 cho Admin, SĐT sai, tên/địa chỉ trống, ảnh của người khác/đường dẫn lạ, không thay đổi, gửi yêu cầu + hồ sơ chưa đổi + thông báo Admin, chặn yêu cầu thứ hai, phạm vi Merchant, từ chối cần lý do, duyệt áp dụng đủ trường và ảnh, bỏ `imageUrls` giữ ảnh, `[]` xóa ảnh.

### Đề xuất và duyệt dịch vụ ngày 01/11/2026 — UC 2.6.1–2.6.3, 3.5.1–3.5.6

- [x] **Merchant (2.6.1):** `POST /merchant-service/filter` chỉ dịch vụ đã duyệt, chưa xóa của tiệm; rỗng: "Chưa có dịch vụ nào được cung cấp." Merchant bị chặn 403 ở `/wash-service`.
- [x] **Đề xuất thêm mới (2.6.2):** `POST /merchant-service/proposals` (tên, mô tả, giá, giá/kg, giá/chiếc, thời gian xử lý, danh mục, đơn vị, ảnh đã upload của tiệm, lý do). Kiểm tra "Giá dịch vụ không hợp lệ.", tên 1–200 ký tự, thời gian 1–8760 giờ; tên trùng (không phân biệt hoa thường) với dịch vụ hoặc đề xuất đang chờ: "Tên dịch vụ này đã tồn tại." Lưu `ServiceChangeRequest` trạng thái chờ, báo Admin (loại 46): "Đề xuất đã được gửi. Vui lòng chờ Admin phê duyệt."
- [x] **Đề xuất chỉnh sửa (2.6.3):** `POST /merchant-service/{serviceId}/proposals` chỉ cho dịch vụ của tiệm, giữ danh mục/đơn vị, lưu ảnh chụp dữ liệu hiện tại và đề xuất; mỗi dịch vụ chỉ một đề xuất chờ.
- [x] **Admin duyệt (3.5.5–3.5.6):** `POST /service-proposal/filter` (Admin lọc trạng thái/loại/Merchant, Merchant chỉ thấy của mình, chờ duyệt lên trước), `GET /service-proposal/{id}`; `POST /service-proposal/{id}/approve` kiểm tra lại rồi tạo dịch vụ đã duyệt (mã `SVC_…`) hoặc cập nhật dịch vụ: "Đã phê duyệt đề xuất!"; `POST /service-proposal/{id}/reject` bắt buộc lý do: "Đã từ chối đề xuất!". Merchant nhận thông báo kèm lý do (loại 26).
- [x] **Admin quản lý (3.5.1–3.5.4):** `/wash-service` chỉ Admin; lọc theo Merchant, trạng thái duyệt; tạo/sửa kiểm tra Merchant tồn tại, mã và tên trùng; "Thêm Dịch vụ thành công!", "Cập nhật Dịch vụ thành công!"; `DELETE /wash-service/{id}` xóa mềm, chặn "Không thể xóa dịch vụ đang có đơn hàng." khi còn đơn chưa hoàn tất/hủy; dịch vụ đã xóa không còn hiển thị.
- [x] `tests/smoke/service-proposals.ps1` PASS: danh sách rỗng, quyền 403, giá/tên/thời gian sai, tên trùng với đề xuất đang chờ, thông báo Admin, phạm vi Merchant, duyệt tạo dịch vụ + thông báo, duyệt lại bị chặn, đề xuất sửa + một đề xuất chờ, từ chối cần lý do, duyệt sửa cập nhật dịch vụ, Admin lọc/tên trùng/Merchant không tồn tại, xóa bị chặn khi có đơn rồi xóa được sau khi hủy đơn.

### Quản lý tài chính ngày 01/11/2026 — UC 3.8.1–3.8.3

- [x] **Quy tắc số liệu:** doanh thu ghi nhận theo giao dịch thanh toán thành công (kể cả giao dịch sau đó được hoàn) tại thời điểm hoàn tất; tiền hoàn ghi nhận theo `RefundedAt` và trừ ra trong kỳ. Tách tiền giặt, phí quá hạn/lưu kho, phí hủy. Hoa hồng chỉ tính trên tiền giặt đã thu trừ phần hoàn (không âm), làm tròn đến đồng; thực trả Merchant = doanh thu ròng − hoa hồng. Phí quá hạn/lưu kho/hủy thuộc nền tảng.
- [x] **Báo cáo doanh thu (Admin):** `POST /finance/revenue-report` lọc `fromDate/toDate` (Unix giây, mặc định 30 ngày, tối đa 366 ngày) và `merchantId`; trả tổng, theo Merchant, theo dịch vụ (giá dịch vụ trên đơn đã thanh toán, trước phụ phí/giảm giá), theo ngày giờ Việt Nam. Không có dữ liệu: "Không có dữ liệu trong khoảng thời gian này." `POST /finance/revenue-report/export` xuất CSV UTF-8 BOM.
- [x] **Phí hoa hồng:** `POST /finance/commissions` theo kỳ: tổng doanh thu, đã hoàn, doanh thu ròng, tỷ lệ hoa hồng của Merchant, phí hoa hồng, số tiền thực trả.
- [x] **Kỳ đối soát** (bảng `merchant_settlements`, migration `AddMerchantSettlements`): `POST /finance/settlements` chốt số liệu cho kỳ đã kết thúc (không cho kỳ tương lai hoặc trùng kỳ đã tạo), lưu snapshot tỷ lệ/hoa hồng; `POST /finance/settlements/filter` (Admin lọc Merchant/trạng thái, Merchant chỉ thấy của mình); `POST /finance/settlements/{id}/confirm-paid` ghi mã chuyển khoản. Merchant nhận thông báo khi tạo kỳ và khi được chi trả.
- [x] **Danh sách hoàn tiền (3.8.3):** `GET /payment/refund-requests?status=` có mã đơn, tên khách, mới nhất trước, "Chưa có yêu cầu hoàn tiền nào."; `GET /payment/refund-requests/{id}` (Admin mọi yêu cầu, Customer của mình).
- [x] `tests/smoke/finance.ps1` PASS: Merchant riêng 25% với 2 đơn đã thanh toán (1 đơn hoàn toàn bộ) và 1 đơn hủy thu phí 30%: tiền giặt 200.000đ, phí hủy 30.000đ, hoàn 100.000đ, tổng 130.000đ; theo Merchant/dịch vụ/ngày; kỳ rỗng; CSV; hoa hồng 25.000đ, thực trả 75.000đ; kỳ đối soát chặn kỳ tương lai và trùng kỳ, giữ snapshot khi đổi tỷ lệ, phạm vi Merchant, chi trả + thông báo; danh sách/chi tiết hoàn tiền; quyền 403/404.
- [ ] Chưa có: biểu đồ (dữ liệu theo ngày đã có cho FE vẽ), đối soát tự động định kỳ, chuyển tiền thật.

### Đồng bộ đặc tả cập nhật ngày 01/11/2026 (bản Word mới)

- [x] UC 1.2.7: phí hủy Pending/Deposited tính trên `TotalAmount` (giá tạm tính **trước giảm**), Collected trên `FinalAmount` — đã trả lại đúng như đặc tả (bản trước tính trên giá sau giảm).
- [x] UC 1.2.7 bước 11: hủy đơn Deposited đang có nhiệm vụ lấy đồ thì báo cho Shipper nhiệm vụ đã hủy; khách nhận lại đồ của đơn hủy thì báo khách và Merchant.
- [x] UC 1.3.2 6b: quá hạn gửi đồ trả đúng thông báo "Đã quá hạn gửi đồ. Vui lòng hủy đơn để giải phóng ô và tạo đơn mới." (không tự hủy).
- [x] UC 4.3.4 bước 12: Shipper trả đồ vào locker thì báo cho Merchant.
- [x] Các quy tắc còn lại trong bản cập nhật (QR/PIN, phiên nhận 10 phút, SLA chỉ cảnh báo, phân công từng chuyến, đơn hủy) khớp với code hiện có; không cần sửa thêm.

### Quản lý Shipper ngày 01/11/2026 — UC 2.4.1–2.4.3

- [x] **API `/shipper` (MerchantOrAdmin):** `POST /filter` (từ khóa tên/SĐT/email/biển số, trạng thái Đang làm việc/Tạm nghỉ, trạng thái giao nhận; Admin lọc theo Merchant), `GET /{id}`, `POST /`, `PUT /{id}`. Merchant chỉ thấy/sửa Shipper của tiệm mình (Shipper tiệm khác trả 404); Admin tạo cho tiệm nào thì phải chọn `merchantId`. Danh sách trống: "Bạn chưa có Shipper nào."
- [x] **Thêm Shipper:** tạo `User` role Shipper + hồ sơ `Shipper` trong một transaction; mật khẩu tạm 12 ký tự đúng chính sách (hoa/thường/số), lưu BCrypt, **không trả về trong API**, gửi qua email (`IAuthEmailSender.SendAccountCredentialsAsync`; local là Mailpit). Gửi email lỗi chỉ ghi log, tài khoản vẫn được tạo (Shipper dùng Quên mật khẩu). Nhận số CMND/CCCD (9 hoặc 12 số) và ảnh CMND qua `POST /files/images`.
- [x] **Validation theo đặc tả:** "Vui lòng nhập họ tên Shipper.", "Số điện thoại không hợp lệ.", "Số điện thoại này đã được sử dụng.", "Email không hợp lệ.", "Email này đã được sử dụng." (so với mọi tài khoản, bỏ qua chính Shipper khi sửa), biển số bắt buộc trừ xe đạp. Sửa email thì Shipper đăng nhập bằng email mới.
- [x] **Tạm nghỉ:** Shipper tạm nghỉ không được phân công (logic phân công sẵn có); không cho chuyển Tạm nghỉ khi đang có chuyến lấy/trả chưa xong. Quy tắc "chuyến đang giao" chuyển về `DeliveryTaskRules` (Domain) để phân công và quản lý Shipper dùng chung.
- [x] **Số đơn đã giao:** `shippers.total_deliveries` tăng khi Shipper hoàn tất một chuyến lấy đồ hoặc trả đồ (gọi lặp không tăng thêm). Danh sách còn hiển thị số chuyến đang giao và đánh giá.
- [x] `tests/smoke/shipper-management.ps1` PASS: quyền 401/403, phạm vi Merchant, validation tạo/sửa và trùng SĐT/email, mật khẩu không lộ trong response, đọc mật khẩu từ Mailpit và đăng nhập được với role Shipper, Admin tạo cho tiệm khác và Merchant không xem/sửa được, đổi email rồi đăng nhập bằng email mới, chặn Tạm nghỉ khi có chuyến, tăng số đơn đã giao sau khi lấy đồ, Shipper Tạm nghỉ không được phân công.
- [ ] Chưa có: xóa Shipper, đánh giá Shipper từ khách, ứng dụng Shipper riêng (hiện Shipper dùng API).

### Nhắc nhở theo thời gian ngày 01/11/2026

- [x] **Worker `ReminderWorker`** (BackgroundService trong `WashGo.HttpApi.Host`, cùng tiến trình với SignalR hub nên thông báo vẫn real-time): quét mỗi `Reminders:IntervalSeconds` (mặc định 60 giây), tắt bằng `Reminders:Enabled=false`. Admin có thể quét ngay bằng `POST /notification/reminders/run`. Lượt quét lỗi được ghi log và thử lại ở lượt sau.
- [x] **Chống gửi trùng:** bảng `reminder_logs` (migration `20261101091541_AddReminderLogs`, khóa duy nhất, ví dụ `deposit:{orderId}`, `overdue-fee:{orderId}:{ngày}`, `overdue-summary:{yyyy-MM-dd}`). Mỗi lời nhắc chỉ gửi một lần, kể cả khi người dùng đã xóa thông báo; đơn đổi trạng thái thì không còn bị nhắc.
- [x] **Các lời nhắc (mốc lấy từ cấu hình SLA UC 3.9):**
  - Khách: còn 15 phút trước hạn gửi đồ (đơn đã quá hạn không nhắc); còn nửa thời gian lấy đồ miễn phí; quá hạn ngày 1 kèm phí; phí phạt ngày 2, ngày 3.
  - Shipper: nhiệm vụ lấy đồ quá 30 phút từ lúc phân công (phân công lại thì nhắc lại); nhắc trả đồ khi đơn đã thanh toán quá nửa SLA trả.
  - Merchant: nhắc trả đồ ở nửa SLA trả; cảnh báo trễ SLA lấy đồ (từ lúc khách gửi) và trễ SLA trả đồ (từ lúc thanh toán); đơn cần chuyển kho.
  - Admin: cảnh báo trễ lấy/trả của Merchant, đơn cần chuyển kho (quá 72 giờ), tổng hợp số đơn quá hạn lấy đồ mỗi ngày (theo giờ Việt Nam).
- [x] `tests/smoke/reminders.ps1` PASS: chỉ Admin quét thủ công; từng lời nhắc tới đúng vai trò đúng một lần dù quét nhiều lần; đơn hết hạn gửi, đơn chưa đến mốc và đơn đã chuyển kho không bị nhắc.
- [ ] Chưa có: tự hủy đơn quá hạn gửi đồ (theo quyết định ngày 31/10 chỉ chặn gửi, không tự hủy), tự chuyển kho, AI phát hiện bất thường. `WashGo.Background.Host` vẫn là khung chưa dùng.

### Cài đặt hệ thống ngày 01/11/2026 — UC 3.9.1, 3.9.2, 3.9.4

- [x] **Migration `20261101090320_AddSystemSettings`:** bảng `system_settings` một dòng (Id cố định), migration tự chèn giá trị mặc định trùng quy tắc đang chạy: gửi đồ 30 phút, Merchant lấy 4h/trả 24h, lấy đồ miễn phí 24h, hoa hồng mặc định 20%, phí quá hạn 10.000/20.000/50.000đ, lưu kho 100.000đ/ngày (chuyển kho sau 72h, cố định), phí hủy 30%/30%/50%, bồi thường 30/70/100/200%.
- [x] **API `/system-setting` (AdminOnly):** `GET /`, `PUT /sla` ("Cấu hình SLA thành công!"), `PUT /commission` ("Tỷ lệ phí phải từ 0-100%."), `PUT /penalty` ("Cấu hình thành công!"); sai thông số trả "Thông số không hợp lệ.". Bồi thường phải tăng dần theo mức hư hỏng. Lưu người/thời điểm cập nhật.
- [x] **Áp dụng cấu hình (thay số viết cứng):** hạn gửi đồ khi deposit, % phí hủy (Pending quá hạn/Deposited/Collected), cảnh báo SLA Shipper lấy/trả, hạn lấy đồ miễn phí khi trả vào locker, phí quá hạn locker và phí kho (Payment + nhận đồ + chuyển kho), % bồi thường khi Admin xử lý khiếu nại, % hoa hồng mặc định khi tạo Merchant không nhập tỷ lệ (sửa Merchant không gửi tỷ lệ thì giữ nguyên; `commissionRate` trong lệnh Merchant nay là tùy chọn).
- [x] Đơn đã hủy hiển thị % phí theo số tiền đã chốt, không đổi khi Admin sửa cấu hình sau đó. Lưu ý: phí quá hạn/lưu kho của đơn đang chờ lấy tính theo cấu hình tại thời điểm tính, không snapshot theo đơn.
- [x] `tests/smoke/system-settings.ps1` PASS: quyền 401/403, validation từng nhóm (không làm đổi cấu hình), hạn gửi đồ 10 phút chặn deposit lúc 11 phút và cho phép lúc 5 phút, phí hủy 40% áp dụng và giữ nguyên sau khi trả cấu hình, hoa hồng mặc định 27,5% cho Merchant mới, sửa Merchant giữ/đổi/chặn tỷ lệ > 100%. Script khôi phục cấu hình gốc ở `finally`.

### Thông báo ngày 01/11/2026 — UC 5.1–5.3 và các thông báo real-time

- [x] **Migration `20261101082042_AddNotifications`:** bảng `notifications` (người nhận, loại, tiêu đề, nội dung, liên kết `order`/`incident`/`rating` + id, đã đọc, thời điểm đọc).
- [x] **API `/notification` (mọi vai trò đã đăng nhập, chỉ hộp thư của chính mình):** `GET /` mới nhất trước, lọc `unreadOnly` ("Bạn chưa có thông báo nào."); `GET /unread-count`; `POST /{id}/read` (đọc lại không đổi `readAt`); `POST /read-all` ("Đã đánh dấu tất cả là đã đọc."); `DELETE /{id}`; `DELETE /`. Thông báo của người khác trả 404.
- [x] **Real-time SignalR `/hubs/notifications`:** bắt buộc JWT (WebSocket đọc `access_token` từ query), gửi theo user id, sự kiện `notification`. Thông báo được ghi trong cùng transaction nghiệp vụ (nghiệp vụ lỗi/rollback thì không có thông báo) và chỉ đẩy sau khi commit; lỗi đẩy chỉ ghi log.
- [x] **Sự kiện đã gắn:** tạo đơn (Customer + Merchant), gửi đồ (Customer + Merchant + Admin), phân công (Shipper), Shipper lấy đồ (Shipper + Customer "đã được tiếp nhận"), giặt/sấy/ủi/gấp, chờ thanh toán kèm số tiền, thanh toán thất bại (sandbox + IPN)/thành công, trả đồ vào locker (Shipper + Customer "đồ đã sẵn sàng" kèm hạn), nhận đồ tại locker/kho (Customer + Admin), hủy đơn (Customer + Merchant), chuyển kho, yêu cầu hoàn tiền (thủ công và tự động khi hủy → Admin), khiếu nại mới (Admin), xử lý/từ chối khiếu nại (Customer + Merchant), đánh giá mới (Admin).
- [x] **Sửa kèm:** `readAt` trả về giờ cắt về micro giây để khớp giá trị đọc lại từ PostgreSQL. **Lưu ý kỹ thuật:** `ISoftDelete` của `BaseEntity` là interface riêng của dự án, không phải của ABP — ABP không tự lọc `IsDeleted` và `DeleteAsync` xóa cứng. Module Khuyến mãi đã đổi sang xóa mềm thật (đặt `IsDeleted`) và lọc `!IsDeleted` ở mọi truy vấn; thông báo xóa cứng.
- [x] `tests/smoke/notifications.ps1` PASS: thông báo tới qua SignalR real-time (dùng `tests/smoke/signalr-listen.cjs`, cần `npm install` trong `washgo-be/tests/smoke`), đủ thông báo cho Customer/Merchant/Shipper/Admin theo từng bước đơn, thanh toán lỗi rồi thành công, hủy đơn, nhập sai PIN không sinh thông báo, quyền sở hữu, đọc/đọc tất cả, xóa/xóa tất cả.
- [ ] Chưa có: nhắc nhở theo thời gian (cần worker định kỳ — `WashGo.Background.Host` hiện là khung rỗng, chưa có trong docker-compose), thông báo duyệt dịch vụ cho Merchant (luồng đề xuất dịch vụ chưa làm), AI phát hiện bất thường, push FCM/SMS/Zalo.

### Đánh giá, sự cố và khiếu nại ngày 01/11/2026 — UC 1.4.1–1.4.3, 3.7.1–3.7.3

- [x] **Upload ảnh `POST /files/images`** (đã đăng nhập, form field `file`): chỉ nhận JPG/PNG/WEBP xác định bằng chữ ký tệp, tối đa 5MB; lưu `uploads/{userId}/{guid}.{ext}` trên volume Docker `washgo_uploads`, phục vụ tĩnh tại `/uploads/...`. Đánh giá/khiếu nại chỉ nhận URL ảnh do chính người gửi upload và còn tồn tại (tối đa 5 ảnh) — không gắn được URL tùy ý hoặc ảnh người khác.
- [x] **Migration `20261101072505_AddRatingsAndIncidents`:** bảng `order_ratings` (unique `order_id`, sao tổng + 4 tiêu chí, nhận xét, ảnh `text[]`, ẩn/lý do) và `incidents` (loại, mức độ, mô tả, ảnh, trạng thái, kết quả, bên chịu trách nhiệm, mức/giá trị/tiền bồi thường, trạng thái chi trả).
- [x] **Đánh giá (Customer):** `POST /rating` chỉ cho đơn `Completed` của chính khách; "Vui lòng chọn số sao đánh giá."; tiêu chí 1–5; "Bạn đã đánh giá đơn hàng này rồi." (khóa đơn + unique, hai request đồng thời chỉ một 201); "Cảm ơn bạn đã đánh giá!". `GET /rating/order/{orderId}`.
- [x] **Điểm Merchant:** `merchants.rating` = trung bình sao các đánh giá đang hiển thị (làm tròn 1 chữ số), cập nhật khi đánh giá mới hoặc ẩn/hiện.
- [x] **Admin/Merchant xem đánh giá:** `POST /rating/filter` lọc số sao, Merchant, thời gian, trạng thái ẩn; Merchant chỉ thấy đánh giá hiển thị của tiệm mình và tên khách bị che `***`. `PATCH /rating/{id}/visibility` (Admin) ẩn bắt buộc lý do / hiện lại.
- [x] **Báo cáo sự cố (Customer):** `POST /incident` cho đơn `Completed`, trong 3 ngày kể từ mốc `Completed` trên timeline ("Đã quá thời hạn báo cáo sự cố."), bắt buộc ≥1 ảnh ("Vui lòng upload ít nhất 1 ảnh minh chứng."), một khiếu nại chờ xử lý mỗi đơn; trạng thái Chờ xử lý. `GET /incident/mine` ("Bạn chưa có khiếu nại nào."), `GET /incident/{id}` chỉ chủ khiếu nại hoặc Admin.
- [x] **Xử lý khiếu nại (Admin):** `POST /incident/filter` (trạng thái, mức độ, loại, Merchant, thời gian; chờ xử lý lên đầu). `POST /incident/{id}/resolve`: bên chịu trách nhiệm, mức bồi thường Nhẹ 30%/Trung bình 70%/Nặng 100%/Mất đồ 200% × giá trị đồ — tiền do server tính, payload không quyết định; tạo khoản bồi thường chờ chi trả. `POST /incident/{id}/reject` bắt buộc lý do. Khóa dòng khi xử lý, đã xử lý thì không xử lý lại. `POST /incident/{id}/confirm-compensation` ghi nhận chi trả thủ công kèm mã tham chiếu (gọi lặp không ghi đè).
- [x] **Sửa kèm:** tạo đơn trước đây không lưu `orders.merchant_name` (44/44 đơn trong DB local để trống) — nay lưu tên tiệm khi đặt; đánh giá/khiếu nại của đơn cũ lấy tên từ bảng Merchant.
- [x] `tests/smoke/feedback.ps1` PASS: upload (401, sai định dạng, >5MB, phục vụ ảnh), đánh giá (quyền, đơn chưa xong, thiếu/sai sao, ảnh của người khác, đường dẫn lạ, đánh giá lặp, race), điểm Merchant khớp trung bình hiển thị, Admin lọc/ẩn, Merchant chỉ thấy tiệm mình và tên khách bị che, khiếu nại (quá 3 ngày, thiếu ảnh, sai loại, đơn người khác, trùng khi đang chờ), danh sách của khách, xử lý 70% × 200.000 = 140.000đ, không xử lý lại, chi trả + replay, từ chối, mất đồ 200%.
- [ ] Chưa có: thông báo cho Admin/Customer/Merchant (module 5), ảnh theo từng giai đoạn gửi/lấy/trả để đối chiếu (UC 3.7.2 bước 2), cấu hình % bồi thường động (UC 3.9.4), Customer UI.

### Khuyến mãi ngày 01/11/2026 — UC 3.6.1–3.6.5 và UC 1.2.1 bước 8–15

- [x] **Entity & Migration `20261101065121_AddPromotions`:** bảng `promotions` (mã, mô tả, loại giảm 1=% / 2=cố định, mức giảm, đơn tối thiểu, giảm tối đa, bắt đầu/kết thúc UTC, tổng lượt, lượt/khách, đã dùng, kích hoạt) và `promotion_usages` (mã, đơn, khách, giá trước giảm, tiền giảm, thời điểm, đã trả lại). Thêm cột `orders.promotion_code` lưu snapshot mã. Unique `promotions.code` chỉ trên bản ghi chưa xóa mềm nên mã đã xóa được dùng lại; unique `promotion_usages.order_id`.
- [x] **Admin API `/promotion` (AdminOnly):** `POST /filter` (lọc trạng thái tính toán 0=Ngừng/1=Sắp diễn ra/2=Đang áp dụng/3=Hết hạn/4=Hết lượt, loại giảm, khoảng thời gian hiệu lực qua `fromDate/toDate` Unix giây, từ khóa), `GET /{id}`, `POST /`, `PUT /`, `DELETE /{id}`, `PATCH /{id}/toggle-active`, `GET /{id}/statistics`, `GET /{id}/statistics/export` (CSV UTF-8 BOM).
- [x] **Validation theo đặc tả:** mã 3–50 ký tự `A-Z0-9-_` (tự viết hoa, bỏ khoảng trắng); "Mã giảm giá này đã tồn tại."; "Thời gian kết thúc phải sau thời gian bắt đầu."; "Mức giảm không hợp lệ." (≤ 0, % > 100, đơn tối thiểu âm, giảm tối đa ≤ 0); lượt/khách không vượt tổng lượt. Sửa: không đổi mã khi đã có lượt dùng, tổng lượt không nhỏ hơn số đã dùng, không ghi đè `UsedCount`. Xóa: "Không thể xóa mã đã có lượt sử dụng." (xóa mềm).
- [x] **Khách hàng — `POST /wash-order/apply-voucher` (CustomerOnly):** nhận `lockerId`, `items`, `promotionCode?`; tính giá từ server bằng cùng hàm với tạo đơn; trả tổng tiền, danh sách mã đang hiệu lực kèm `isEligible`, `ineligibleReason`, `estimatedDiscountAmount`, lượt còn lại của khách; có mã thì trả giá sau giảm hoặc 400 với thông báo đặc tả ("Đơn hàng chưa đủ điều kiện áp dụng mã này.", hết hạn, chưa bắt đầu, hết lượt, hết lượt của khách, không tồn tại). Không có mã khả dụng: "Hiện tại không có mã giảm giá khả dụng cho đơn hàng này."
- [x] **Tạo đơn kèm `promotionCode`:** kiểm tra lại mã trên giá server; trong transaction giữ ô, tăng `used_count` bằng một `UPDATE` có điều kiện (hiệu lực, còn lượt) — khóa dòng nên đếm lại lượt/khách được tuần tự hóa; lỗi thì rollback, không giữ ô. Lưu `PromotionId`, `PromotionCode`, `DiscountAmount`, `FinalAmount`, timeline ghi mã và số tiền giảm.
- [x] **Quy tắc tiền đã chốt trong code:** giảm % làm tròn đến đồng, áp trần "giảm tối đa"; tiền giảm tính trên giá cơ bản lúc đặt đơn và giữ nguyên khi Merchant cân (giá cuối = giá gói + phụ phí cân − giảm giá). Sau giảm đơn còn tối thiểu 5.000đ (`Promotion.MinPayableAmount`) để vẫn thanh toán được qua cổng — cần nhóm xác nhận con số này.
- [x] **Hủy đơn:** trả lại lượt dùng (đánh dấu `is_reverted`, giảm `used_count` đúng một lần, hủy lặp không trả lần hai). Phí hủy theo đặc tả UC 1.2.7: Pending/Deposited tính trên `TotalAmount` (giá tạm tính trước giảm), Collected tính trên `FinalAmount` đã chốt (đã gồm giảm giá và phụ phí cân).
- [x] **Thống kê:** số lượt dùng, tổng tiền giảm, tổng giá trước giảm, doanh thu (giá cuối) và phần đã thanh toán của các đơn dùng mã chưa hủy; số liệu theo ngày giờ Việt Nam (UTC+7). Chưa dùng: "Mã này chưa được sử dụng lần nào."
- [x] **Sửa kèm (phát hiện khi chạy test):** DB local có dịch vụ thử `SVC_NEG` giá −50.000đ làm `order-security.ps1` lỗi 400. API tạo/sửa dịch vụ trước đây không kiểm tra giá; nay chặn giá ≤ 0, thời lượng sai và mã trùng. Catalog khách (`/customer/services`) ẩn dịch vụ giá ≤ 0. Không sửa dữ liệu DB.
- [x] `tests/smoke/promotions.ps1` PASS trên Docker: quyền 401/403, validation tạo, lọc trạng thái/thời gian, báo giá + danh sách khả dụng, trần %, mức tối thiểu 5.000đ, lưu giảm giá trên đơn, giới hạn tổng/lượt khách, đơn bị từ chối không giữ ô, sửa/xóa mã đã dùng, dùng lại mã đã xóa, hủy trả lượt (kể cả hủy lặp), phí hủy theo giá trước giảm, cân kg giữ giảm giá, hai đơn đồng thời tranh mã còn 1 lượt (đúng một 201), thống kê + CSV, tắt mã.
- [ ] Chưa có: mã chỉ cho khách mới/đơn đầu tiên, giới hạn theo Merchant/dịch vụ, gửi thông báo, ứng dụng Customer để hiển thị bước chọn mã.

### Hủy đơn và nhận lại đồ ngày 31/10/2026 — UC 1.2.7

- [x] `GET /wash-order/{id}/cancellation-quote`: Customer xem điều kiện, giá làm căn cứ, phần trăm phí, tiền hoàn và phí còn thiếu. Giá được tính lại dưới khóa order khi `POST /wash-order/{id}/cancel`; payload không quyết định phí.
- [x] Pending trong 30 phút: 0%; Pending quá 30 phút/Deposited: 30% TotalAmount; Collected có ActualWeightKg > 0: 50% FinalAmount. Làm tròn tiền 2 chữ số thập phân. Đã bắt đầu giặt hoặc các bước sau không cho hủy. Chưa có thao tác cân riêng cho đơn chỉ theo chiếc/đôi, nên Collected chưa có cân thực tế vẫn bị từ chối hủy.
- [x] Pending hủy giải phóng Reserved; Deposited hủy vẫn giữ Occupied tại tủ gửi để khách nhận lại. Hủy khi đã về tiệm lưu vị trí AtMerchant, cho Merchant/Admin phân công riêng Return để Shipper trả về locker; không yêu cầu thanh toán tiền giặt cho chuyến trả đơn hủy. Không có phí giao lại bổ sung trong luồng này.
- [x] Quyết định người dùng: phí hủy thu qua sandbox/VNPay trước khi KHÁCH nhận lại đồ; Shipper có thể đưa đồ về locker trước khi khách trả phí. `POST /payment/cancellation-fee/create-url` dùng Purpose=3, chỉ số phí còn thiếu, chống tạo giao dịch Pending trùng. Callback/IPN xác nhận phí riêng, không đổi PaymentStatus tiền giặt hoặc đơn sang Paid.
- [x] Nhận lại đồ dùng open-pickup-box + confirm-pickup hiện có: đúng chủ đơn, QR/PIN nhận riêng, phí hủy đã trả đủ, token phiên 10 phút. Ô chỉ được giải phóng lúc xác nhận lấy hết đồ. Đơn luôn Cancelled; CancelledGoodsStatus chuyển 1=AtDepositLocker/2=AtMerchant/3=AtReturnLocker/4=ReturnedToCustomer; 0=None cho hủy khi chưa gửi. Không tạo lịch sử Completed cho đơn hủy.
- [x] Có thanh toán gốc ở dữ liệu cũ: đối chiếu giao dịch Order Succeeded (hoặc tiền mặt đã ghi nhận), giữ lại phí hủy và tự tạo RefundRequest Pending cho phần còn lại; không hoàn lại phí hủy. Hủy lặp không tạo request trùng. Request hoàn tiền thủ công cho đơn hủy bị chặn; Admin duyệt/ghi nhận hoàn tiền theo quy trình hiện có, không tự chuyển tiền ngân hàng.
- [x] Migration `20261030194924_AddOrderCancellationFlow`: thêm phí đã trả, số tiền yêu cầu hoàn, trạng thái trước hủy, vị trí/trạng thái trả đồ hủy và thời điểm nhận lại. Dữ liệu cũ không bị tự tính lại phí hoặc gán vị trí giả.
- [x] Build thành công, Docker backend đã rebuild; EF không có pending model changes và migration đã được startup áp dụng. Swagger có cancellation-quote và cancellation-fee/create-url, có summary.
- [x] `tests/smoke/order-cancellation.ps1` PASS: 0%/30%/50%, giá tạm tính/giá thực tế, quyền, payload sai/giả phí, thanh toán phí thất bại/thử lại/replay, giữ Occupied, chống Admin giải phóng ô đang chứa đồ, Shipper trả đồ khi tiền giặt Unpaid, nhận lại qua phiên QR/PIN, đơn luôn Cancelled, hoàn 70.000đ từ thanh toán gốc 100.000đ sau phí 30.000đ và không tạo refund trùng, race cancel/deposit.
- [x] Hồi quy `order-security.ps1`, `qr-pin-flow.ps1`, `actual-weight.ps1` PASS trên bản Docker cuối: phạm vi/quyền/actor, thanh toán sandbox, phí kho và nhận kho, nhận locker hai bước, cân/chốt giá 3/5/10 kg, replay đồng thời và toàn vòng đơn giặt bình thường.
- [x] Word UC Hủy đơn đã đồng bộ phí/thanh toán/giữ đồ/hoàn tiền; bổ sung nhánh đơn hủy trong phân công Shipper, trả locker và nhận đồ. Có backup `../Đặc tả usecase.backup-cancellation-20261031-025948.docx`. Kiểm tra nội dung và giữ số mục 1.3.1–1.3.4; chưa xác minh bố cục do công cụ xuất xem trước không hoạt động như đã ghi ở đợt trước.
- [ ] Module thông báo chưa triển khai. Chưa có quy tắc phí lưu kho/quá hạn riêng cho đồ của đơn hủy: hiện không tự tính thêm phí kho hoặc chuyển kho cho đơn Cancelled; đồ vẫn giữ ô đến khi nhận hoặc có xử lý hỗ trợ. Chưa kiểm thử VNPay thật hoặc tải lớn.
### Kết quả triển khai luồng giao nhận mô phỏng QR/PIN 31/10/2026 — UC 1.3 & 4.3

- [x] **Entity & Migration:** Migration `20261030184032_AddQrPinDeliveryActions` bổ sung: `DepositedAt`, `PaidAt`, `ShipperTaskPinCode`, `ShipperTaskQrString`, `TaskPinAttemptCount`, `TaskPinLockoutUntil`, `CustomerDepositPinAttemptCount`, `CustomerDepositLockoutUntil`, `CustomerPickupPinAttemptCount`, `CustomerPickupLockoutUntil`, `PickupSessionTokenHash`, `PickupSessionExpiresAt`, `LockerOverdueFeeAtStorageTransfer`.
- [x] **Chống dò mã Brute-Force:** Khóa bản ghi order trong UoW riêng của endpoint; khi sai mã chỉ lưu bộ đếm và commit trước khi trả lỗi, chưa thay đổi ô/trạng thái. Không mở transaction lồng cùng bản ghi. Sai 5 lần khóa 5 phút; lần xác thực sau hết khóa reset lượt sai, mã đúng hoặc phân công lại cũng reset.
- [x] **Mã nhiệm vụ Shipper:** Khi phân công (`AssignShipperAsync`), server tự sinh mã PIN 6 chữ số ngẫu nhiên an toàn (`RandomNumberGenerator`) và QR nhiệm vụ riêng biệt (`WASHGO:COLLECT:...` hoặc `WASHGO:RETURN:...`). Chỉ đúng Shipper được gán mới thấy mã này; che hoàn toàn với Customer và Merchant.
- [x] **Chặn đường tắt:** `UpdateStatusAsync` chặn hoàn toàn việc đổi trạng thái sang `Deposited`, `Collected`, `ReadyToReturn`, `Returned`, `Completed`, `MovedToStorage`, `Paid`, `Cancelled`. Chỉ còn các bước giặt nội bộ theo quyền Merchant/Admin (`Washing`, `Drying`, `Ironing`, `Folding`, `AwaitingPayment`).
- [x] **Customer gửi đồ (`POST /wash-order/{id}/deposit`):** Xác thực `DepositPinCode`/QR, kiểm tra thời hạn giữ ô 30 phút (`CreatedOn + 1800s`), kích hoạt ô `Occupied`, chuyển `Deposited`, lưu `DepositedAt`.
- [x] **Shipper lấy đồ (`POST /wash-order/{id}/shipper-collect`):** Xác thực đúng Shipper và mã nhiệm vụ, giải phóng ô gửi `Available`, cảnh báo trễ SLA lấy đồ nếu > 4 giờ, chuyển `Collected`.
- [x] **Shipper trả đồ (`POST /wash-order/{id}/shipper-return`):** Bắt buộc đơn `Paid` và đã thanh toán đủ, xác thực mã nhiệm vụ, tự động tìm và chiếm ô trống phù hợp kích thước (`Size >= originalSize`), cảnh báo trễ SLA trả đồ nếu > 24 giờ, chuyển `ReadyToReturn`, đặt hạn nhận 24h.
- [x] **Customer nhận đồ tại Locker (2 bước mô phỏng):**
  - **Bước 1 (`POST /wash-order/{id}/open-pickup-box`):** Xác thực mã `PickupPinCode`/QR, kiểm tra phí quá hạn phát sinh đã thanh toán đủ, sinh token phiên ngẫu nhiên 256 bit, chỉ lưu SHA-256 của token và hạn 10 phút, thông báo mở ô tủ. Ô tủ vẫn giữ `Occupied`.
  - **Bước 2 (`POST /wash-order/{id}/confirm-pickup`):** Xác thực token phiên hợp lệ, giải phóng ô trả đồ `Available`, hủy token phiên, chuyển `Returned` rồi tự động chuyển tiếp `Completed` trong cùng transaction.
- [x] **Customer nhận đồ tại Kho (`POST /wash-order/{id}/pickup-storage`):** Dành cho đơn `MovedToStorage`, tính lại phí lưu kho thực tế (phí locker cũ + 100.000đ/ngày lưu kho), kiểm tra đã thanh toán đủ, chuyển `Returned` -> `Completed` mà TUYỆT ĐỐI KHÔNG giải phóng ô locker (vì đã giải phóng lúc chuyển kho).
- [x] **Gọi lặp:** Các hành động gửi/lấy/trả/xác nhận nhận đồ kiểm tra quyền trước và trả 200 khi đã hoàn tất đúng bước, không thay đổi ô/bộ đếm/lịch sử lần nữa. Mở lại ô nhận khi còn ReadyToReturn cấp phiên mới và vô hiệu token phiên trước; không giải phóng ô.
- [x] **Xác minh sau sửa 31/10:** Đã dọn khối code trùng; build không có lỗi. Local build có NU1900 vì không truy cập được nguồn audit NuGet; Docker publish thành công. EF không có pending model changes; migration `20261030184032_AddQrPinDeliveryActions` đã được startup áp dụng vào PostgreSQL local. Swagger có đủ 6 route hành động và summary.
- [x] **Smoke QR/PIN PASS trên Docker:** 5 request sai PIN đồng thời ghi đủ 5 lượt và khóa mã; hết hạn khóa reset lượt sai; QR gửi không dùng nhận đồ; kiểm tra đúng Shipper; token nhận hết hạn/sai/chưa mở bị từ chối; bước mở giữ nguyên ô; xác nhận nhận kiểm tra lại thanh toán; replay không giải phóng ô lần hai. Nhận kho dùng cùng công thức với Payment, phí thử 180.000đ tại tủ + 200.000đ/25 giờ kho = 380.000đ, thanh toán sandbox rồi hoàn tất không đổi số ô. Tủ và Shipper fixture được vô hiệu, giữ lịch sử đơn/giao dịch.
- [x] **Hồi quy quyền/thanh toán PASS:** `tests/smoke/order-security.ps1` dùng các endpoint hành động mới; kiểm tra phạm vi, dữ liệu riêng tư, Actor từ JWT, chặn Shipper đổi bước giặt, callback mock/replay và IPN chữ ký sai. Đơn thử hoàn tất `3a24a05b-f4ea-952c-64e5-8654e5c2cd55`.
- [x] **Hồi quy cân và phân công PASS trên bản cuối:** Cập nhật `actual-weight.ps1`, `shipper-assignment.ps1`, `order-security.ps1` sang endpoint QR/PIN, giữ các assertion chặn API status. Bộ cân kiểm tra dưới/đúng/vượt 3/5/10 kg, kg lẻ, nhiều dịch vụ, snapshot giá/ETA, replay đồng thời và toàn vòng đến Completed. Bộ phân công kiểm tra quyền/tiệm/tài khoản, hai đơn tranh Shipper, replay và phân công riêng chuyến trả; các đơn thử hoàn tất `3a24a05d-8820-ebac-0a71-931bf535bf4a`, `3a24a05d-886c-0aac-77b4-ba6581219aa2`.
- [ ] Chưa kiểm thử VNPay thật, phần cứng locker hoặc kiểm thử tải; SLA không có mốc lịch sử được bỏ qua thay vì gán thời điểm giả vào dữ liệu.

**Đã chỉnh tài liệu nguồn ngày 29/10:** Sửa UC 2.4.4 thành một luồng chính tuần tự, đưa quy định lấy/trả và phân công từng chuyến vào hàng Quy tắc nghiệp vụ; sửa mô tả/điều kiện và trạng thái trong UC Shipper trả đồ, Customer nhận đồ; Customer được hệ thống tự hoàn tất đơn sau khi nhận, không thêm bước Admin. Sửa câu thanh toán sau khi nhận đồ ở UC 1.2.1 thành thanh toán trước khi trả về locker. Đã lưu bản dự phòng cạnh file gốc; mới kiểm tra nội dung và gói DOCX, chưa xác minh bố cục trang do công cụ xuất bản xem trước không khởi động được. Lỗi lặp tiêu đề/mục 1.3 đã được dọn ngày 31/10: giữ một bảng Hiển thị QR Code, các mục Customer lần lượt là 1.3.1 Hiển thị QR, 1.3.2 Gửi đồ, 1.3.3 Lấy đồ, 1.3.4 Nhận tại kho.

### Quy tắc cân thực tế và chốt giá đã thống nhất ngày 29/10/2026

| Tham số | Giá trị đã chốt |
| --- | --- |
| Small: khối lượng bao gồm trong giá | 3 kg |
| Standard: khối lượng bao gồm trong giá | 5 kg |
| Large: khối lượng bao gồm trong giá | 10 kg |
| Khối lượng vượt | `max(0, ActualWeightKg - IncludedWeightKg)` |
| Phụ phí | `ExcessWeightKg * 15000`, giữ nguyên phần kg thập phân |
| Giá cuối cùng theo quy tắc gói | `BasePrice + ExcessWeightFee`; không tính thêm lần hai cho phần kg đã bao gồm |
| Thời gian xử lý dự kiến | `BaseDurationHours + 2 * ceiling(ExcessWeightKg)` |

Ví dụ đã thống nhất: Standard, giá cơ bản 100.000đ, thời gian cơ bản 24 giờ, cân 6,5 kg → vượt 1,5 kg, phụ phí 22.500đ, giá 122.500đ, thời gian xử lý dự kiến 28 giờ. Dưới hoặc đúng ngưỡng thì giữ giá và thời gian cơ bản. Đây là mô hình nghiệp vụ đồ án; giới hạn vật lý tối đa của ô tủ cần quy định riêng theo thiết bị.

- [x] Người dùng đã chốt bộ số và cách tính nêu trên; không còn là đề xuất chờ xác nhận.
- [x] Đã lưu bộ số, công thức và ví dụ vào hàng Quy tắc nghiệp vụ của UC 2.3.4 trong `../Đặc tả usecase.docx`; sửa luồng chính thành các bước tuần tự và đổi cảnh báo vượt mức tính giá để không nhầm với giới hạn vật lý. Có bản dự phòng cạnh file gốc; đã kiểm tra cấu trúc và nội dung DOCX. Xuất bản xem trước Word chưa hoàn tất, nên chưa xác minh bố cục trang.
- [x] `PUT /wash-order/{id}/actual-weight` yêu cầu policy `MerchantOnly`; Merchant chỉ cân đơn của tiệm ở `Collected`, chưa thanh toán. Khối lượng phải > 0, tối đa 1.000 kg và tối đa 2 chữ số thập phân; đây là giới hạn đầu vào kỹ thuật, không thay giới hạn vật lý của ô.
- [x] Gói theo kg dùng `WashService.UnitPrice` làm giá cơ bản cho phần kg bao gồm; mỗi loại gói chọn số lượng 1, `WeightKg` ước tính khi tạo đơn là tùy chọn và không nhân vào giá. Dịch vụ chiếc/đôi vẫn dùng số lượng x (`PricePerItem` hoặc `UnitPrice`). Đơn nhiều dịch vụ cộng giá cơ bản từng dòng, phụ phí cân chỉ cộng một lần trên đơn có gói theo kg. Đơn chỉ có chiếc/đôi không áp dụng API cân kg.
- [x] ETA tính từ thời điểm Merchant xác nhận cân; thời lượng cơ bản là giá trị lớn nhất trong các dịch vụ trên đơn, được lưu lúc đặt. Lưu `EstimatedFinishAt`, `EtaUpdatedAt` và đồng bộ `ExpectedDeliveryTime` theo Unix seconds. Sửa bảng giá/thời lượng dịch vụ sau khi đặt không làm đổi giá gốc/ETA cơ bản của đơn.
- [x] Merchant được sửa kg khi đơn còn `Collected` và chưa thanh toán; mỗi lần tính lại từ `TotalAmount` gốc, thay phụ phí cũ. Gửi lại cùng kg không tạo timeline hoặc kéo dài ETA; khóa order trong transaction để xử lý request đồng thời. Sau khi bắt đầu giặt/đã thanh toán không cho sửa kg.
- [x] Cân xong giữ `Collected`. Với đơn mới có gói kg, chặn bắt đầu giặt hoặc yêu cầu thanh toán nếu chưa cân thực tế. Chỉ sau các bước giặt mới chuyển `AwaitingPayment`; tạo payment URL trước đó bị từ chối. Word UC 2.3.4 đã sửa tương ứng, có backup cạnh file nguồn; chưa xác minh bố cục trang Word.
- [x] Migration `20261029165358_AddActualWeightPricing` đã được áp dụng vào PostgreSQL local, bổ sung mức kg bao gồm, thời lượng gốc, phụ phí và dấu hiệu dòng dịch vụ theo kg. Đơn cũ không có dữ liệu giá gói được giữ nguyên luồng cũ và bị từ chối áp quy tắc cân mới; không tự lấy bảng giá hiện tại để đổi giá lịch sử. Backend build 0 warning/0 error; EF không có pending model changes; Swagger HTTP 200 và có route `/wash-order/{id}/actual-weight`.
- [x] Docker backend đã rebuild và chạy API mới; `tests/smoke/actual-weight.ps1` PASS trên tủ/dịch vụ thử riêng: dưới/đúng/vượt các mốc 3/5/10 kg, số kg lẻ, đơn nhiều gói + chiếc/đôi, không nhân kg hai lần, giá/thời lượng đã lưu, quyền Merchant, payload sai, replay đồng thời, sửa kg không cộng phí dồn, chặn thanh toán trước giặt và khóa sửa khi đã trả tiền. Các đơn thử đi đến `Completed`, giải phóng ô; tủ/dịch vụ thử được ẩn, phiên test được thu hồi.
- [x] `tests/smoke/order-security.ps1` PASS hồi quy trên bản Docker mới; các script hồi quy đã bổ sung bước cân nếu đơn có gói kg.
- [ ] Module thông báo chưa có; gửi nhắc thanh toán sẽ bổ sung tại thời điểm giặt xong/chuyển `AwaitingPayment`, không gửi khi vừa cân.

### Kết quả xác minh đợt khóa quyền 29/10/2026

- [x] Backend build: 0 warning, 0 error. Sửa thêm khai báo `GetListAsync` bị đứt trong `IMerchantAppService.cs` phát hiện lúc build.
- [x] Docker backend đã rebuild và chạy bản mới bằng `docker compose -f ../docker-compose.yml up -d --build washgo-be`.
- [x] `tests/smoke/order-security.ps1` PASS trên Docker: cả Admin/Merchant/Shipper bị chặn chuyển `Paid`/`Cancelled`; Merchant/Shipper không đọc/sửa/lọc được đơn ngoài phạm vi; dữ liệu khách và mã mở tủ được ẩn cả trong response cập nhật; actor giả từ body bị bỏ qua; callback sandbox thanh toán thành công, replay không tạo thêm timeline; IPN chữ ký sai bị từ chối.
- [x] Smoke test chạy đến `Completed`, giải phóng ô và thu hồi các phiên test; giữ đơn test riêng `3a249827-ae3f-84fd-580a-8783a10f5f29` để đối chiếu. Một fixture thử đầu tiên được hủy để giải phóng ô. Không thay đổi schema trong đợt này.
- [ ] Chưa test IPN có chữ ký thật từ VNPay; chưa hoàn thiện QR/PIN, kg thực tế và phí hủy. Phân công Shipper bởi Merchant đã bổ sung ở đợt tiếp theo dưới đây.

### Kết quả triển khai phân công Shipper 29/10/2026 — UC 2.4.4

- [x] `GET /wash-order/{id}/available-shippers`: Merchant của đơn/Admin xem Shipper cùng tiệm, có tài khoản và role Shipper đang hoạt động, `IsActive = true`, `Status = Available`, không bận chuyến khác. Trả ID hồ sơ, tên/điện thoại, phương tiện/biển số, rating, số chuyến đang chờ và loại nhiệm vụ. Danh sách là ảnh chụp; API phân công kiểm tra lại dưới khóa.
- [x] `PUT /wash-order/assign-shipper` dùng policy `MerchantOrAdmin`. Body chỉ cần `orderId`, `shipperId`; tên Shipper lấy từ User trên server, không tin `shipperName` client gửi. Response Merchant tiếp tục được che thông tin khách.
- [x] Giai đoạn phân công theo ánh xạ trạng thái hiện có: `Deposited = 2` → nhiệm vụ `Collect = 1`; `Paid = 9` và `PaymentStatus.Paid` → nhiệm vụ `Return = 2`. `ReadyToReturn = 10` đã có đồ trong ô trả nên không dùng để bắt đầu phân công chuyến trả. Cần phân công lại chuyến trả dù dùng cùng Shipper của chuyến lấy.
- [x] Lưu `AssignedTaskType` và `ShipperAssignedAt` trên đơn, ghi timeline actor từ JWT. Request lặp cùng Shipper/cùng nhiệm vụ không tạo lịch sử trùng. Shipper gọi bước lấy/trả bị chặn nếu chưa có đúng loại nhiệm vụ.
- [x] Tính bận từ nhiệm vụ chưa hoàn tất trên đơn (`Deposited/Collect` hoặc `Paid/Return`) kết hợp trạng thái hồ sơ; khi sang `Collected` hoặc `ReadyToReturn`, chuyến tương ứng hết bận. Không tự ghi đè trạng thái hồ sơ `Busy/Offline` do người vận hành quản lý.
- [x] Migration `20261029084314_AddShipperTaskAssignment` đã được Docker áp dụng vào PostgreSQL local. Đơn cũ `Deposited` có Shipper được nhận diện là chuyến lấy; đơn cũ `Paid` phải được phân công chuyến trả rõ ràng. Không tạo thời điểm phân công giả cho dữ liệu cũ.
- [x] Backend build 0 warning/0 error, EF không có pending model changes; Docker backend đã rebuild; Swagger trả HTTP 200 và có hai route phân công/danh sách khả dụng.
- [x] `tests/smoke/shipper-assignment.ps1` PASS: đúng quyền/giai đoạn/tiệm, chặn hồ sơ inactive/busy/offline và User inactive, tên từ server, lặp không trùng timeline, hai request đồng thời tranh một Shipper trả 200/400; giải phóng khả dụng sau thu gom và yêu cầu phân công lại khi trả đồ. Hai đơn test đã `Completed`; tài khoản/hồ sơ Shipper thử đã dọn và phiên test được thu hồi.
- [x] `tests/smoke/order-security.ps1` PASS hồi quy với quy tắc phân công mới, bao gồm thanh toán và replay. Script đã đổi thứ tự: khách gửi đồ trước khi phân công; phân công chuyến trả sau khi thanh toán.
- [ ] Chưa có thông báo cho Shipper (module 5), màn hình điều phối, phân công tự động hay bảng lịch sử nhiệm vụ riêng. Các phần này chưa được đánh dấu hoàn tất UC toàn diện.

---

## III. Nhật ký kỹ thuật đã triển khai (không thay cho nghiệm thu use case)

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
- [x] **API cho đăng ký Customer theo đặc tả/Figma**: UI thu thập họ tên, email, số điện thoại, mật khẩu và xác nhận mật khẩu; sau đó gọi `send-otp` → `verify-otp` trả `verificationToken` có hạn 10 phút → `register` nhận token cùng thông tin form. OTP không thể dùng lại sau khi xác minh; token được lưu dạng BCrypt trên bản ghi OTP hiện có. `register` trả ID/email và thông báo đăng nhập, không cấp phiên tự động. Quên mật khẩu dùng `send-otp` với purpose ForgotPassword → `verify-otp` trả token → `reset-password` nhận email, verificationToken, mật khẩu mới và xác nhận mật khẩu mới (cập nhật 03/11/2026).
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

### 4. API CRUD nền tảng & phần code luồng đơn đã có (chưa nghiệm thu toàn UC)
> **Lưu ý Routing**: Hiện tại các route trong backend map trực tiếp không có prefix `/api/v1` (ví dụ: `/auth`, `/service-area`, `/merchant`, `/locker`, `/wash-service`, `/wash-order`). Nếu muốn chuyển sang chuẩn `/api/v1/...` sẽ cấu hình route group hoặc global prefix sau.

- [x] **Module ServiceArea (`/service-area`)**: Danh sách, Chi tiết, Tạo mới, Cập nhật, Đổi trạng thái kích hoạt.
- [x] **Module Merchant (`/merchant`)**: Danh sách (kèm filter/paging), Chi tiết, Tạo mới, Duyệt đối tác (`APPROVED`/`SUSPENDED`).
- [x] **Module Locker & LockerBox (`/locker`)**: Quản lý tủ, ô tủ, cập nhật trạng thái hoạt động/bảo trì.
- [x] **Module WashService (`/wash-service`)**: Quản lý danh mục dịch vụ giặt, bảng giá (theo kg / theo món).
- [x] **Module WashOrder (`/wash-order`) & chuyển trạng thái cơ bản hiện có**:
  - Quản lý tạo đơn gắn liền với Customer, Merchant, Locker, LockerSlot và danh sách chi tiết dịch vụ.
  - Kiểm tra và thực thi luồng tuần tự:
    `PENDING` -> `DEPOSITED` -> `COLLECTED` -> `WASHING` -> `DRYING` -> `IRONING` -> `FOLDING` -> `AWAITING_PAYMENT` -> `PAID` -> `READY_TO_RETURN` -> `RETURNED` -> `COMPLETED`.
  - **Quy tắc trong code hiện tại**: Chặn `READY_TO_RETURN` và `RETURNED` nếu `PaymentStatus` chưa `PAID`; từ 29/10 route công khai đã chặn chuyển `Paid`, việc xác nhận thanh toán nằm trong service thanh toán nội bộ.
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
  - `POST /wash-order/{id}/cancel`: Customer hủy đơn của mình ở `Pending`, `Deposited`, `Collected` đã cân; backend tính phí và chỉ giải phóng ngay ô `Reserved`, giữ `Occupied` đến lúc nhận lại đồ.
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
- [x] Chỉ nhận dịch vụ đang bán, đã duyệt, thuộc đúng Merchant của tủ. Yêu cầu 1–30 dịch vụ không trùng, số lượng/khối lượng hợp lệ, phương thức thanh toán hợp lệ. Từ đợt cân thực tế: gói kg chọn số lượng 1, kg ước tính là tùy chọn; giá cơ bản gói không nhân kg, chốt phụ phí sau khi Merchant cân. Giá và thời lượng được lấy từ server rồi lưu trên đơn, không tin giá client.
- [x] Giữ ô bằng `UPDATE` có điều kiện theo trạng thái `Available`; cập nhật bộ đếm ô và tạo order trong cùng transaction. Request thứ hai chọn cùng ô nhận 400 thay vì tạo thêm order. Không đổi schema, không cần migration.
- [x] Build backend sạch (0 warning, 0 error), Docker đã rebuild và test runtime ngày 26/10/2026: tạo/hủy đơn khôi phục số ô trống, hai request đồng thời cùng ô nhận 201/400, sáu payload sai đều nhận 400 và không giữ ô.
- [ ] Cần kiểm thử tải, thanh toán end-to-end và các thao tác quản trị locker đồng thời trước khi dùng production.

### 4.4. Nhật ký nhánh hủy Pending cũ (26–29/10; được thay thế bằng luồng 31/10 bên trên)
- [x] Customer `POST /wash-order/{id}/cancel` chỉ đổi đơn từ `Pending` sang `Cancelled` bằng cập nhật có điều kiện, rồi giải phóng đúng ô đang `Reserved`; trạng thái đơn, trạng thái ô, bộ đếm ô và lịch sử hủy cùng một transaction. Nếu một bước không thành công, transaction không commit.
- [x] Ngày 29/10 đã khóa `Cancelled` ở `PUT /wash-order/status`; hủy đơn đi qua `/cancel`. Nhánh `Pending → Deposited` vẫn có cập nhật có điều kiện đối với ô `Reserved`, nhưng còn thiếu xác minh QR/PIN và endpoint Customer riêng.
- [x] Kết quả smoke test lịch sử ngày 26/10/2026: hai request hủy đồng thời trả 200/400 và bộ đếm ô không bị lệch. Test này chỉ xác minh nhánh cũ, không nghiệm thu nghiệp vụ hủy theo đặc tả.
- [x] Ngày 31/10 đã bổ sung phí theo trạng thái/thời gian, hoàn tiền và nhận lại đồ ở phần triển khai mới; thông báo vẫn chưa có.
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

## IV. Backlog kỹ thuật chi tiết (đọc cùng bản đồ use case ở đầu file)

### 1. Phân quyền Endpoint chặt chẽ (RBAC Enforce) & Chuẩn hóa Auth Production
- [x] Gắn Policy `.RequireAuthorization(...)` chi tiết cho từng nhóm endpoint backend:
  - ServiceArea: `AdminOnly`.
  - Merchant: `AdminOnly`.
  - Locker và WashService: `MerchantOrAdmin`.
  - WashOrder: danh sách quản trị và bộ lọc cột dành cho `Admin`, `Merchant`, `Shipper` trong đúng phạm vi đơn; Customer dùng `POST /wash-order/mine` và chỉ xem được chi tiết đơn của chính mình. Tạo/hủy đơn bởi `Customer`; cập nhật trạng thái theo vai trò; từ 29/10 Merchant của đơn và Admin được phân công Shipper cùng tiệm.
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
  - `POST /payment/callback`: callback sandbox yêu cầu Admin, chuyển đơn sang `PAID` qua private method trong service thanh toán, kiểm tra giao dịch/số tiền và ghi timeline; không gọi API đổi trạng thái công khai.
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
- [x] Phân công thủ công lấy/trả đồ bởi Merchant/Admin và danh sách khả dụng theo đơn; có kiểm tra quyền, trạng thái và chống phân công trùng đồng thời (xem mục UC 2.4.4 ở đầu file).
- [ ] Phân công tự động và bảng lịch sử `DeliveryAssignment` riêng; hiện lưu nhiệm vụ hiện tại trên đơn và sự kiện phân công trong timeline.
- [ ] API & Luồng nghiệp vụ cho Shipper:
  - Shipper nhận chuyến, xác nhận đã lấy đồ tại tủ (mở tủ bằng mã Shipper).
  - Shipper giao đồ tới tiệm giặt và giao đồ hoàn trả lại tủ.
- [ ] Màn hình điều phối giao nhận trên Admin UI.

---

### 4. Module Khuyến mãi & Giảm giá (Promotions & Vouchers)
- [x] Quản lý chương trình khuyến mãi (`Promotion`, `PromotionUsage`): mã theo %/cố định, đơn tối thiểu, giảm tối đa, tổng lượt, lượt/khách, thời gian hiệu lực (01/11/2026).
  - [ ] Giảm giá cho đơn hàng đầu tiên của khách hàng mới.
- [x] API báo giá/áp dụng voucher (`POST /wash-order/apply-voucher`) và `promotionCode` khi tạo đơn.
- [ ] Giao diện quản lý khuyến mãi trên Admin UI (FE chưa làm).

---

### 5. Module Đánh giá & Xử lý sự cố (Rating & Damage Report)
- [x] Đánh giá dịch vụ (`OrderRating`): sao 1–5 + tiêu chí, nhận xét, ảnh; điểm trung bình Merchant (01/11/2026).
- [x] Báo cáo sự cố (`Incident`): hư hỏng/mất đồ/khác, mức độ, ảnh bắt buộc, Admin xử lý và bồi thường (01/11/2026).
- [ ] Xử lý đồ tồn kho lưu trữ lâu ngày (`StorageItem`):
  - Thu gom đồ quá hạn không có người lấy về kho lưu trữ tập trung.

---

### 6. Module Đối soát doanh thu đối tác (Merchant Settlement)
- [x] Quản lý đối soát & doanh thu (`MerchantSettlement`, 01/11/2026 — xem mục Quản lý tài chính):
  - Tính toán tổng doanh thu định kỳ (hàng tuần/hàng tháng) cho từng Merchant.
  - Trừ hoa hồng nền tảng WashGo (Platform Fee %).
  - Tạo kỳ đối soát và xác nhận thanh toán chuyển khoản cho đối tác.
- [ ] Báo cáo thống kê tài chính trên Admin UI: Biểu đồ doanh thu, số lượng đơn giặt, tỷ lệ hoàn tất.

---

### 7. Module Thông báo tự động (Notification System)
- [x] Thông báo trong ứng dụng + real-time SignalR (01/11/2026) — xem mục Thông báo ở đầu file.
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
- [x] Cấu hình hệ thống động (`SystemSetting`, 01/11/2026 — xem mục Cài đặt hệ thống):
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
