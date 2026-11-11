# Kế hoạch hoàn thiện Backend Shipper — 11/11/2026

Trạng thái cập nhật 11/11/2026: **BE cốt lõi đã triển khai và nghiệm thu local**. Plan giữ chi tiết phạm vi và đề xuất có điều kiện; trạng thái/bằng chứng tại [checklist](../IMPLEMENTATION_CHECKLIST.md), mục Shipper 11/11; hợp đồng cuối tại [SHIPPER_API_REVIEW.md](SHIPPER_API_REVIEW.md). Không tick toàn bộ đề xuất tùy chọn hoặc IoT/2FA đã loại khỏi phạm vi.

## 1. Nguồn yêu cầu và phạm vi

Đọc đầy đủ ba tài liệu tại root BE:

1. `BACKEND_SHIPPER_DASHBOARD_PROFILE.md` — dashboard, hồ sơ, liên hệ Merchant, preview nhiệm vụ.
2. `BACKEND_SHIPPER_DELIVERY_HISTORY.md` — lịch sử, summary, tìm kiếm, snapshot, export.
3. `BACKEND_SHIPPER_DELIVERY_TASKS.md` — nhiệm vụ, QR/PIN, phân quyền, concurrency, operation lookup, SLA.

Chỉ làm BE. Giữ luồng locker mô phỏng đã chốt; không đưa phần tích hợp phần cứng, routing, thu nhập hoặc 2FA thành yêu cầu bắt buộc để nghiệm thu đợt này. Không sửa namespace toàn dự án trong đợt Shipper. Những mục có chữ “nếu cần” được phân loại tại mục 10 để không bỏ sót hoặc tự coi là yêu cầu đã chốt.

## 2. Hiện trạng xác nhận từ code

| Phần | Đã có | Khoảng trống / hướng xử lý |
| --- | --- | --- |
| Hồ sơ | `ShipperProfileHandler/AppService`: GET/PUT theo UserId JWT, upload thuộc người dùng, cập nhật User + Shipper | PUT đọc trước UoW riêng; chưa có guard phiên bản và operation lookup. Thiếu liên hệ/ảnh Merchant, ngày tham gia, lỗi có mã. |
| Phiên bản Shipper | Entity `Shipper.Revision` và cơ chế DB tăng revision; quản lý Shipper đã dùng expectedRevision | Dùng lại và kiểm tra toàn bộ đường ghi; phiên bản hồ sơ còn phải bao phủ thay đổi User, không chỉ Shipper. |
| DeliveryTask | Entity, EF config, recorder Assigned/Completed/Reassigned/Cancelled, lịch sử dành Merchant/Admin | Không tạo bảng nhiệm vụ mới trùng. Chưa có API tự xem nhiệm vụ cho Shipper; thiếu actor hoàn tất, snapshot địa điểm, SLA và kết nối event. |
| Lịch sử Shipper | `ShipperDeliveryAppService` query timeline ActorId/ActorRole, giữ quyền theo người thực hiện | Chưa có summary/detail/export/keyword/lookup; count trước khi bỏ order thiếu nên có thể lệch list. Tên/ô lấy dữ liệu hiện tại. |
| Giao nhận | Collect/return có khóa đơn, UoW, mã riêng, bộ đếm PIN, ghi timeline/task, tăng lượt; hỗ trợ trả đơn hủy | Command chỉ có pinCode/qrCode; thiếu taskId/version/revision và ledger lookup cho Shipper. Replay phụ thuộc trạng thái hiện tại. |
| Preview | `OrderListEnricher.FillTaskAsync` có tên/địa chỉ/ô cho detail | Chưa enrich danh sách theo đúng nhiệm vụ; fallback tên tủ trả về tủ gửi cần sửa khi hai địa điểm khác nhau. |
| Ảnh và liên hệ đơn | `OrderMediaEnricher` đã batch ảnh tủ gửi/trả, ảnh Merchant, MerchantPhoneNumber, ảnh items.ServiceImageUrls | Nhận định “chưa có ảnh dịch vụ” trong tài liệu đã cũ. Tái sử dụng; bổ sung taskLocker*, email nghiệp vụ và kiểm tra payload thực tế theo role. Không tạo trường phone trùng vô cớ. |
| Bảo mật tài khoản | `/auth/security`, `/auth/change-password`, `/auth/sessions`, revoke phiên dùng RequireAuthorization | Có thể dùng cho Shipper; cần test role, không xây lại. 2FA hiện không hỗ trợ. `/admin/me/activities` vẫn AdminOnly, chưa phải nhật ký Shipper. |
| SLA | Đơn có CollectSlaHoursSnapshot/ReturnSlaHoursSnapshot; giao nhận đọc policy theo đơn | Cần đưa mốc gốc/deadline/coverage vào task, lịch sử và dashboard. Trễ SLA cảnh báo, không chặn lấy/trả. |

Không lấy mô tả FE là bằng chứng API đã có hoặc đã chạy đúng. Bản kế hoạch này dựa vào đọc source, chưa kiểm tra Swagger đang chạy.

## 3. Hợp đồng dùng chung phải thống nhất trước

- Identity: JWT `User.Id` → tìm `Shipper.UserId` → dùng `Shipper.Id` nghiệp vụ. Không tin shipperId/merchantId từ request tự phục vụ. User khóa, Shipper xóa/ngừng hoạt động phải được chặn theo quy tắc quyền hiện hành.
- Quyền: danh sách/chi tiết hiện tại theo nhiệm vụ được giao; lịch sử theo actor thực hiện; operation theo actor tạo thao tác. Không dùng quyền hiện tại của đơn thay quyền lịch sử.
- `taskId` là định danh một lần phân công; phân công lại tạo task mới. `assignmentVersion` ổn định trong thế hệ phân công, không đổi vì thay đổi giá/trạng thái không liên quan. `orderRevision` là phiên bản khác, không dùng thay taskId.
- Query mới kế thừa `BaseFilterQuery` khi phù hợp, đặt Page=1, Take=20 rõ ràng (base hiện mặc định Page=0). Không tự biến endpoint cũ từ 1-based sang 0-based. Giữ tương thích fromDate/toDate cũ.
- Enum sai, Unix âm/quá miền, khoảng ngược, sort không whitelist, page/take không hợp lệ → 400 có mã. Kỳ đọc tổng hợp tối đa 366 ngày; quy định rõ default/giới hạn cho request không có cận.
- UTC cho lưu trữ/response; lịch nhóm theo `Asia/Ho_Chi_Minh`, ngày VN 00:00–23:59:59. Tuần = 7 ngày gần nhất, tháng = đầu tháng đến hiện tại; custom không ở tương lai. Trả khoảng thực tế, timezone, generatedAt/serverNow.
- Ảnh [] khi thiếu, phone/address null khi thiếu. Không fallback tủ trả sang tủ gửi khác ID. Không lộ tên/SĐT khách, mã khách, CCCD, tài liệu hợp đồng, ngân hàng hoặc PIN/QR trong list/history/summary/export/audit.
- Dùng DTO riêng cho preview/credential; SelectFields hoặc serializer không được đưa bí mật trở lại payload. Ảnh dịch vụ/ảnh trạm không phải bằng chứng giao nhận.
- Mỗi file giữ đúng tầng Contracts/Queries/Commands/Results/Services, Domain và EF; dùng using/alias phía trên thay namespace dài trong phần xử lý mới.

## 4. Chặng 1 — Nền dữ liệu nhiệm vụ và lịch sử

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh dự kiến: `feat/shipper-task-snapshots`.

- [ ] Mở rộng DeliveryTask hiện có: assignment version, thời điểm/mốc SLA, SLA hours theo policy đã chốt của đơn, deadline; actor hoàn tất UserId/role và eventId liên kết timeline.
- [ ] Snapshot khi hoàn tất: mã đơn/mã Merchant, Merchant liên quan, lockerId/name/address/coordinates, boxId/number, loại nhiệm vụ, trạng thái đơn tại sự kiện, ghi chú đã lọc. Ghi snapshot trong cùng transaction collect/return; Return dùng đúng ô BE vừa chiếm.
- [ ] Giữ cả assigned Shipper và actual completion actor. Admin làm thay phải ghi Admin; không tự ghi Admin thành Shipper để tăng KPI actor. Giữ totalDeliveries hiện hành và mô tả riêng nếu khác số lượt chính Shipper thực hiện.
- [ ] Giữ timeline làm nguồn event lịch sử; liên kết với task mới để có assignedAt/SLA. Không đếm cùng một lượt hai lần từ cả timeline và DeliveryTask.
- [ ] Snapshot tên/địa chỉ/ô bất biến. Ảnh dùng nguồn hiện tại còn được cấp quyền, đánh dấu `imageBasis=CurrentResource`; thiếu/xóa ảnh trả []. Nếu lưu URL snapshot phải tích hợp vòng đời upload, không hứa URL sống vĩnh viễn.
- [ ] Migration/index theo truy vấn Shipper/actor + task status/type + assignedAt/completedAt/eventId; unique liên kết event khi có. Kiểm tra FK hiện tại DeliveryTask→Order Cascade trước khi quyết định retention; không để xóa cứng order làm mất nguồn thống kê đã cam kết.
- [ ] Dữ liệu cũ chỉ backfill từ mốc/sự kiện có bằng chứng. Snapshot dựng từ tài nguyên hiện tại phải ghi `snapshotSource=LegacyCurrentResource`, độ phủ thiếu/null; không gọi đó là địa chỉ/ô lịch sử chính xác. Mốc không biết để null, không dùng CreatedOn của đơn thay AssignedAt.

Nghiệm thu: phân công lại cùng/khác Shipper không mất lượt cũ; một đơn có collect/return riêng; đổi tên/xóa mềm trạm không làm mất sự kiện; Admin làm thay phân biệt đúng actor; migration không tạo duplicate.

## 5. Chặng 2 — Nhiệm vụ riêng, preview và cấp mã

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh: `feat/shipper-task-api` (sau chặng 1).

| API dự kiến | Nội dung |
| --- | --- |
| `POST /shipper-tasks/filter` | ShipperOnly, keyword hai mã đơn, taskType/status, assignedFrom/assignedTo; mặc định Assigned. Sort AssignedAt/deadline và taskId ổn định. |
| `POST /shipper-tasks/summary` | Snapshot totalPending/collectPending/returnPending. Cùng keyword/ngày phân công; độc lập taskType/status của bảng, ghi rõ query áp dụng. |
| `GET /shipper-tasks/{taskId}` | Chi tiết đã scope: task/order version, địa điểm, Merchant, items, canCollect/canReturn, nextAction, reasonCode, deadline/serverNow. Không credential mặc định. |
| `GET /shipper-tasks/{taskId}/credentials` | Chỉ nhiệm vụ đang hoạt động của Shipper được giao; Admin dùng quyền quản trị tương ứng. Trả mã theo hành động xem có audit, không cache, không log giá trị. |

- [ ] DTO gồm taskId/taskType/taskStatus/assignmentVersion/orderRevision/orderId/hai mã đơn/assignedAt/completedAt/endedAt, SLA và taskLockerName/address/imageUrls/latitude/longitude/taskBoxNumber.
- [ ] Batch enrich theo tập ID, tái sử dụng OrderMediaEnricher. Collect lấy tủ gửi; Return lấy tủ trả, bao gồm đơn hủy. Chưa chọn ô trả thì null.
- [ ] Bổ sung taskLocker* và whitelist sort ShipperAssignedAt/deadline cho `/wash-order/filter` cũ, giữ tie-break ID và scope staff. Không biến CreatedOn thành ngày phân công.
- [ ] Rà `/wash-order/filter` và GET detail để không còn mã nhiệm vụ/khách ngoài endpoint cấp mã đã chốt. Chuyển FE cần có ghi chú breaking change rõ, không âm thầm cắt mã rồi coi FE cũ vẫn chạy.
- [ ] Dùng tên `merchantPhoneNumber` sẵn có ở DTO đơn; bổ sung merchantEmail nghiệp vụ nếu có nguồn hợp lệ, không lấy email cá nhân khác làm liên hệ. Ảnh items đã có chỉ cần test/enrich đúng role.
- [ ] Không hiển thị canMoveToStorage như một nhiệm vụ Collect/Return; chức năng kho thuộc mục 10.

Nghiệm thu: không gọi N+1 theo dòng; tủ trả khác tủ gửi vẫn đúng ảnh/địa chỉ; truy cập task người khác bị từ chối; list/detail mặc định không mã; credential cũ bị từ chối sau phân công lại.

## 6. Chặng 3 — Giao nhận có guard, idempotency và lookup

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh: `feat/shipper-delivery-write-safety` (sau chặng 2).

- [ ] Collect/return nhận taskId, expectedAssignmentVersion, expectedTaskType, expectedRevision và operationId, cùng đúng một PIN/QR. PIN ASCII 6 số; QR có trần độ dài, đúng loại/mã đơn. Validate độc lập FE.
- [ ] Dùng lại hạ tầng GuardedOrderCommand/OrderWriteGuard nếu phù hợp; không mở API operation Merchant/Admin cho Shipper. Scope operation mới theo actor + task + action + operationId.
- [ ] Trình tự ghi: xác thực JWT/scope → khóa theo cùng thứ tự với assign/cancel → tìm operation cũ và đối chiếu fingerprint → kiểm tra task/version/revision/stage → kiểm tra mã/lockout → đổi order/slot/count/task/timeline → lưu kết quả operation → commit duy nhất transaction thành công.
- [ ] Không lưu plaintext PIN/QR hoặc hash PIN 6 số không có khóa vào ledger/log/audit. Nếu fingerprint bao phủ credential, dùng HMAC với khóa server riêng; lưu kết quả đã redact, kể cả snapshot response lỗi.
- [ ] Replay cùng operation trả lại kết quả ban đầu ngay cả khi đơn đã Washing/Completed hoặc giao lại; kiểm tra actor của operation trước replay. Không tăng bộ đếm PIN, totalDeliveries, timeline hoặc bộ đếm ô lần hai. Cùng ID khác payload trả 409.
- [ ] Lookup `GET /shipper-tasks/{taskId}/operations/{operationId}` chỉ owner operation; trả result revision/ô thực tế và trạng thái kết quả. Không dùng trạng thái đơn hiện tại thay bằng chứng request trước thành công.
- [ ] Không tìm thấy ledger không khẳng định request trước đã thất bại; nếu cần `processing/failed` bền vững phải có mô hình operation attempt riêng và recovery. Bản tối thiểu an toàn trả `unknown/notRecorded`, cho gửi lại chính operationId dưới lock, không sinh ID mới tùy tiện.
- [ ] Sai mã lưu attempt/lockout bền vững trước trả lỗi; không dùng hai transaction khóa cùng record gây deadlock. Reset hết hạn/gán lại theo luật hiện có.
- [ ] Lỗi có meta.code: InvalidTaskCredential, TaskLocked, AssignmentChanged, RevisionMismatch, TaskNotActive, NoSuitableBox, LockerUnavailable, OperationIdReused; kèm remainingAttempts/lockoutUntil/serverNow khi phù hợp, không lộ mã đúng.
- [ ] Return hết ô hoặc tủ không hoạt động: rollback order/slot/count/task, còn nguyên nhiệm vụ để thử lại. Return đơn hủy vẫn Cancelled + AtReturnLocker.
- [ ] SLA lấy tính từ DepositedAt; trả thường từ PaidAt; dùng snapshot policy của đơn. Trễ chỉ cảnh báo và ghi lịch sử. Return đơn hủy thiếu PaidAt phải có mốc riêng được chốt ở mục 10, không giả ngày thanh toán.
- [ ] Giữ mô phỏng một request xác nhận collect/return theo hiện tại; không trả thông báo đã mở cửa thật, không tích hợp IoT trong chặng này.

Nghiệm thu: concurrent double submit, timeout sau commit, replay sau chuyển bước/phân công lại, cùng ID khác payload, PIN sai/khóa, stale revision, không còn ô, cancel/assign chạy đua. Mỗi trường hợp kiểm tra DB/timeline/slot/counter/operation chứ không chỉ HTTP200.

## 7. Chặng 4 — Lịch sử, summary, lookup và export

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh: `feat/shipper-delivery-history` (sau chặng 1; tích hợp lại sau chặng 3).

- [ ] Nâng `POST /shipper-delivery/history`: eventId/taskId, hai mã đơn, taskType, completedAt, trạng thái tại sự kiện và trạng thái hiện tại tách riêng; snapshot địa điểm/ô, ảnh/địa chỉ và metadata nguồn lịch sử.
- [ ] Lọc server trước paging: ngày hoàn tất, keyword hai mã, taskType, lockerId tại sự kiện, nhánh trả đồ hủy, event status; sort whitelist CompletedAt/OrderCode + eventId tie-break. Không lọc theo đơn đang được giao cho ai.
- [ ] Count/list dùng cùng tập sự kiện và snapshot đọc. Giữ đơn xóa mềm theo quyền lịch sử; thiếu tài nguyên không skip dòng sau count. Legacy không phục hồi được order vẫn có DTO lịch sử tối thiểu kèm coverage.
- [ ] `POST /shipper-delivery/summary`: total/collectCount/returnCount, khoảng áp dụng, timezone/generatedAt và coverage. Tái sử dụng bộ lọc history; summary giữ keyword/locker/date nhưng bỏ taskType bảng để ba số so sánh có ý nghĩa; trả appliedFilters rõ ràng.
- [ ] `GET /shipper-delivery/history/{eventId}` kiểm tra completion actor, không current order owner. Không trả mã hoặc action ghi nhận lại. Một order có nhiều lượt không bị gộp.
- [ ] Lookup locker scoped từ các sự kiện của chính Shipper, gồm tên snapshot khi trạm đã xóa; không dùng lookup toàn hệ thống.
- [ ] Export CSV server theo cùng bộ lọc, scope và sort; tối đa 366 ngày, trần 25.000 sự kiện đề xuất theo export hiện có, quá trần báo lỗi rõ; bảo vệ CSV formula injection, không xuất trang đầu rồi gọi là toàn bộ.
- [ ] Lịch sử lớn có index và query bounded; cursor là mở rộng sau đo đạc, không tự đổi hợp đồng paging cũ trong đợt này.

Nghiệm thu: same-second events ổn định, reassignment, order/trạm xóa mềm, tổng=list matching, VN day boundary, invalid enum/Unix/range, keyword trước paging, CSV đúng đầy đủ/giới hạn/privacy.

## 8. Chặng 5 — Dashboard snapshot

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh: `feat/shipper-dashboard` (sau chặng 2 và 4).

- [ ] `GET /shipper-dashboard` ShipperOnly: period day/week/month/custom, fromDate/toDate, tối đa 366 ngày; query DTO có hợp đồng bind rõ ràng.
- [ ] Một snapshot đọc nhất quán: pendingCollectCount/pendingReturnCount hiện tại; completedCollectCount/completedReturnCount theo CompletedAt trong kỳ; totalDeliveries lifetime; status/Merchant; appliedRange/timezone/serverNow/generatedAt.
- [ ] Gắn `pendingBasis=Current` rõ ràng, không mô tả là backlog lịch sử tại cuối kỳ. Nếu lọc ngày phân công thì dùng assignedFrom/assignedTo riêng và trả basis, không lấy CreatedOn.
- [ ] Chart collect/return theo giờ cho kỳ ngày, theo ngày cho kỳ dài; tách số lượt và số đơn. Tạo bucket trống bằng 0 chỉ khi phạm vi dữ liệu đã biết; thiếu lịch sử/assignedAt có coverage rõ.
- [ ] KPI đúng hạn/average duration/kỳ trước dựa event + task + SLA snapshot: numerator/denominator/sampleCount/unknownCount, previousRange và dataCoverage. Không có mẫu → null; không suy deadline từ DepositDeadline/PickupDeadline khách.
- [ ] So sánh kỳ trước cùng độ dài local calendar, kỳ hiện tại chưa kết thúc phải đánh dấu partial. Có null baseline thì không chia ra phần trăm tăng trưởng giả.
- [ ] Danh sách preview tối đa 5 pending collect/return và 5 hoàn tất trong kỳ dùng DTO/batch enrich đã xây; trả deadline/type/đúng trạm, không trả PIN.

Nghiệm thu: đổi tuần/tháng/custom không đổi lifetime/current pending sai nghĩa; KPI không bịa số; trễ SLA vẫn có action; snapshot totals không lệch giữa các query khi ghi đồng thời; reassignment giữ đúng actor completion.

## 9. Chặng 6 — Hồ sơ và bảo mật tự phục vụ

**Phần BE cốt lõi đã triển khai**; kết quả thực tế và giới hạn xem checklist/API review ở đầu file. Danh sách dưới giữ thiết kế và tiêu chí nghiệm thu; các mục có điều kiện không tự coi là hoàn thành.

Nhánh: `feat/shipper-profile-safety` (có thể làm sau chặng 1, không phụ thuộc dashboard).

- [ ] GET/PUT thêm ngày tham gia/updatedAt, revision hồ sơ và Merchant imageUrls/phone (email/address nghiệp vụ nếu có). Join chỉ Merchant của Shipper đang đăng nhập; thiếu trả null/[] và tên fallback rõ, không trả giấy tờ/tài chính.
- [ ] Không bịa ratingCount: kiểm tra có nguồn đánh giá Shipper thật không. Nếu chưa có, trả ratingSource/coverage cùng ratingCount null, rating 0 không được mô tả là đã nhận đánh giá 0 sao. Không lấy rating Merchant làm rating Shipper.
- [ ] Đưa đọc/validate/cập nhật User+Shipper vào UoW có khóa/concurrency thực sự; expectedRevision phải phản ánh cả thay đổi user fields và phương tiện/Merchant. Chuẩn hóa các đường ghi Admin/Merchant/self cùng guard; chỉ kiểm tra s.Revision rồi save ngoài điều kiện không đủ chống race.
- [ ] Profile operationId/clientRequestId + fingerprint + API lookup scope User; replay kết quả cùng request, payload khác409; request timeout tra được. Snapshot operation không lưu mật khẩu/PIN/giấy tờ.
- [ ] Avatar null giữ/empty xóa/newowned thay; length URL thống nhất storage/DB. Xe đạp biển số optional nhưng nếu có vẫn giới hạn độ dài/ký tự; phone giữ quy tắc Shipper hiện hành đến khi nhóm đổi, không tự áp quy tắc Customer.
- [ ] Mã lỗi ShipperProfileMissing/PhoneAlreadyUsed/InvalidProfile/AvatarNotOwned/RevisionMismatch/OperationIdReused. Thiếu User/Shipper/Merchant được phân biệt qua chẩn đoán nội bộ, không mở quyền sửa identity tùy ý.
- [ ] Kiểm tra seed và luồng tạo User+Shipper+Merchant một transaction. Nếu có dữ liệu cũ thiếu liên kết: script chẩn đoán read-only trước, chỉ repair theo mapping Merchant đã xác nhận, không tự tạo trong GET.
- [ ] Test Shipper sử dụng các `/auth/security`, change-password, sessions, revoke hiện có. Không dựng lại OTP quên mật khẩu thành change-password.
- [ ] Nếu đưa nhật ký cá nhân vào phạm vi: thêm route self-service riêng scope ActorId cho Shipper, DTO redact; `/admin/me/activities` giữ AdminOnly. Audit mới cho self-update và xem credential không ghi dữ liệu bí mật.

Nghiệm thu: hai PUT cùng revision chỉ một cập nhật, chạy đua với Merchant sửa User cũng không ghi đè; operation timeout/replay, sai uploader, phone trùng, xe đạp payload dài, không sửa email/Merchant/status qua profile; tài khoản thiếu profile có mã ổn định; credential/session người khác không truy cập được.

## 10. Các đề xuất cần phân biệt khỏi phạm vi đã chốt

| Nội dung trong ba tài liệu | Xử lý kế hoạch |
| --- | --- |
| 2FA | Đã chốt loại khỏi phạm vi, giữ twoFactorSupported=false; không mở lại yêu cầu. |
| Thu nhập Shipper | Không có mô hình chi trả; không tạo số giả từ doanh thu/commission Merchant. Chỉ triển khai sau yêu cầu riêng. |
| GPS/km đường thẳng/link chỉ đường | FE xử lý theo đồng ý người dùng, BE chỉ cung cấp đúng tọa độ trạm; không cần Google API key. Routing đường chạy/ETA là mở rộng riêng. |
| Mở cửa thật, door acknowledgement, phiên đặt/lấy/đóng cửa, thiết bị offline | Ngoài phạm vi mô phỏng. Chỉ khi chuyển IoT mới chốt thiết bị, reservation expiry/recovery, token phiên, bằng chứng và test phần cứng. Không tick đã hoàn thành bằng checkbox mô phỏng. |
| Bằng chứng giao nhận | Chưa có yêu cầu bắt buộc; không dùng ảnh dịch vụ làm bằng chứng. Nếu triển khai cần upload/retention/ownership/read contract riêng. |
| Shipper tự Available/Offline | Đề xuất tùy chọn: endpoint riêng, khóa đang có nhiệm vụ và tách IsActive/Busy suy diễn. Cần nhóm chốt quyền trước khi code; profile PUT không được tự sửa status. |
| Nhận/từ chối/hủy/đổi nhiệm vụ, task chuyển kho | Chưa có workflow phân công và enum tương ứng; không dùng assign Merchant/Admin thay self-service. Cần quyết định tác nhân, trách nhiệm và SLA trước khi mở rộng. API chuyển kho cũ vẫn giữ quyền/guard hiện có. |
| SLA trả đồ hủy chưa thanh toán | Cần chốt mốc (ví dụ lúc đủ phí hủy/được giao Return), áp dụng snapshot nào; chưa có PaidAt thì deadline=null + reason cho đến khi có quyết định, không giả SLA đúng hạn. |
| Nhật ký self-service | Có thể code trực tiếp, liệt kê trong chặng 6 như mở rộng đọc có scope; không mở admin audit cho Shipper. |
| Lịch sử chi tiết/lookup/CSV/KPI | Có thể code trực tiếp theo chặng 4–5; không cần tích hợp tài chính hoặc ML. Giới hạn/metadata đề xuất phải được công bố trong API trước triển khai FE. |

Các dòng ngoài phạm vi không phải “đã code xong”. Khi nhóm chọn mở rộng phải bổ sung checklist con và cập nhật đặc tả trước khi triển khai.

## 11. Test, Docker và cập nhật tài liệu theo từng chặng

- [ ] Viết smoke fixture riêng theo user/shipper/merchant/locker/order của test; cleanup dữ liệu tạo riêng, không sửa giá seed/đơn người dùng, không gửi SMTP hoặc giao dịch cổng thanh toán thật.
- [ ] Tái dùng script phù hợp: shipper-profile, shipper-task-history, shipper-tasks, shipper-write-safety, shipper-assignment, qr-pin-flow, order-cancellation, order-media-contacts, admin-profile-security. Đọc phạm vi script trước chạy; có file không đồng nghĩa đã PASS.
- [ ] Thêm kiểm thử đúng lỗ hổng: scope/reassignment, history snapshots/count, KPI coverage, concurrent profile/delivery/assign, no PIN in list/audit, operation after unknown result và rollback hết ô.
- [ ] Mỗi chặng: build BE → kiểm tra model/migration nếu có → apply vào DB local → smoke tập trung → build Docker BE → Swagger HTTP200 và xác minh endpoint mới. Không cần chạy toàn bộ smoke suite sau mỗi sửa nhỏ.
- [ ] Chặng cuối chạy hồi quy luồng giặt thường + đơn hủy trả lại đồ + privacy + quản lý Merchant/Admin Shipper hiện có. Test API cũ và mới, role Customer/Merchant/Admin/Shipper và không token.
- [ ] Cập nhật ba tài liệu nguồn để bỏ nhận định đã cũ và thêm hợp đồng FE, `NEXT_IMPLEMENTATION_PLAN.md`, `IMPLEMENTATION_CHECKLIST.md`; chỉ tick mục đã có bằng chứng build/test, ghi hạn chế và breaking changes.
- [ ] Word chỉ sửa phần UC Shipper/nghiệp vụ thực sự thay đổi; không đổi tài liệu thành liệt kê chi tiết kỹ thuật nội bộ, không thêm UC lặp. Cập nhật một bản chính, giữ backup, render/kiểm tra nếu chỉnh Word.
- [ ] Chia commit/PR theo nhánh mỗi chặng, PR sau dựa trên chặng đã merge develop hoặc ghi phụ thuộc rõ; không push thẳng main. Bản kế hoạch không tự tạo branch/commit/push.

## 12. Đối chiếu bao phủ ba file

| Tài liệu / mục | Chặng chịu trách nhiệm |
| --- | --- |
| Dashboard/Profile 1–2, 8.1 | Hiện trạng + 5: giữ API cũ, thêm snapshot/kỳ/KPI/preview. |
| Dashboard/Profile 3, ảnh/địa chỉ bổ sung | 2: task preview/sort/ảnh không N+1, 4: ảnh lịch sử. |
| Dashboard/Profile 4, 5, 7, 8.2 | 6: revision/lookup/validation/profile missing/contact; phần tùy chọn theo mục 10. |
| Dashboard/Profile 6 (giao diện) | Không code FE; chỉ cung cấp route/DTO và hướng dẫn tích hợp. |
| Delivery History 1–5 + ảnh | 1 và 4: nguồn event/snapshot, summary/filter/detail/CSV/validation/count/index. |
| Delivery History 6 | 11: test BE riêng; không coi test mock FE là test BE. |
| Delivery Tasks 1–3 + ảnh/bản đồ | 2: task API, reuse media, metadata đúng trạm; routing/GPS FE theo mục 10. |
| Delivery Tasks 4 và 6 | 3: guard/ledger/lookup/credential/errors; 2: endpoint cấp mã và permission. |
| Delivery Tasks 5 | Giữ simulation ở 3; IoT/session cửa thật theo mục 10. |
| Delivery Tasks 7 | 1/3/5: SLA đúng mốc; chuyển kho/tự nhận việc cần chốt riêng. |
| Delivery Tasks 8 | 11: tests BE + ghi phần tích hợp thiết bị chưa thực hiện. |

Thứ tự triển khai đề nghị: **1 → 2 → 3 → 4 → 5 → 6 → hồi quy cuối**. Ưu tiên nguồn dữ liệu và an toàn thao tác trước khi làm biểu đồ. Hoàn thành ở đây nghĩa là hoàn thành phạm vi BE đã chốt, không bao gồm các mở rộng được phân loại tại mục 10.
