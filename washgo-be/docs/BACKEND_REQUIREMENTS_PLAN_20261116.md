# Plan Backend — thanh toán, lịch sử đơn, thông báo và danh mục

Ngày rà soát: **16/11/2026**. Trạng thái: **đã triển khai source cả 8 cụm**. Cụm thanh toán 1–2 đã được người dùng báo test PASS và build Docker trước đợt này; test mới cho cụm 3–8 đã nối CI nhưng chưa chạy. Migration, Docker và deploy bản mới do người dùng thực hiện.

Bàn giao FE: [payment](PAYMENT_SESSION_RECOVERY_API_REVIEW.md), [timeline/storage/notification](ORDER_EVENTS_STORAGE_NOTIFICATION_API_REVIEW.md), [danh mục dịch vụ](SERVICE_CATEGORIES_API_REVIEW.md). Đối chiếu hiện trạng bên dưới ghi nhận trước khi sửa; kết quả mới xem checklist cuối file.

## 1. Nguồn và phạm vi

Folder yêu cầu: `C:/Users/PC/Desktop/HK1_2027/TLCN/cần làm`, gồm bốn file ngày 15/11/2026:

| File | Phạm vi |
| --- | --- |
| BACKEND_PAYMENT.md | Đọc phiên đang hiệu lực; xác minh/khôi phục thanh toán; capability hạn mức theo tiền thực trả; quote phí |
| BACKEND_ORDER.md | Timeline có sự kiện cấu trúc; thống nhất vòng lưu giữ tại tiệm; mã LK tự sinh |
| BACKEND_NOTIFICATION.md | Admin nhận đủ sự kiện/deadline; inbox/outbox bền vững; privacy và nội dung lưu giữ |
| BACKEND_CATEGORY.md | Danh mục dịch vụ động do Admin quản lý, CRUD/batch/lookup, liên kết và khóa hiệu lực dịch vụ |

Chỉ sửa BE khi được duyệt. Các file có liệt kê thay đổi FE là yêu cầu bàn giao, không phải chỉ thị tự sửa FE trong đợt này. Không thay `.env`, cổng thanh toán, dữ liệu thật hoặc tài khoản người dùng khi lập plan.

API danh mục trong file mới là **quản lý danh mục dịch vụ động**, khác API tổng hợp enum/dropdown từng tạm đề xuất. Không triển khai song song một `/admin/catalogs` chung không có nhu cầu.

## 2. Đối chiếu source hiện tại

| Nhóm | Đã có, tái sử dụng | Khoảng trống xác nhận |
| --- | --- | --- |
| Payment | Create/reuse phiên theo order + purpose, transaction lock, chặn đổi cổng/số điểm, Unknown/legacy cần review; IPN kiểm xác thực; thu dư; sổ điểm và giữ/nhả/tiêu/thưởng/hoàn | Chưa thấy API Customer đọc active-session, gateway query/recovery, capability hạn mức hoặc quote chỉ đọc thống nhất ba purpose |
| Timeline | WashOrderTimeline có FromStatus, Status, actor, timestamp/note; phân công đã ghi lịch sử | DTO chưa xuất FromStatus; chưa có EventType/snapshot phân công/task/cycle/sequence. Không phải mất toàn bộ lịch sử |
| Notification | Lưu inbox trong UoW, push sau commit; Admin active, privacy, messageKey/args, hub và read/delete sync, worker claim | Chưa có retry push bền vững trong publisher; chưa đủ fan-out Admin cho tất cả mốc; claim deadline chưa tách đầy đủ theo vòng/mốc |
| Storage | Cycle tại tiệm Merchant, ShopAddress snapshot, handover/task, giao lại, verify legacy | Handover đang bắt receiptReference và dùng IntakeReceipt làm order.StorageReference; nhánh quá hạn thanh toán có projection khác; chưa có mã LK hệ thống |
| Category | ServiceType, CRUD dịch vụ và proposal, filter đa ServiceTypes, public catalog/guest | Chưa có entity/CategoryId/CRUD/lookup/category active guard |

Nguồn chính đã kiểm tra:

- `PaymentAppService.cs`, `.Sessions.cs`, `.Reconciliation.cs`, PaymentHandler và PaymentContracts.
- `WashOrderTimeline.cs`, `WashOrderResultDtos.cs`, các writer trong OrderAssignment/WashOrder/Storage.
- `NotificationPublisher.cs`, `OrderReminderAppService.cs`, `Notification.cs`.
- `StorageAppService.cs`, `StorageCycleCoordinator.cs`, `OrderStorageCycle.cs`, `StorageCycleDto.cs`; nhánh quá hạn thanh toán nằm tại `Features/PaymentApplication/Services/UnpaidOrderStorageMover.cs`.
- `WashService.cs`, `CustomerCatalogAppService.cs`, `ServiceProposalAppService.cs`, `WashServiceRules.cs`, `EtaPredictor.cs`, `OrderProgressCalculator.cs`, `OrderListEnricher.cs`.

**Phát hiện cần giữ:** ServiceType còn quyết định các bước xử lý trong OrderProgressCalculator và nhóm mẫu ETA trong EtaPredictor. Vì vậy danh mục động không thay thế/xóa ServiceType. Giữ loại xử lý độc lập, snapshot cho đơn mới ở phần bị ảnh hưởng; không đọc category hiện tại để sửa tiến độ đơn cũ.

Không xác nhận trạng thái Render/Neon hoặc khả năng query của tài khoản gateway chỉ từ source. Các chẩn đoán URL/tài khoản trong file yêu cầu là bối cảnh, chưa phải kết quả kiểm tra runtime lần này.

## 3. Thứ tự và điểm bàn giao

| Cụm | Công việc | Phụ thuộc | Điểm dừng để review/PR |
| --- | --- | --- | --- |
| 1 | Payment read model, quote, capability và validation hạn mức | Payment/loyalty hiện có | FE đọc đúng phiên, không thử cổng bằng POST; tiền nhỏ bị chặn trước giữ điểm |
| 2 | Query gateway và khôi phục phiên an toàn | Cụm 1, đặc tả query tài khoản gateway | Có đường xác minh/thử lại, không tự đóng chỉ vì hết 15 phút |
| 3 | Event/timeline có cấu trúc và thứ tự | Writer đơn/phân công/payment hiện có | Admin phân biệt trạng thái với sự kiện; không làm sai progress/ETA |
| 4 | Mã LK và projection tiệm/vòng lưu giữ | Cụm 3 cho sự kiện; storage hiện có | Mã duy nhất, đúng vòng, không nhập mã lưu giữ thủ công |
| 5 | Admin notification đầy đủ + outbox/deadline | Cụm 3–4, sự kiện payment cụm 2 | Inbox đủ mốc; rollback/replay/worker/reconnect không tạo bản ghi trùng |
| 6 | Category schema/backfill và CRUD/lookup | Chốt chuẩn danh mục; event/outbox dùng lại khi có thông báo | DB và Admin CRUD ổn, dữ liệu cũ không bị phân loại sai |
| 7 | Category tích hợp dịch vụ/proposal/order/guest/chatbot | Cụm 6 | Khóa danh mục thật sự chặn đặt mới, đơn cũ tiếp tục chạy |
| 8 | Category batch, hồi quy và bàn giao tổng | Cụm 5–7 | Batch từng mục khôi phục được, đủ test/contract/tài liệu |

Làm tuần tự theo cụm, có thể tách nhiều PR nhỏ nếu một cụm lớn. Không gom cả bốn module thành một commit. Mỗi cụm cập nhật checklist và API review; chỉ đánh dấu nghiệm thu khi có kết quả test/deploy tương ứng.

## 4. Cụm 1 — đọc phiên, quote và hạn mức thanh toán

### 4.1. Active session

Đề xuất `GET /payment/orders/{orderId}/active-session?purpose=1|2|3`:

- CustomerOnly; xác thực sở hữu ở service, không nhận customerId. `Cache-Control: no-store`.
- GET thuần đọc: không tạo phiên, gọi query/create gateway, giữ/nhả điểm, đổi trạng thái hoặc gửi thông báo.
- Phân loại theo cùng logic session hiện hành, gồm Pending legacy trùng, Creating, Ready, Unknown, tiền chưa áp dụng cần review; không chỉ lọc IsActiveSession rồi bỏ quên legacy.
- Không có phiên khóa: data=null. Nghĩa vụ đã trả đủ thể hiện qua quote/capability, không suy data=null là được trả tiếp.
- Có một phiên: orderId, purpose, paymentMethod, amount, pointsToRedeem, transactionStatus, sessionState, expiresAt nếu biết, reasonCode, retryAfterSeconds, canContinue/canVerify/canCreateReplacement.
- Nhiều phiên legacy: không chọn ngẫu nhiên; paymentMethod có thể null, requiresReconciliation=true, canContinue=false. Chỉ rõ nhóm capability là của phiên được xác định an toàn.
- Không trả URL đã ký, secret, raw IPN hoặc giao dịch ngoài quyền; tiếp tục cùng URL qua POST reuse hiện có.
- 409 PaymentMethodConflict/PaymentPointsConflict bổ sung metadata an toàn của phiên sau khi kiểm quyền. POST vẫn kiểm lại dưới khóa nếu trạng thái đổi sau GET.

### 4.2. Quote và capability

Đề xuất quote chung `GET /payment/orders/{orderId}/quote?purpose=...&pointsToRedeem=...`, giữ quote loyalty cũ tương thích:

- Dùng chung bộ tính nghĩa vụ hiện có: tiền giặt sau voucher/điểm; phí hủy còn thiếu; phí kho/quá hạn tính lại theo vòng và khoản đã trả. GET không ghi số dư/giữ điểm/cộng phí DB.
- Response có grossDue, pointsRequested/applied, cashAmount, policy/version hoặc quote fingerprint, computedAt, capability từng paymentMethod và reasonCode; số dự kiến không phải quyền thu tiền.
- Chính sách hạn mức là cấu hình server theo môi trường/phương thức/kênh, không để FE ghi đè. Tách nguồn giới hạn gateway với mức phòng tránh riêng WashGo.
- Mức VNPay 10.000đ trong tài liệu là chính sách FE/WashGo, không khẳng định mọi ngân hàng có ngưỡng đó. MoMo wallet/thẻ có thể khác hạn mức; cần đối chiếu cấu hình và tài liệu merchant trước khi bật giá trị live.
- Dưới mức mọi cổng hỗ trợ: không miễn/tăng phí, không giả Paid; trả capability chặn và hướng dẫn liên hệ WashGo. Không tự thêm phương thức tiền mặt để lách hạn mức.
- Kiểm giới hạn bằng **cashAmount thực cần thu**, trước tạo transaction/giữ điểm/gọi cổng. Session cũ đang khóa vẫn ưu tiên trạng thái xác minh, không đề xuất đổi cổng rồi tạo khoản thu mới.
- Với retry phiên có sẵn, tách kiểm tra tiếp tục phiên và tạo mới: thay cấu hình không tự giải phóng reference/điểm của phiên cũ. Phiên cũ không phù hợp đi qua recovery.
- Khi quote cũ khác tiền/điểm/nghĩa vụ tại POST, trả QuoteChanged + quote mới để FE xác nhận. Triển khai additive trường xác nhận trong thời gian tương thích FE, không âm thầm đổi số tiền.

Nghiệm thu: JWT/ownership, ba purpose độc lập, legacy trùng, phương thức phí khác tiền giặt, giới hạn biên và điểm làm thay cashAmount, không ghi DB khi GET, amount đổi giữa quote và write, không giữ điểm cho request bị chặn.

## 5. Cụm 2 — query và khôi phục thanh toán

Đề xuất POST `/payment/orders/{orderId}/verify-session` và hành động tạo phiên thay thế có xác nhận quote; tên cuối cùng chốt trong API review.

1. Customer sở hữu đơn, purpose/reference lấy từ BE; operationId gắn payload, rate limit/cooldown và quyền tra kết quả operation của chính khách.
2. Đọc snapshot phiên; ghi attempt bền vững nếu cần rồi commit. Gọi query gateway ngoài transaction DB, timeout hữu hạn; không in secret/chữ ký/payload nhạy cảm.
3. Adapter query riêng VNPay/MoMo theo hợp đồng chính thức của merchant. Chuẩn hóa kết quả VerifiedSuccess / VerifiedTerminalFailure / Pending / Unknown; kiểm chữ ký, merchant, reference và amount. Không biến responseCode giao tiếp thành kết luận giao dịch thất bại.
4. Khóa transaction → order → wallet theo code hiện tại; kiểm lại reference/state/version sau cuộc gọi. Thành công dùng chung đường ApplyGatewayResult của IPN để chỉ ghi Paid/thu phí/điểm một lần.
5. Chỉ đóng phiên và nhả điểm nếu kết quả cuối được xác thực và hợp đồng cổng cho phép kết luận phiên không còn thu được. NotFound đơn lẻ, lỗi mạng, chữ ký sai, quá 15 phút local không đủ.
6. Sau đóng, tính quote mới; khác nghĩa vụ/điểm/cổng phải xác nhận lại. Cùng operation/retry/hai tab chỉ được tạo tối đa một phiên thay thế. Không đổi reference hoặc URL đã ký trên giao dịch cũ.
7. IPN cũ đến muộn sau thay thế: giữ bằng chứng thu tiền, phân loại áp dụng/thu dư theo cơ chế hiện có; không bỏ receipt hoặc thưởng điểm lần hai.
8. Worker retry có lịch, backoff/jitter, lease và số lần thử; cảnh báo Admin/escalation khi không xác minh được. Tránh polling vô hạn dồn request/khóa DB. GET chỉ phản ánh trạng thái, không biến thành query cổng.
9. Cấu hình ReturnUrl FE và IPN BE hướng dẫn trong tài liệu deploy; không sửa secrets/tunnel ở đợt code nếu chưa được yêu cầu. Đổi config không làm URL cũ hợp lệ trở lại.

Điểm chặn nghiệm thu live: có endpoint query, credentials và quy tắc trạng thái cuối của từng gateway/merchant. Có thể viết adapter và fake tests trước; nếu cổng không cung cấp bằng chứng cuối thì giữ review, không đặt một timeout giả để mở khóa tiền.

Nghiệm thu: mất mạng trước/sau trả tiền, IPN chậm, query sai chữ ký/pending/timeout/notfound, recovery trùng operation, hai tab tạo mới, fee/point đổi, callback muộn, đối soát/hoàn dư và kiểm số dư điểm. Fake tests không thay bằng chứng sandbox.

## 6. Cụm 3 — một nguồn sự kiện cho timeline và thông báo

- Bổ sung metadata vào timeline hiện có, không thêm trạng thái phân công vào WashOrderStatus.
- Đề xuất eventId dùng ID timeline; EventType là mã ổn định, FromStatus/Status giữ trước/sau; Sequence tăng theo từng đơn dưới khóa order, unique `(OrderId, Sequence)` cho dữ liệu mới. Không suy sequence từ timestamp cùng giây hoặc số Revision dự đoán trước trigger.
- Payload có cấu trúc: snapshot Merchant được gán, manual/auto; Shipper cũ/mới, taskId/taskType, cycleId/requestId; payment purpose/reference nội bộ tối thiểu và số thực thu. Actor thực hiện khác người được phân công.
- Writer dùng helper chung trong cùng UoW với thao tác thành công; operation replay lấy event đã lưu, không ghi thêm dòng/audit/notification. Tạo đơn gán ID xong mới tạo sự kiện.
- Rà mọi writer: tạo/gửi/nhận/hủy, Merchant assignment đơn/batch, Shipper assign/reassign/cancel, từng bước giặt, IPN/query/phí, worker/legacy recovery, các chặng kho/giao lại. Returned → Completed trong một UoW vẫn lưu đủ sự kiện thật.
- Progress/ETA chỉ lấy các mốc chuyển trạng thái phù hợp; assignment và thông báo phí cùng Status không được dời mốc đã đạt. Rà cả nhóm mẫu lịch sử ETA.
- DTO thêm FromStatus/event/sequence/snapshot theo role; giữ Timestamp/Note cũ để không phá FE. messageKey/args whitelist; Admin không cần regex Note để dựng nhãn.
- Legacy: EventType=Legacy/Unknown, giữ note theo quyền; không parse tiếng Việt rồi giả tạo snapshot. Chỉ backfill khi task/audit/operation có bằng chứng đủ, ghi basis nguồn.
- Privacy áp trước serialize: Customer không thấy nhân sự/địa chỉ tiệm/PIN/QR/token; không đẩy toàn bộ order DTO vào event args.

Nghiệm thu: assignment không đổi Status vẫn phân biệt được, sequence cùng giây, nhiều vòng/task, rollback/replay/batch không nhân đôi, progress/ETA không lùi, legacy và cross-role an toàn.

## 7. Cụm 4 — mã lưu giữ tại tiệm và projection thống nhất

- Kho vẫn là tiệm Merchant; không có registry/CRUD kho mới.
- Thêm StorageReference hệ thống trên cycle, unique khi khác null và không tái sử dụng. Giữ IntakeReceipt/biên nhận nhập cũ riêng; handover không còn bắt nhập mã lưu giữ.
- Generator dùng giờ BE UTC chuyển UTC+7, định dạng `LKyyyyMMddHHmmssNNNN`, NNNN sinh ngẫu nhiên an toàn có cả 0000. Chỉ cấp lần đầu cycle thực sự Stored.
- Handover và nhánh quá hạn thanh toán tại tiệm dùng chung bộ cấp mã. Nhánh sau có basis SystemUnpaidAtShop, không giả task hoặc biên nhận bàn giao.
- Collision: unique DB là chốt, retry hữu hạn qua savepoint/transaction phù hợp PostgreSQL; rollback toàn bộ nghiệp vụ nếu không cấp được. Không catch unique violation rồi tiếp tục trên transaction đã aborted.
- Replay trả mã cũ; vòng mới mã mới. Assign/collect/quote/GET chưa Stored không tạo mã hoặc báo đã nhập tiệm.
- Order.StorageLocation/StorageReference là projection của cycle tương ứng. DTO cho biết active/latest/history và vị trí thực tế; khi đã xuất tiệm không dùng mã/địa chỉ còn sót để nói hàng vẫn ở đó.
- Địa chỉ/tên tiệm snapshot từ đúng Merchant; thiếu dữ liệu trả capability/reason, không mượn địa chỉ locker/tiệm khác. Đổi Merchant không rewrite vòng lịch sử.
- Legacy giữ nguyên biên nhận, basis và thời điểm xác minh/cấp mã nếu có; không giả giờ cấp lịch sử. Customer vẫn không được thấy địa chỉ/mã nội bộ.
- Đổi payload handover additive: receiptReference vật lý tùy chọn; client gửi mã LK không được ghi đè mã hệ thống. Giữ task/PIN/QR/version/actor và điều kiện bàn giao.

Nghiệm thu: cả hai nhánh vào tiệm, thiếu địa chỉ, collision cùng giây, hai request cùng vòng, rollback/retry, hai vòng cùng đơn, legacy và order/cycle/notification cùng mã.

## 8. Cụm 5 — notification Admin và delivery bền vững

### 8.1. Ma trận bổ sung

- Tạo đơn; Merchant assignment manual/auto/batch; Shipper assign/reassign/cancel; collect; từng bước xử lý; AwaitingPayment.
- Tiền giặt thành công và các khoản phí thành công theo purpose, kể cả Status không đổi; hủy đơn đúng actor, nhận lại đồ hủy, hoàn tiền/điểm nếu nghiệp vụ phát sinh.
- Chuyển tiệm, nhập tiệm, lưu giữ do quá hạn trả tiền tại tiệm; yêu cầu/hủy giao lại, phân công/xuất tiệm/trả locker/khách nhận theo đúng cycle/request/task.
- Giữ/reuse các notification Admin đã có cho Deposited, PickedUp, refund/incident/rating và SLA; không tạo một bản từ helper cũ và một bản từ event mới cho cùng mốc.

### 8.2. Inbox/outbox

- Event/timeline, inbox và outbox ghi cùng transaction; recipient Admin active được chốt theo nghiệp vụ, gồm Admin tự thao tác. Gộp recipient trùng ID, unique `(EventId, RecipientUserId)` cho inbox mới.
- Outbox liên kết NotificationId; worker claim bằng lease/row lock phù hợp, có attempts/nextAttemptAt/error/dead-letter và quan sát backlog. Không giữ khóa DB trong lúc gửi SignalR.
- Giao realtime là **at-least-once**: crash sau gửi trước đánh dấu có thể push lại cùng notificationId. FE dedupe ID/eventId; không hứa exactly-once SignalR. Không tạo inbox mới khi retry push.
- Giữ `/hubs/notifications`, `notification`, `notificationState` hiện có; metadata thêm là additive. Reconnect tải bù từ inbox phân trang/cursor, không phát lại nghiệp vụ. Read/delete của user có hiệu lực; worker không tái tạo notification đã xóa hoặc đẩy payload đã lỗi quyền.
- Kiểm tra role/active và sanitize trước lưu và trước delivery phù hợp, whitelist args. Customer chỉ thấy hướng dẫn yêu cầu giao lại locker, không địa chỉ/nhân sự/mã biên nhận/PIN/QR.
- Sửa template gốc và nội dung Merchant đang hướng dẫn nhận tại kho; giữ lớp privacy cho dữ liệu cũ. Không dùng privacy để che một template nghiệp vụ sai rồi bỏ nguyên bản sai.
- Logging/correlation theo eventId/notificationId, metric pending/retry/failure và cảnh báo backlog. SignalR lỗi không trả thất bại cho nghiệp vụ tiền đã commit.
- Worker phải có tiến trình chạy; Render Free ngủ thì outbox vẫn bền vững nhưng không bảo đảm realtime khi process dừng. Tài liệu vận hành ghi đúng giới hạn, không hứa retry chạy khi host không hoạt động.

### 8.3. Deadline

- DepositExpired cho Admin; PaymentOverdue/đã lưu giữ tại tiệm đúng kết quả; PickupOverdue lần đầu, khác StorageTransferDue sau đó.
- Dedupe theo đơn + loại hạn + deadline/cycle/version; rà các claim hiện tại chỉ dùng orderId để không mất thông báo vòng sau.
- Khi claim, khóa và kiểm lại điều kiện đang hiệu lực, deadline/policy của đơn; nhiều worker/restart không ghi inbox trùng. Không đoán deadline của legacy thiếu bằng chứng.
- Lưu dueAt và detectedAt khác nhau. Nếu chuyển kho thất bại thì cảnh báo điều phối, không gửi “đã nhập tiệm”.

Nghiệm thu: đầy đủ ma trận online/offline, nhiều Admin active/inactive, rollback, replay, callback/worker đồng thời, reconnect/read/delete, lỗi SignalR và crash lease, nhiều vòng/deadline đổi, không lộ dữ liệu nội bộ.

## 9. Cụm 6–8 — danh mục dịch vụ động

### 9.1. Schema và chuyển đổi

- Danh mục một cấp, mỗi dịch vụ một CategoryId. Entity có UUID, code chuẩn hóa/unique không tái sử dụng, name/description, active, sortOrder, version/audit và soft delete theo quy ước dự án.
- Dùng alias rõ ràng nếu đặt entity ServiceCategory vì đã có enum trùng tên. Không dùng enum ServiceCategory.Combo=4 để seed BlanketWash.
- Tạo đúng 5 category seed theo WashServiceType đang dùng, ID cố định ở migration/config BE; backfill bằng bảng ánh xạ tường minh. Preflight thống kê loại lạ và proposal lỗi; dữ liệu không hợp lệ chặn bước siết FK, xuất báo cáo đối chiếu, không gán ngẫu nhiên.
- Migration chia expand → backfill có kiểm soát → validate/siết FK. Chưa drop ServiceType: đó là loại xử lý ảnh hưởng ETA/progress, không phải khóa danh mục động. Danh mục thứ 6 chọn loại xử lý hiện có cho dịch vụ; không cần thêm enum loại xử lý chỉ để thêm category.
- Giữ raw ProposedData/CurrentData cũ cho audit; thêm schema version/mapping đọc legacy. Chỉ áp dụng CategoryId khi chứng minh được mapping; proposal lỗi trả lỗi rõ để chỉnh, không giả ID rồi duyệt.
- Category snapshot mới cho order item nếu hiển thị lịch sử; loại xử lý/giá/duration của đơn không lấy lại từ category vừa đổi. Không rewrite đơn/biên lai/finance cũ hoặc giả snapshot quá khứ bằng tên mới.
- Phần ETA/progress hiện đọc ServiceType từ dịch vụ phải được rà khi tách field: đơn mới có căn cứ loại xử lý đã chốt; legacy đánh dấu nguồn fallback, không dùng rename/di chuyển category để đổi tiến độ.

### 9.2. Admin và lookup

Theo hợp đồng đề xuất trong BACKEND_CATEGORY.md:

- POST `/admin/service-categories/filter`; GET/POST `/admin/service-categories`; PUT `/{id}`, PUT `/{id}/status`; action xóa dùng guard chuẩn của dự án, ưu tiên POST `/{id}/delete` nếu DELETE body không phù hợp.
- Validation: code 2–30 Latin/số/-/_, name trim 1–100, description ≤500, sortOrder 0–100000; chốt normalization Unicode/case/whitespace và tên trùng nhất quán. Lưu plain text, không render HTML đầu vào.
- expectedVersion, operationId, audit trước/sau, capability/dependencyCounts/reasonCode; GET không ghi DB. Không đổi code nếu có ràng buộc đã công bố mà không kiểm tra version/dependency.
- Soft delete chặn service chưa xóa/proposal pending tham chiếu; không cascade. Reference check và category attach cùng khóa/transaction; DB FK ngăn tham chiếu mất, nhưng không tự bảo đảm active/soft-delete nên vẫn cần guard.
- Lookup Admin có lựa chọn inactive theo ngữ cảnh; Merchant chỉ đọc/chọn active khi đề xuất; Customer/guest chỉ active/public và count từ dịch vụ thực sự bán. Có keyword/phân trang, thứ tự sortOrder/name/id.

### 9.3. Hiệu lực và tích hợp

- Bộ điều kiện chung: service active, chưa xóa, approved; category active/chưa xóa; thêm điều kiện giá/scope hiện có. Trả effectiveIsActive và blockedReason cho staff; không ghi đè cờ khóa riêng của service.
- Khóa category chặn catalog/list/detail/lookup/featured/suggestion/chatbot tools, quote/voucher/create-order bằng direct ID; đơn đã chốt tiếp tục giặt/thu tiền/hoàn tiền theo snapshot.
- Khóa lại các category liên quan theo thứ tự ID ổn định trước khi chốt dịch vụ mới, đổi CategoryId, duyệt proposal hoặc tạo đơn; kiểm lại hiệu lực trong transaction. Rà cùng thứ tự với locker/order/promotion locks hiện có để tránh deadlock.
- Quote không phải giấy phép đặt; cache cũ không vượt kiểm write. Rename/inactive invalidate read model sau commit nếu có cache.
- Duyệt proposal kiểm lại category, version/payload; không tự approve khi mở lại category. Sửa service dưới category khóa được giữ thông tin quản trị nhưng không thể mở bán vượt khóa cha.
- CategoryIds là UUID multi-select, giới hạn số ID, OR trong nhóm và AND với keyword/giá. Không Number(uuid) hoặc UUID All giả. Hai giá trị loại cũ và category mới không cùng nghĩa: giữ ServiceTypes để lọc **loại xử lý** trong thời gian tương thích, không tự đổi tất cả URL serviceType sang categoryId vì dịch vụ có thể chuyển category.
- Công bố rõ mapping 5 seed category cho URL cũ nếu FE cần; không hardcode UUID ở FE. Nếu nhận cả filter cũ và mới, chốt giao tập hoặc lỗi rõ, không âm thầm bỏ một tiêu chí.
- Rà DTO/mappers, Admin service CRUD/filter/batch, Merchant proposal JSON/review/batch, Customer/guest/Home/order quote, chatbot context và ETA/progress/receipts. FE tự tích hợp sau bàn giao.

### 9.4. Batch và operation recovery

- Đề xuất POST `/admin/service-categories/batch/delete` và `/batch/status`, tối đa 100 items `{id, expectedVersion}`; reason, operationId gắn toàn payload.
- Kết quả từng item: success/no-op/in-use/stale/duplicate, dependency counts, version đúng quyền. Không gọi một batch một UoW rồi báo partial khi thực tế rollback hết.
- Parent operation lưu tiến độ bền vững; child key suy ra ổn định từ batch+item, payload fingerprint. Retry sau crash chạy phần chưa có kết quả, không chạy lại phần đã commit. Kết quả hoàn tất được đóng băng; payload khác cùng key trả conflict.
- GET operation của Admin để khôi phục sau timeout; dữ liệu chưa có kết quả không được hiểu là chắc chắn chưa chạy. Retry các item lỗi bằng operation mới phải là quyết định rõ.
- Impact chỉ đọc trước xác nhận, write kiểm lại số liệu. No-op không gửi audit/event thay đổi giả; batch chỉ phát sự kiện cho item đã commit.

Nghiệm thu: category thứ 6 không deploy enum/FE; backfill đủ/đúng và proposal đọc legacy; CRUD/unique/guard/permission; khóa/mở không bật service khóa riêng; soft-delete vs attach/approve/create đồng thời; multi-filter/guest/privacy; batch partial/replay/crash; progress/ETA/điểm/thanh toán không đổi vì category.

## 10. Quy ước thực hiện và kiểm thử

- Contracts tách Commands/Queries/Results/Services; interface và DTO riêng; Application dùng service nghiệp vụ chung, Domain giữ quy tắc, EF giữ query/lock/index. Dùng using/alias, không nhét namespace dài trong hàm.
- Không copy payment settle/điểm sang recovery; không tạo hai bộ category active rule; không tạo timeline và notification event độc lập cho cùng thao tác.
- Migration additive theo từng cụm, Designer/snapshot đồng bộ, preflight/backfill có log và kiểm count; Down không xóa bằng chứng tài chính/lịch sử đã phát sinh. Backup trước khi người dùng áp DB.
- Test theo cụm, nối Run-ApiSmoke/CI trước bàn giao: unit công thức/state mapping; HTTP contract/quyền; PostgreSQL migration/unique/concurrency; outbox/SignalR integration; sandbox query khi có cấu hình.
- Các test hiện có payment-assignment-concurrency, loyalty-points, actual-weight, storage lifecycle, guest/privacy phải tiếp tục chạy; không sửa kỳ vọng để bỏ qua lỗi route/500 hoặc đổi unknown thành failed cho xanh CI.
- Theo cách làm đã thống nhất: cập nhật source và test, người dùng chạy test/build Docker; không tự thao tác live gateway/DB/secrets khi chưa được yêu cầu. Mỗi kết quả ghi rõ static/build/test/migration/deploy, không đánh dấu theo suy đoán.

## 11. Những điểm cần xác nhận khi đến cụm tương ứng

1. Hạn mức phòng tránh WashGo và capability từng kênh/merchant; mặc định chưa xác thực không được quảng bá là bảo đảm thanh toán thành công. Khoản dưới mọi cổng hỗ trợ hiện trả hướng dẫn liên hệ, không tự thay số tiền.
2. Quyền/query/status cuối của VNPay/MoMo sandbox đang dùng; thời gian/backoff/cooldown/escalation chọn theo hạn mức cổng và config vận hành, không tự đặt timeout làm bằng chứng không thu tiền.
3. Category: name VI duy nhất hay cần displayNames VI/EN ngay; đề xuất bản đầu name VI động + FE hiển thị tên dữ liệu, không tạo nhãn EN giả. Xác nhận tên không phân biệt hoa/thường sau chuẩn hóa trước khi bật unique.
4. Window tương thích API ServiceTypes/CategoryIds và handover receiptReference; đề xuất additive ít nhất một đợt bàn giao, có hướng dẫn deprecation rồi mới siết yêu cầu mới ở API ghi.

Các lựa chọn kỹ thuật như tên enum, route cuối và outbox schema sẽ do BE chốt trong API review trước khi FE tích hợp; không cần hỏi lại người dùng về mọi tên class. Quy tắc tiền, bỏ/miễn phí hoặc mở quyền dữ liệu không được tự suy từ implementation.

## 12. Checklist đợt này

- [x] Đọc đủ 4 file trong folder mới.
- [x] Đối chiếu source và phân biệt phần đã có/chưa có; xác nhận ảnh hưởng ServiceType tới ETA/progress.
- [x] Lập thứ tự 8 cụm, dependency, hợp đồng đề xuất, migration/concurrency/privacy và nghiệm thu.
- [x] Người dùng duyệt triển khai phần thanh toán để bàn giao FE, không chạy test/Docker.
- [x] Cụm 1–2: GET session/quote/capabilities, metadata, hạn mức tiền thực trả và xác nhận quote mới trước thay phiên.
- [x] Durable operation, query VNPay/MoMo ngoài transaction, worker lease/retry/escalation và chung settle/IPN.
- [x] Migration AddPaymentVerification + Designer/snapshot, viết fake HTTP/smoke recovery và nối CI, tài liệu FE/config.
- [x] Kiểm tra biên dịch source; chưa chạy bộ test mới.
- [ ] Người dùng chạy CI/test, backup/áp migration/build/deploy và xác minh sandbox merchant.
- [x] Cụm 3: eventType/fromStatus/sequence/basis và snapshot phân công/task/cycle/payment; privacy; ETA/progress bỏ event không chuyển trạng thái.
- [x] Cụm 4: mã LK cấp nguyên tử khi Stored, UTC+7/unique/retry, biên nhận tùy chọn, projection thống nhất và lịch sử giữ nguyên sau xuất tiệm.
- [x] Cụm 5: Admin event fanout, deadline claim theo vòng/hạn/revision, inbox/outbox cùng transaction, worker lease/retry/dead-letter và API summary.
- [x] Cụm 6: danh mục động, FK/backfill 5 nhóm đúng WashServiceType, preflight dừng dữ liệu legacy không ánh xạ được; giữ raw proposal JSON và snapshot cũ null.
- [x] Cụm 7: Admin CRUD/status/soft-delete có version/operation/audit/dependency guard, batch từng item và phục hồi operation; lookup đúng role.
- [x] Cụm 8: CategoryIds multi-filter, hiệu lực cha/con trên catalog/direct-order/proposal; DTO staff và chatbot; snapshot category/processing type cho đơn mới.
- [x] Viết WorkflowMetadata, service-categories-workflow và mở rộng storage-lifecycle; nối Run-ApiSmoke. Có migration/Designer/model snapshot mới và tài liệu FE.
- [ ] Người dùng chạy bộ regression mới, kiểm tra cạnh tranh/rollback và migration trên DB cô lập; chưa ghi nhận PASS cho cụm 3–8.
- [ ] Backup DB đích, áp migration mới, build Docker/deploy, kiểm chứng worker/SignalR và nghiệp vụ trên staging.

### Chi tiết triển khai khác đề xuất ban đầu

- Dùng khóa transaction chung `service-catalog` theo cơ chế đang có để tuần tự hóa attach/approve/delete/create, thay cho nhiều category lock rời; đổi lại các write catalog bị tuần tự hóa.
- Name VI duy nhất sau NFC/case/space; code bất biến. Client cũ thiếu categoryId còn fallback nhóm seed khi tạo và giữ category hiện tại khi sửa.
- Outbox at-least-once, không có xác nhận client đã nhận. FE dedupe bằng notification.id và tải lại inbox khi reconnect; chưa có dịch vụ alert bên ngoài hoặc API replay dead-letter.
- Kiểm tra biên dịch chỉ xác nhận source hợp lệ; không thay thế kiểm thử HTTP, migration hay sandbox thực tế.
