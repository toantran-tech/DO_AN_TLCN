> Bổ sung 14/11/2026: luồng kho/giao lại, task type 3/4, order 14/15 và self-status xem [STORAGE_WORKFLOW_API_REVIEW.md](STORAGE_WORKFLOW_API_REVIEW.md). Với đơn v1 không dùng shortcut nhận trực tiếp ở kho. Chatbot hướng dẫn yêu cầu giao lại locker; không đưa mã/địa chỉ tiệm vào AI. Phần cũ bên dưới cần đọc cùng hợp đồng bổ sung; Docker chính chưa cập nhật cụm này.

# Hợp đồng Backend trợ lý AI cho Customer

Cập nhật 13/11/2026. Phạm vi: UC 1.5.1–1.5.6. BE đã triển khai, migration local đã áp dụng, smoke tích hợp với provider fixture PASS. **Gemini thật đã kết nối và PASS 4 tình huống cơ bản**: hướng dẫn nhận đồ, dịch vụ/giá, hỏi locker chưa cấp vị trí, tìm locker với tọa độ thử nghiệm. Không có FE trong đợt này.

## 1. Authentication và dữ liệu

Base URL local: `http://localhost:5000`. Swagger: `http://localhost:5000/swagger/index.html`.

Tất cả API dưới `/customer/chatbot` cần Bearer token Customer; role khác 403. Đọc/gửi/lookup/xóa hội thoại khác trả 404. Response dùng envelope `meta/data/succeeded/message/errors/statusCode` hiện có, `Cache-Control: no-store`.

Bot chỉ đọc và giải thích. Không có công cụ tạo đơn, thay đổi trạng thái, gọi thanh toán hay duyệt bồi thường. CustomerId do JWT quyết định; không nhận quyền do model/client tự khai.

## 2. Endpoint

| Method | URL | Hành vi |
|---|---|---|
| GET | `/customer/chatbot/conversations?take=20&cursor=...` | Danh sách hội thoại của tôi, mới tạo trước |
| POST | `/customer/chatbot/conversations/first-message` | Tạo hội thoại và lưu tin đầu cùng transaction; không tạo hội thoại rỗng |
| GET | `/customer/chatbot/conversations/{id}/messages?take=30&cursor=...` | Lấy tin gần nhất hoặc tin cũ hơn |
| POST | `/customer/chatbot/conversations/{id}/messages` | Hỏi tiếp trong hội thoại |
| GET | `/customer/chatbot/conversations/{id}/requests/{requestId}` | Tra trạng thái/kết quả sau mất kết nối |
| DELETE | `/customer/chatbot/conversations/{id}` | Ẩn hội thoại khỏi lịch sử; chặn lượt AI đang chạy ghi kết quả lại |

## 3. Request gửi tin

Tạo GUID một lần cho một ý định gửi. Khi timeout/retry giữ nguyên GUID, message, context và endpoint.

```json
{
  "requestId": "<Guid mới>",
  "message": "Có locker nào gần tôi còn ô trống không?",
  "context": {
    "selectedOrderId": null,
    "location": { "latitude": 10.77, "longitude": 106.69 },
    "city": null,
    "district": null
  }
}
```

`context` và từng trường đều tùy chọn. Chỉ gửi `location` sau khi khách đồng ý; cần đủ hai tọa độ hợp lệ. Không có vị trí thì gửi city/district hoặc hỏi bot để nhập khu vực. Không lấy địa chỉ hồ sơ làm vị trí hiện tại tự động. Không truyền PIN, QR, OTP, mật khẩu hoặc token vào `message`.

Tra đơn: gửi `selectedOrderId` khi khách chọn đơn, hoặc viết chính xác mã đơn trong câu hỏi. Quyền sở hữu được kiểm tra trước khi đưa dữ liệu vào ngữ cảnh AI. Nếu hỏi chính sách của đơn, nên truyền selectedOrderId để dùng snapshot vận hành của đơn; nếu không, nguồn chính sách là hiện hành và câu trả lời phải nêu rõ phạm vi đó.

Message 1–2.000 ký tự sau trim. Mặc định 10 lượt/phút và 100 lượt/ngày/Customer, ngày giờ Việt Nam; một lượt đang chạy/hội thoại, tối đa hai lượt/Customer. Cả lượt lỗi đã nhận cũng tính quota. Xóa hội thoại không reset quota.

## 4. Kết quả và retry

```json
{
  "meta": null,
  "data": {
    "conversationId": "<Guid>",
    "requestId": "<Guid>",
    "messageId": "<Guid tin khách>",
    "status": 2,
    "answer": "<Văn bản trợ lý>",
    "errorCode": null,
    "sources": [
      { "code": "locker-catalog", "title": "Locker hoạt động còn ô trống", "version": null, "retrievedAt": "<UTC>" }
    ],
    "cards": [
      { "type": "Locker", "id": "<Guid>", "title": "<Tên locker>", "data": {} }
    ],
    "serverNow": "<UTC>"
  },
  "succeeded": true,
  "message": null,
  "errors": {},
  "statusCode": 200
}
```

- Status: `1 Processing`, `2 Completed`, `3 Failed`.
- Lần gửi thông thường chờ kết quả hoàn chỉnh, không streaming. Lượt xử lý được lưu trước khi gọi AI.
- Retry trùng đang xử lý: HTTP 202, trả IDs và status=1. Retry đã xong: trả kết quả cũ, không gọi Gemini lại.
- Cùng requestId khác nội dung/context: 409 `RequestIdReused`.
- Lượt Failed không tự gọi Gemini lại với cùng requestId. FE lookup để biết kết quả; nếu khách chủ động thử lại sau lỗi, dùng **requestId mới**. Không dùng retry vô hạn.
- HTTP 503 có thể kèm `data` với IDs/status/errorCode; FE cần giữ chúng ngay cả khi thư viện HTTP ném lỗi.
- GET lookup trả 200 với status hiện tại, kể cả Failed; 404 không phải bằng chứng request trước chưa được gửi. Đợi/kiểm tra rồi retry đúng requestId và payload cũ để đảm bảo idempotency.
- Nếu tiến trình chết: lease hết hạn, lần lookup hoặc lần gửi tiếp theo ghi nhận `ChatTurnExpired`, giải phóng khóa logic. Không tự sinh câu trả lời hoặc gọi lại cổng AI âm thầm.
- Với tin đầu chưa biết conversationId do timeout: retry endpoint first-message với requestId/payload cũ; server suy ra cùng hội thoại. Có thể refresh lịch sử để tìm hội thoại vừa tạo.

## 5. Lazy load

Các API danh sách trả trong `data`:

```json
{
  "items": [],
  "hasMore": true,
  "nextCursor": "<cursor có chữ ký>"
}
```

Hội thoại mặc định take=20, giới hạn 1–50. Tin nhắn mặc định 30, giới hạn 1–100. Cursor truyền qua URL encoding; hết hạn sau hai giờ, bị sửa hoặc dùng sai user/resource thì 400 InvalidCursor, cần tải lại danh sách.

Hội thoại sắp theo thứ tự tạo bất biến, không tự nhảy lên đầu khi chat tiếp. Cursor chốt mốc dữ liệu đầu lượt tải; hội thoại mới sau mốc đó được FE chèn từ kết quả gửi hoặc lấy qua refresh. Không dùng nút số trang, không cần total count.

Tin nhắn đợt đầu là những tin gần nhất, **trả cũ đến mới** trong đợt. Cuộn lên tải nextCursor, prepend tin cũ và giữ vị trí theo ID đang đọc. Tin mới sau snapshot tải qua lookup/kết quả send hoặc refresh, không lấy bằng cursor tin cũ.

FE chặn request tải thêm trùng khi đang tải; merge theo ID. Hết dữ liệu dừng tải, lỗi thì giữ dữ liệu và cho thử lại. Backend cũng dùng khóa thứ tự duy nhất để không trùng/bỏ sót do ranh giới phân trang. Bản ghi bị xóa/hết hạn sẽ biến mất theo chủ đích.

Message: `id`, `sequence`, `role` (1 Customer/2 Assistant), `content`, `createdAt`, `requestId`, `status`, `errorCode`, `result`. Status/error trên tin Customer mô tả lượt trả lời; tin Assistant có `result` chứa cards/sources. Không suy role từ việc có/không có status.

## 6. Thẻ dữ liệu và nguồn

Render `answer` bằng text, không `innerHTML`; không thực thi markdown/URL từ mô hình. Thẻ do server sinh, `type` và `id` ánh xạ đến route FE đã biết:

- `Service`: tên/mô tả, UnitPrice, Unit, loại dịch vụ, thời gian xử lý chuẩn. Đây là giá niêm yết, chưa phải giá cuối đơn.
- `Locker`: tên/địa chỉ, DistanceKm nếu có tọa độ, AvailableSlotCount. Khoảng cách đường chim bay; ô trống là tại lúc đọc, không giữ chỗ.
- `Order`: trạng thái/tiền/ETA/deadline/quote-related values và `nextStep` hướng dẫn do server xác định. Không có QR/PIN/điện thoại khách. NextStep không thay thế việc API hành động kiểm tra lại quyền/điều kiện.
- `Policy`: phiên bản điều khoản hiện hành, rules hiện tại; có `orderOperationalSnapshot` nếu khách chọn đơn. Snapshot vận hành không đồng nghĩa văn bản điều khoản hiện hành là bản lịch sử của đơn.

Mọi số tiền quan trọng trên UI ưu tiên data trong thẻ/kết quả API nghiệp vụ, không parse số từ câu trả lời AI. Sources có thời điểm tra cứu; kết quả xem lại lịch sử là dữ liệu ở thời điểm trả lời, không tự cập nhật. Gửi câu hỏi mới để tra lại.

Một lượt gọi tối đa một công cụ dữ liệu trong allowlist và hai request generateContent (chọn công cụ, diễn giải). Khi hỏi nhiều chủ đề, bot có thể yêu cầu hỏi tiếp; bản đầu không điều phối nhiều công cụ trong một lượt.

ETA lấy từ logic đang có: giặt xong/chờ thanh toán, có fallback theo quy tắc khi thiếu mẫu. Không phải ETA lái xe/đồ về locker. Không tự suy đoán nguyên nhân chậm.

## 7. Lỗi chính

| HTTP | Code | FE xử lý |
|---|---|---|
| 400 | InvalidChatMessage / SensitiveChatInput / InvalidChatContext | Hiển thị lý do, cho sửa dữ liệu |
| 400 | InvalidCursor / InvalidPageSize | Tải lại hoặc chỉnh take |
| 401/403 | Xác thực/quyền | Đăng nhập đúng Customer |
| 404 | ConversationNotFound / ChatRequestNotFound | Kiểm tra trạng thái, không lộ tài nguyên người khác |
| 409 | RequestIdReused | Không tái sử dụng ID cho nội dung mới |
| 409 | ConversationBusy / CustomerChatBusy | Chờ lượt trước, lookup |
| 429 | ChatRateLimited | Chờ hạn mức; không tự retry liên tục |
| 503 | AiNotConfigured | BE chưa có key/model hợp lệ |
| 503 | AiConfigurationRejected / AiRateLimited / AiUnavailable / AiTimeout | Thông báo lỗi; lookup trước khi thử câu hỏi mới |
| 503 | InvalidAiResponse / InvalidAiToolCall / ChatDataUnavailable / ChatOrderNotFound | Không hiển thị câu trả lời giả; kiểm tra hoặc chọn lại thông tin |

Thông báo công khai không chứa raw body Gemini hoặc stacktrace. Không ghi message/location/raw provider response vào audit tự động; provider key được gửi trong header, không nằm trên URL.

## 8. Lưu và xóa

Tin nhắn được ẩn khi quá 90 ngày tính từ CreatedAt. Hội thoại không còn hoạt động trong 90 ngày bị ẩn. Worker mỗi 30 phút xóa vật lý tin hết hạn và hội thoại rỗng hết hạn/đã xóa.

DELETE là xóa khỏi lịch sử ngay, không phải hứa xóa vật lý ngay khỏi DB hoặc nhà cung cấp AI. Rows tombstone được giữ đến hạn để bảo toàn quota; hội thoại đã xóa không nhận kết quả muộn. Tọa độ context không lưu trong message/result, chỉ dùng tại lần tra cứu đó. Fingerprint HMAC không chứa tọa độ thô.

Đổi Chatbot:SigningKey/Jwt:SecretKey làm cursor/fingerprint cũ không còn khớp. Khi xoay key phải thông báo refresh và không hứa replay cross-key; không xóa toàn bộ lịch sử chỉ để đổi key.

## 9. Bằng chứng và giới hạn nghiệm thu

- Build host và fixture host PASS; Docker image build PASS, Swagger 5 đường dẫn/6 operations.
- Migration `20261113025410_AddCustomerChatbot` đã áp dụng local; không pending model changes.
- `tests/smoke/chatbot.ps1` PASS với test host riêng: owner/role, first-message replay, cursor, services/lockers/policy/own order, access-code isolation, provider errors, concurrent replay, busy/delete races, stale lease, 90-day purge, quota sau xóa.
- `GeminiContractChecks` PASS: header/body/parser, 403/429/502 mapping và signed cursor; không có gọi mạng Google.
- Container thật thiếu key trả 503 AiNotConfigured, không fallback Mock.
- **Gemini thật đã test:** 4 tình huống trên đều HTTP 200/Completed, tiếng Việt; chọn đúng nguồn và cards dịch vụ/locker, yêu cầu chia sẻ vị trí khi thiếu; model cấu hình hoạt động với key của tài khoản. Đã ẩn hội thoại thử và thu hồi phiên đăng nhập.
- **Còn nghiệm thu rộng với model thật:** tra cứu đơn/ETA và snapshot chính sách trong hội thoại thực tế, prompt-injection ở lời trả lời, câu hỏi nhiều nghĩa và thống kê độ trễ/quota. Chưa coi 4 câu trả lời thành công là toàn bộ ma trận AI đã PASS. Các quyền DB/API đã được kiểm thử độc lập bằng fixture.
- Giới hạn ngày mặc định có code theo UTC+7; smoke đã kiểm tra chặn phút và không reset quota khi xóa, chưa chạy kiểm thử chuyển ngày thực tế.
- Chưa có FE: lazy load/giữ vị trí cuộn là hợp đồng, chưa có kiểm thử trình duyệt FE.
- Không chạy lại toàn bộ các module cũ trong đợt này; thay đổi helper HTTP chỉ thêm mapping 202/503, giữ nguyên mapping hiện có.

Plan và checklist chi tiết: [CHATBOT_AI_IMPLEMENTATION_PLAN.md](CHATBOT_AI_IMPLEMENTATION_PLAN.md). Cấu hình và chạy test xem mục 10–11 bên dưới.

## 10. Cấu hình Gemini

Quản lý/tạo key tại [Google AI Studio API Keys](https://aistudio.google.com/apikey), chọn hoặc import project bạn quản lý. Nếu thiếu quyền, xem [hướng dẫn API key chính thức](https://ai.google.dev/gemini-api/docs/api-key). Quyền dùng model, quota và billing phụ thuộc tài khoản/project.

Trong `DO_AN/.env`:

```dotenv
GEMINI_API_KEY=<khóa riêng, không commit hoặc gửi cho FE>
GEMINI_MODEL=gemini-3.5-flash-lite
```

Model cấu hình đã hoạt động trong 4 live test ngày 13/11/2026; khi Google thay đổi model hoặc dùng project khác, kiểm tra lại quyền truy cập và khả năng function calling. `.env.example` chỉ chứa key rỗng.

Compose map sang `Chatbot__Gemini__ApiKey` và `Chatbot__Gemini__Model`. Dotnet host có loader .env; biến môi trường .NET đã cấu hình sẵn được ưu tiên hơn alias trong .env.

Từ terminal thư mục `DO_AN`, nếu có sửa code:

```powershell
docker compose up -d --build washgo-be
```

Nếu chỉ đổi key/model trong .env thì recreate để nhận cấu hình mới:

```powershell
docker compose up -d --force-recreate washgo-be
```

Không in toàn bộ Docker Env hoặc compose config để kiểm tra key. Thiếu key trả 503 `AiNotConfigured`, không có trả lời AI giả. Lượt Failed cũ không tự chạy lại sau khi thêm key; khách chủ động gửi lại với requestId mới sau khi BE nạp cấu hình.

Mặc định 10 lượt/phút, 100 lượt/ngày/Customer theo UTC+7; có thể chỉnh `Chatbot__PerMinute`, `Chatbot__PerDay`. `Chatbot__TimeoutSeconds` mặc định 30, giới hạn 5–60. Không tự retry Gemini khi chưa biết request trước có được nhận hay tính phí hay không.

Kiểm thử thật qua Swagger: login Customer, Authorize rồi gửi payload mục 3 với requestId mới; kiểm tra answer/sources/cards và lưu conversationId. Thử thêm dịch vụ, locker có/không vị trí, đơn/chính sách, lookup/replay và DELETE. Không coi một HTTP 200 là nghiệm thu toàn bộ chatbot.

Trước khi mở cho người dùng thực tế, cần công bố phù hợp việc dùng Gemini/lưu chat/vị trí tự nguyện trong điều khoản. Đợt này không tự phát hành bản điều khoản mới. Lịch dọn DB không đồng nghĩa Google xóa dữ liệu theo cùng lịch.

Tài liệu tích hợp: [generateContent](https://ai.google.dev/api/generate-content), [models](https://ai.google.dev/gemini-api/docs/models). Xác thực bằng header `x-goog-api-key`, không đặt key trên URL.

## 11. Chạy smoke test không dùng Gemini key

Từ thư mục `washgo-be`, terminal 1:

```powershell
$env:WASHGO_CHATBOT_SMOKE = '1'
dotnet run --project tests/smoke/ChatbotHost/ChatbotHost.csproj
```

Chờ test host sẵn sàng ở `http://localhost:5099`, rồi terminal 2:

```powershell
powershell -ExecutionPolicy Bypass -File tests/smoke/chatbot.ps1
```

Cần migration và seed Customer/Admin có mật khẩu demo chưa đổi. Test host dùng DB dev local theo cấu hình host; script tạo users/order/chat fixture riêng và dọn trong finally. Retention test gọi repository purge hết hạn toàn bảng chat theo chính sách 90 ngày; chỉ chạy trên DB phát triển.

Provider giả lập chỉ nằm trong project test; container Backend không có biến bật Mock chatbot. HTTP contract/parser và cursor được test khi khởi động fixture host, không phát sinh phí Gemini. Tắt terminal 1 sau test; không deploy project test ra ngoài.
