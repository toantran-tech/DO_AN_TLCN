# Kế hoạch triển khai Backend trợ lý AI WashGo

Ngày lập và chốt lựa chọn: 12/11/2026. Cập nhật 13/11/2026: **BE cốt lõi, migration local và fixture PASS; Gemini thật đã kết nối và PASS 4 ca cơ bản**, còn nghiệm thu rộng. Hợp đồng thực tế, cấu hình Gemini và chạy test: [CHATBOT_API_REVIEW.md](CHATBOT_API_REVIEW.md).

Nguồn nghiệp vụ: mục 1.5 và các use case 1.5.1–1.5.6 trong `DO_AN/Đặc tả usecase.docx`, bao gồm cập nhật tạo hội thoại mới và lazy load. Người dùng chọn Gemini và đồng ý các phương án còn lại ngày 12/11/2026. Các checkbox triển khai chỉ được đánh dấu sau khi có kết quả thực tế.

## 1. Mục tiêu và phạm vi

Xây Backend cho Customer trò chuyện với trợ lý AI, dùng dữ liệu được cấp quyền của WashGo. Chatbot hướng dẫn và tra cứu; các thao tác nghiệp vụ vẫn đi qua API hiện có và bước xác nhận của khách.

| Use case | Kết quả cần có | Nguồn dữ liệu |
|---|---|---|
| 1.5.1 Hướng dẫn sử dụng | Giải thích đăng ký, đặt đơn, giao nhận, thanh toán, hủy đơn, khiếu nại | Nội dung hướng dẫn được nhóm duyệt |
| 1.5.2 Dịch vụ và giá | Tìm dịch vụ đang bán, giá/đơn vị, mô tả, giờ xử lý chuẩn, liên kết chi tiết | Danh mục Customer hiện có |
| 1.5.3 Tra cứu đơn | Trạng thái, giá đã chốt hoặc tạm tính, thanh toán, tiến độ, ETA, hành động tiếp theo | Đơn thuộc Customer và kết quả BE hiện có |
| 1.5.4 Chính sách | Giải thích chính sách, phí, hoàn tiền/bồi thường; phân biệt chính sách hiện hành và chính sách chốt trên đơn | Điều khoản, quy tắc nghiệp vụ, kết quả tính phí được phép xem |
| 1.5.5 Locker gần đây | Xin vị trí, tìm locker khả dụng, khoảng cách ước tính, số ô trống | Truy vấn locker Customer hiện có |
| 1.5.6 Hội thoại | Tạo chat mới, xem/chat tiếp, xóa; lazy load lịch sử và tin nhắn | Hai bảng hội thoại/tin nhắn mới |

Không thuộc đợt này: FE, nhận diện ảnh/giọng nói, AI tự tạo/hủy đơn, tự thanh toán, tự duyệt khiếu nại, tra cứu chéo khách, chatbot cho nhân viên, IoT, dẫn đường/giao thông, vector database hoặc huấn luyện mô hình riêng. Streaming không bắt buộc cho bản đầu.

## 2. Kết quả kiểm tra source và phần tái sử dụng

Chưa tìm thấy module hội thoại hoặc tích hợp mô hình AI trong source BE được kiểm tra khi lập plan.

| Thành phần có sẵn | Cách sử dụng |
|---|---|
| `CustomerCatalogAppService.GetServicesAsync` | Tái sử dụng lọc nhiều loại dịch vụ/khoảng giá, tên, giá và phân trang; không viết bảng giá mới |
| `CustomerCatalogAppService.GetLockersAsync` | Tái sử dụng điều kiện locker hoạt động, Merchant hoạt động, có ô trống; Haversine và sắp xếp theo khoảng cách khi có tọa độ |
| `IWashOrderAppService.GetByIdAsync`, `GetMyOrdersAsync` | Tái sử dụng kiểm tra sở hữu và dữ liệu nghiệp vụ; thêm bước chuyển sang DTO tối thiểu trước khi cung cấp cho AI |
| `EtaPredictor` | Đọc kết quả dự kiến giặt xong/chờ thanh toán; không mô tả thành thời gian Shipper trả đồ |
| `GetCancellationQuoteAsync` và dữ liệu tài chính của đơn | Lấy giá trị server tính sẵn; chỉ gọi đường đọc, không gọi cancel/payment-create/refund-write |
| `IPolicyAppService`, chính sách được chốt theo đơn | Dùng nội dung được phép công khai cho Customer, không đưa toàn bộ cấu hình nội bộ cho AI |
| `CustomerOnly`, `CurrentUser`, ABP UoW, response/error conventions | Tái sử dụng xác thực, quyền, transaction và hình thức trả lỗi |

Không truyền trực tiếp `WashOrderDetailDto` hoặc entity sang AI: các đối tượng này có thể chứa dữ liệu không cần thiết, mã mở tủ hoặc thông tin cá nhân. Adapter phải chọn rõ từng trường.

## 3. Luồng xử lý một lượt chat

1. Xác thực Customer và kiểm tra quyền sở hữu hội thoại; không nhận CustomerId do client tự khai.
2. Kiểm tra message, requestId, ngữ cảnh đơn/vị trí và hạn mức. Một hội thoại chỉ xử lý một lượt tại một thời điểm.
3. Trong transaction ngắn: tạo hội thoại nếu đây là tin đầu, lưu tin khách và trạng thái lượt trả lời. Cùng requestId và cùng payload chỉ tạo một lượt.
4. Kết thúc transaction trước khi gọi AI. Không giữ row lock hoặc transaction DB trong lúc chờ mạng.
5. Xác định nhu cầu; chỉ cho mô hình yêu cầu các công cụ đọc trong danh sách cho phép. Tham số công cụ phải được server kiểm tra, mọi truy vấn vẫn scope bằng Customer hiện tại.
6. Truy vấn dữ liệu hiện tại, tạo ngữ cảnh tối thiểu và gắn nguồn/thời điểm tra cứu. Nội dung hướng dẫn/ghi chú/dịch vụ đều được coi là dữ liệu, không phải chỉ thị thay đổi quyền.
7. Gọi AI để diễn giải. Với giá, phí, trạng thái và locker, trả thêm thẻ dữ liệu có cấu trúc do server tạo; AI không được quyết định các trường này.
8. Kiểm tra câu trả lời và liên kết theo loại cho phép. Không chạy SQL, gọi URL tùy ý, API ghi hoặc mã do mô hình tạo.
9. Trong transaction ngắn: kiểm tra lượt xử lý còn hợp lệ và hội thoại chưa bị xóa; lưu câu trả lời/kết quả và hoàn tất lượt.
10. Nếu lỗi: lưu trạng thái lỗi phù hợp, giữ tin khách để thử lại; không chèn câu trả lời giả. Nếu client mất kết nối, cho phép tra cứu lượt gửi bằng requestId.

Ngữ cảnh hội thoại chỉ lấy số tin gần nhất trong ngân sách cho phép. Câu trả lời cũ không được coi là bằng chứng giá, trạng thái hoặc số ô trống hiện tại.

## 4. Cấu trúc code theo các tầng ABP của dự án

Một type/file cho các type mới, namespace khớp thư mục. Interface không để chung file DTO; logic endpoint chỉ binding, gọi service và trả kết quả.

```text
WashGo.Domain.Shared/
  Enums/ChatMessageRole.cs, ChatTurnStatus.cs
  Constants/ChatbotErrorCodes.cs
WashGo.Domain/Entities/Chatbot/
  ChatConversation.cs
  ChatMessage.cs
  Interfaces/IChatConversationRepository.cs
  Interfaces/IChatMessageRepository.cs
WashGo.Application.Contracts/Features/ChatbotContracts/
  Commands/SendChatMessageCommand.cs
  Queries/ChatConversationCursorQuery.cs, ChatMessageCursorQuery.cs
  Results/ChatConversationDto.cs, ChatMessageDto.cs, ChatTurnResultDto.cs
  Results/ChatSourceDto.cs, ChatActionDto.cs, ChatCursorPageDto.cs
  Services/IChatbotAppService.cs
WashGo.Application/Features/ChatbotApplication/
  Services/ChatbotAppService.cs
  Services/ChatTurnCoordinator.cs
  Services/ChatbotContextBuilder.cs
  Services/ChatbotKnowledgeProvider.cs
  Services/IChatModelClient.cs
  Providers/GeminiChatModelClient.cs
  Tools/<Các adapter đọc dịch vụ, đơn, chính sách, locker>
  Knowledge/<Hướng dẫn đã duyệt, có mã và phiên bản>
WashGo.EntityFrameworkCore/
  BuilderConfiguration/ChatConversationEntityTypeConfiguration.cs
  BuilderConfiguration/ChatMessageEntityTypeConfiguration.cs
  Repositories/<Repository tương ứng>
  Migrations/<Migration mới>
WashGo.HttpApi/Endpoints/Chatbot/ChatbotHandler.cs
```

Tên file chi tiết có thể điều chỉnh khi triển khai để dùng chung thành phần hiện có; không tạo lớp rỗng chỉ để đủ danh sách. Interface client AI thuộc phần tích hợp nội bộ Application, không mở ra thành endpoint tự động.

## 5. Thiết kế dữ liệu đề xuất

### ChatConversation

- `Id`, `CustomerId` theo Guid và quy ước liên kết User/Customer đang có.
- `Title`: lấy từ tin đầu theo quy tắc server, không gọi thêm AI chỉ để đặt tên.
- Khóa thứ tự tạo bất biến, `LastMessageAt`, các trường audit/xóa theo quy ước hiện tại.
- Trạng thái lượt đang chạy và thông tin lease hết hạn để phục hồi sau process restart.

### ChatMessage

- `Id`, `ConversationId`, `Role`, `Content`, thứ tự tin nhắn tăng trong hội thoại và thời điểm tạo.
- `RequestId` gắn trên tin khách, fingerprint để phân biệt retry với payload khác.
- Tin trợ lý liên kết tin khách; trạng thái lượt `Pending/Processing/Completed/Failed` và error code an toàn.
- Nguồn, thẻ kết quả cần thiết để FE hiển thị lại; không lưu response thô chứa tool context hoặc secrets.
- Model/tokens/duration nếu nhà cung cấp trả về, phục vụ theo dõi hạn mức; không đưa khóa tích hợp vào log.

Ràng buộc: unique `(ConversationId, RequestId)` cho tin khách; mỗi tin khách có tối đa một câu trả lời thành công; unique thứ tự tin trong hội thoại. Index phục vụ owner + thứ tự hội thoại và conversation + thứ tự tin. FK, delete behavior và chiều dài có cấu hình rõ ràng.

Ưu tiên hai bảng này, không mở thêm một ledger trùng chức năng chỉ để theo dõi chat. Metadata xử lý có thể nằm trên tin khách và hội thoại. Migration không sửa dữ liệu đơn, dịch vụ, locker.

## 6. Lazy load và tạo hội thoại mới

### Danh sách hội thoại

- Tải 20 hội thoại/lần, tối đa 50; dùng `cursor` và `hasMore`, không cần total count mỗi lần.
- Bản đầu sắp xếp theo thứ tự tạo hội thoại mới nhất trước, khóa thứ tự bất biến; `LastMessageAt` dùng hiển thị, không làm thay đổi vị trí giữa các đợt tải.
- Cursor chứa ranh giới ổn định và mốc dữ liệu đầu phiên tải. Hội thoại mới phát sinh sau đó hiển thị qua refresh/FE chèn từ kết quả tạo, không làm trượt các đợt đang tải.
- Cursor được xác thực và scope theo user/resource/filter; cursor sai, khác hội thoại hoặc hết hạn trả lỗi rõ ràng để FE tải lại.
- Nếu sau này muốn xếp theo hoạt động gần nhất, cần thiết kế snapshot thứ tự riêng; không đổi sang trường cập nhật liên tục rồi khẳng định không bỏ sót.

### Tin nhắn

- Tải 30 tin/lần, tối đa 100. Lần đầu lấy các tin gần nhất; mỗi đợt trả theo thứ tự cũ đến mới để FE hiển thị.
- Cuộn lên đầu gửi cursor tải tin cũ hơn và prepend. FE giữ vị trí cuộn bằng ID tin đang đọc.
- Có tie-breaker/thứ tự duy nhất; tin mới không làm dịch chuyển ranh giới tải tin cũ. Không trả trùng một tin trong các đợt tải liên tiếp.
- Đang tải không gửi yêu cầu trùng; hết dữ liệu ngừng tải. Nếu lỗi, giữ danh sách và cho thử lại.

### Hội thoại mới và xóa

- Nhấn Hội thoại mới chỉ tạo khung chat trống ở FE. Chỉ tạo bản ghi khi gửi tin đầu thành công; không tạo nhiều hội thoại rỗng.
- Có thể gửi tin đầu tới endpoint riêng bên dưới, server tạo hội thoại + tin khách trong cùng transaction.
- Xóa hội thoại phải kiểm tra owner, ẩn cả tin và lượt xử lý; worker đang gọi AI không được ghi sống lại hoặc trả dữ liệu hội thoại đã xóa.
- Soft delete không đồng nghĩa dữ liệu đã bị xóa vật lý khỏi DB hoặc nhà cung cấp AI. Lịch sử được lưu tối đa 90 ngày; cần tác vụ purge và kiểm thử thật trước khi đánh dấu hoàn thành. Cơ chế lưu dữ liệu bên Gemini phải được kiểm tra theo loại dịch vụ/tài khoản dùng thực tế.

Lazy load dùng cursor có mục đích khác `BaseFilterQuery` đang dựa trên Page/Take. Không ép kế thừa để tạo các trường phân trang không sử dụng; chỉ tái sử dụng helper/kiểu chung có ý nghĩa.

## 7. API đề xuất cho FE và Swagger

Tất cả route dưới `/customer/chatbot`, policy `CustomerOnly`, có WithSummary/WithOpenApi. Kiểm tra owner trong service kể cả route lookup/retry/delete.

| Method | Route tương đối | Mục đích |
|---|---|---|
| GET | `/conversations?cursor=&take=20` | Lazy load hội thoại của tôi |
| POST | `/conversations/first-message` | Gửi tin đầu, tạo hội thoại và bắt đầu lượt trả lời |
| GET | `/conversations/{id}/messages?cursor=&take=30` | Lazy load tin nhắn của hội thoại |
| POST | `/conversations/{id}/messages` | Gửi câu hỏi tiếp theo |
| GET | `/conversations/{id}/requests/{requestId}` | Xem trạng thái/kết quả sau timeout hoặc tải lại trang |
| DELETE | `/conversations/{id}` | Xóa hội thoại của tôi |

Không cần API tạo hội thoại rỗng. Metadata hội thoại được trả cùng kết quả gửi tin/lấy danh sách; chỉ thêm endpoint detail nếu FE thực sự cần.

Ví dụ request gửi tin:

```json
{
  "requestId": "<Guid mới cho một ý định gửi>",
  "message": "Có locker nào gần tôi còn ô trống không?",
  "context": {
    "selectedOrderId": null,
    "location": {
      "latitude": 10.77,
      "longitude": 106.69
    },
    "city": null,
    "district": null
  }
}
```

`requestId` retry phải giữ nguyên. `context` tùy chọn; không có location thì không suy đoán vị trí. Tọa độ chỉ gửi sau khi khách đồng ý. Không ghi context location vào nội dung tin lưu lâu dài; fingerprint dùng khóa bảo mật, không lưu tọa độ thô để so retry.

Kết quả hoàn tất gồm conversationId, messageId, requestId, answer, sources, actions/cards và thời điểm tra cứu. Route lookup trả thêm turnStatus/errorCode. Khi provider chưa trả xong hoặc request gửi trùng đang xử lý, trả trạng thái có thể tra cứu, không tạo lượt AI thứ hai.

Mã lỗi dự kiến: `ConversationNotFound`, `InvalidCursor`, `InvalidChatContext`, `RequestIdReused`, `ConversationBusy`, `ChatRateLimited`, `AiNotConfigured`, `AiUnavailable`, `ChatTurnExpired`. HTTP status và envelope phải khớp quy ước dự án, tài liệu FE phải ghi rõ 200/202/400/401/403/404/409/429/503.

## 8. Quy tắc từng nguồn dữ liệu

### Hướng dẫn và chính sách

- Nội dung hướng dẫn trong source có mã/version, được review theo Word và luồng BE; không gửi cả file Word vào mỗi request.
- Điều khoản hiện hành lấy từ nguồn policy; câu hỏi đơn cụ thể đọc policy snapshot/quote của đơn.
- Không copy công thức phí sang prompt. Nếu chưa có API/helper đọc quote phù hợp, tách phần tính thuần dùng chung từ logic hiện có; không gọi API tạo thanh toán để lấy giá.
- Trả source code/title/version khi có. Chưa có căn cứ thì trả thiếu thông tin, không tự đặt chính sách.

### Đơn và ETA

- Dùng user JWT để scope trước khi lấy dữ liệu; không cho AI truyền customerId tùy ý.
- Mã đơn hoặc selectedOrderId đều phải kiểm tra quyền. Không cho biết đơn người khác có tồn tại không.
- Chỉ cung cấp mã đơn, trạng thái, các khoản tiền được phép xem, tiến độ, ETA và hành động được phép. Không gửi địa chỉ/điện thoại không cần thiết, QR/PIN, token phiên nhận đồ.
- ETA là dự kiến giặt xong, có nguồn và thời điểm cập nhật; thiếu thì để trống. Không suy đoán nguyên nhân chậm từ trạng thái.

### Dịch vụ

- Nguồn chính là danh mục đang bán. Dùng lại OR trong cùng tiêu chí và AND giữa tiêu chí theo query hiện có.
- Luôn ghi đơn vị giá; phân biệt giá dịch vụ và giá cuối của đơn. Giờ xử lý chuẩn không phải cam kết nhận đồ tại locker.
- Không khẳng định xử lý được chất liệu khi mô tả dịch vụ chưa có thông tin.

### Locker

- Có tọa độ hợp lệ thì gọi truy vấn khoảng cách hiện có. Phải có cả latitude/longitude và nằm trong miền hợp lệ.
- Nếu client chỉ gửi một tọa độ: trả lỗi context, không để truy vấn âm thầm coi là không có vị trí.
- Không cấp vị trí thì hỏi khu vực; không tự geocode địa chỉ tự do hoặc gán vị trí hồ sơ làm vị trí hiện tại.
- Card trả tên, địa chỉ, distanceKm nếu có, số ô trống, ID và loại action mở chi tiết. Không tự bịa URL bản đồ.
- Khoảng cách đường chim bay, số ô trống là tại lúc tra cứu; tạo đơn phải kiểm tra và giữ ô bằng luồng hiện có.

## 9. Bảo vệ dữ liệu và kiểm soát chi phí

- API key/model/base URL chỉ nằm trong cấu hình, không commit secret. Base URL từ cấu hình tin cậy; model không được tự chọn host.
- Disable/mask audit tự động cho nội dung chat và location tại các boundary liên quan; log vận hành chỉ có ID kỹ thuật, status, duration và usage cần thiết.
- Từ nguồn nghiệp vụ: tuyệt đối không đưa field secret vào prompt. Với khách tự dán OTP/token/mật khẩu: có kiểm tra/lọc đầu vào và thông báo; không cam kết bộ lọc có thể nhận diện mọi chuỗi bí mật tùy ý.
- Công cụ đọc có allowlist, schema validation, giới hạn số lần gọi và kích thước kết quả. Cả mô tả dịch vụ và lịch sử chat đều có thể chứa chỉ dẫn không tin cậy.
- Không mở transaction dài, không log raw provider body. Không trả trace/stack/secret khi cổng AI lỗi.
- Timeout hoặc retry có thể phát sinh lại chi phí ở nhà cung cấp nếu request trước đã được họ nhận. Không hứa “exactly once” đối với dịch vụ bên ngoài; ứng dụng phải chống ghi tin trùng và giới hạn retry.

Thông số kỹ thuật khởi đầu đã được đồng ý; triển khai dạng cấu hình để điều chỉnh sau:

| Thông số | Giá trị khởi đầu |
|---|---|
| Độ dài câu hỏi | 1–2.000 ký tự sau trim |
| Tần suất gửi | 10 lượt/phút/Customer |
| Hạn mức ngày | 100 lượt/Customer/ngày, mốc ngày giờ Việt Nam |
| Lượt đồng thời | 1/hội thoại, tối đa 2/Customer |
| Context tin gần nhất | Tối đa 12 tin và có ngân sách độ dài/token |
| Công cụ đọc | Bản đầu một công cụ/lượt, tối đa 5 bản ghi dịch vụ/locker; hai lần generateContent |
| Thời gian chờ AI | Tối đa 30 giây/lượt xử lý, budget bao gồm các lần gọi |
| Retry tự động | Không tự retry cổng Gemini; timeout có thể xảy ra sau khi cổng đã nhận request. Client lookup/replay cùng requestId không gọi AI lại |
| Lưu lịch sử | 90 ngày; bổ sung cơ chế purge và test hết hạn |

Nhà cung cấp là Gemini. Model ID đặt trong cấu hình, chọn model ổn định phù hợp từ tài liệu chính thức và kiểm tra quyền truy cập bằng API key khi tích hợp; không tự gắn một model ID chưa kiểm chứng. Ngân sách tiền tổng phụ thuộc tài khoản Gemini thực tế; không ghi con số giá API khi chưa xác minh. Hạn mức nhiều instance cần kho dùng chung hoặc DB, không chỉ bộ đếm RAM.

## 10. Các chặng triển khai và điểm nghiệm thu

### Chặng 1 — Chốt hợp đồng và nguồn kiến thức

- [x] Duyệt phạm vi 6 use case, provider Gemini, hạn mức và thời hạn lưu.
- [x] Đối chiếu model mặc định với tài liệu chính thức; model để trong cấu hình.
- [x] Người dùng điền key riêng, Docker đã nạp lại; model cấu hình trả lời thật thành công.
- [x] Có payload/response, cursor, trạng thái lượt gửi, mã lỗi trong API review.
- [x] Nội dung hướng dẫn baseline version 2026-11-13-v1 bám Word/code; dữ liệu sống qua adapter có source/version.
- [x] Có API review cho FE về new chat, retry và lazy load.

Đầu ra: contracts draft và tài liệu; chưa cần gọi AI thật. Không chỉnh Word âm thầm nếu thay đổi nghiệp vụ.

### Chặng 2 — Hội thoại, phân quyền và lazy load

- [x] Entity/config/repository/contracts/endpoints và migration AddCustomerChatbot.
- [x] Tạo hội thoại cùng tin đầu, history, cursor, xóa khỏi lịch sử.
- [x] Duplicate requestId, payload mismatch, concurrent replay, busy và owner checks PASS.
- [x] Processing/Completed/Failed, lease và lookup; không có queue Pending riêng trong bản đầu.
- [x] Ẩn sau 90 ngày, worker purge mỗi 30 phút; test repository purge dữ liệu 91 ngày PASS.
- [x] Smoke persistence/lazy load qua project ChatbotHost riêng PASS, không có production Mock switch.

Nghiệm thu: dữ liệu và phân quyền đúng khi chưa có khóa AI. Test client không phải fallback production; thiếu cấu hình AI thật phải báo rõ.

### Chặng 3 — Adapter dữ liệu WashGo

- [x] Công cụ đọc dịch vụ, locker, đơn/ETA/quote và chính sách; hướng dẫn baseline riêng.
- [x] Tái sử dụng query/helper, scope bằng JWT/owner trước khi lấy đơn.
- [x] DTO allowlist và sources/cards do server sinh.
- [x] Test các adapter, selectedOrderId khác chủ và access-code isolation PASS. Chưa kiểm chứng lời diễn giải model thật.

Nghiệm thu: các câu hỏi mẫu có dữ liệu thật để diễn giải; không có công cụ ghi nghiệp vụ.

### Chặng 4 — Tích hợp AI thật

- [x] Gemini sau IChatModelClient, compose/.env mappings; thiếu key trả 503 AiNotConfigured trên Docker thật.
- [x] System instruction, tool allowlist, context/output budget; bảo toàn thoughtSignature và function call ID.
- [x] Có timeout/cancellation/quota/error/lease; HTTP contract lỗi, quota phút và stale lease có test. Chưa đo timeout/quota Google thật và chuyển ngày thực tế.
- [x] Transaction ngắn, kiểm tra hội thoại trước công cụ và trước lưu; delete race PASS.
- [x] 4 live test với key người dùng: hướng dẫn nhận đồ, dịch vụ/giá, locker có/không vị trí; không in key/token, đã ẩn chat thử và đăng xuất.
- [ ] Nghiệm thu thêm đơn/ETA/snapshot policy, prompt-injection và câu hỏi nhiều nghĩa với Gemini thật.

Nghiệm thu: trả lời thật đủ 5 nhóm câu hỏi; lỗi khóa/quota/network không làm mất history hoặc tạo câu trả lời giả.

### Chặng 5 — Kiểm thử tích hợp và Docker

- [x] Suite chatbot PASS. Nguồn query nghiệp vụ cũ không sửa; helper HTTP chỉ bổ sung status 202/503, được suite kiểm chứng.
- [x] Build BE/fixture host; migration 20261113025410_AddCustomerChatbot đã áp dụng, không pending model changes.
- [x] Docker build/recreate PASS; Swagger 5 paths/6 operations; missing-key 503 PASS.
- [ ] Test AI thật có giới hạn; ghi rõ phần test giả lập, phần gọi provider thật và phần chưa kiểm chứng.
- [x] .env bị ignore, .env.example key rỗng, compose map Gemini; không in key.

Nghiệm thu: Swagger chạy, history còn sau restart, không pending migration/model ngoài dự kiến, không ảnh hưởng trạng thái/ô tủ/thanh toán.

### Chặng 6 — Bàn giao và chia nhánh

- [x] API review, setup và smoke script đã có.
- [x] Tiến độ/checklist ghi rõ đã test fixture và còn thiếu nghiệm thu Gemini thật.
- [x] Giới hạn ETA/khoảng cách/read-only/no streaming đã ghi trong bàn giao.
- [x] Tách using refactor và chatbot thành hai nhánh/commit nối tiếp; xem [CHATBOT_GIT_PUSH_PLAN.md](CHATBOT_GIT_PUSH_PLAN.md).
- [ ] User duyệt và tự tạo PR theo workflow đang áp dụng.

Các chặng chưa thực hiện đều để trống checkbox. Mỗi chặng code ổn định cần build và cập nhật tiến độ; chỉ đưa API đã chạy được lên checklist hoàn thành.

## 11. Ma trận kiểm thử bắt buộc

| Nhóm | Tình huống cần PASS |
|---|---|
| Authentication | Không token 401; role nhân viên 403; Customer hợp lệ được truy cập |
| Ownership | Đọc/gửi/xóa/lookup hội thoại khác và tra đơn khách khác đều bị từ chối |
| New chat | Chưa gửi không có row; retry tin đầu không tạo hai hội thoại; không mang context chat cũ |
| Lazy load | Ranh giới nhiều trang, timestamp trùng, dữ liệu mới xen vào, hết dữ liệu, cursor sai và cursor user khác; không duplicate/skip do pagination |
| Tin nhắn | Tin cũ tải theo cursor, hiển thị đúng thứ tự; chuyển chat không trộn tin; request lỗi không mất dữ liệu đã tải |
| Đồng thời | Hai request cùng requestId; khác payload; hai tin khác ID cùng hội thoại; process restart; response client timeout; delete trong lúc AI đang chạy |
| Dịch vụ | Nhiều loại/giá, không có kết quả, giá/đơn vị/giờ đúng DB; không suy đoán chất liệu |
| Đơn/ETA | Trạng thái và quote khớp BE, thiếu ETA, đơn hủy/hoàn tất, sai mã đơn; không lộ QR/PIN |
| Chính sách | Chính sách hiện hành và snapshot cũ của đơn khác nhau; thiếu nguồn; AI không tự quyết định tiền |
| Locker | Có/không cấp vị trí, thiếu một tọa độ, tọa độ ngoài miền, không có ô, locker inactive; sắp xếp gần; không giữ chỗ hoặc bịa đường đi |
| Prompt injection | Yêu cầu bỏ qua quyền, lấy dữ liệu khách khác, ghi trạng thái Paid, gửi bí mật; chỉ dẫn độc hại nằm trong mô tả dịch vụ |
| Provider | Thiếu khóa, khóa sai, timeout, 429/5xx, malformed output, sai tool args; budget và retry hữu hạn |
| Logging/quota | Không lộ raw prompt/context location/secrets; hạn mức hoạt động khi đồng thời, đúng mốc ngày Việt Nam |
| Regression | Không thay đổi đơn, thanh toán hoặc số ô chỉ vì trò chuyện; các query có sửa vẫn giữ hành vi cũ |

Test tự động dùng provider test để kiểm soát lỗi, đảm bảo không phát sinh chi phí mỗi lần chạy. Test provider thật là đợt riêng có hạn mức, báo kết quả đúng mức đã kiểm chứng.

## 12. Phương án nhánh đề xuất

1. `feat/toantran/chatbot-conversations`: dữ liệu, cursor, owner, operation lifecycle và test nền.
2. `feat/toantran/chatbot-washgo-context`: nguồn kiến thức và các công cụ đọc dùng chung.
3. `feat/toantran/chatbot-ai-integration`: provider thật, điều phối, hạn mức và cấu hình.
4. `test/toantran/chatbot-handoff`: kiểm thử tích hợp, Docker và API review/tiến độ còn lại.

Nếu dùng nhánh nối tiếp, ghi rõ base của từng PR; không push thẳng develop/main. Có thể gộp nhánh 4 vào các nhánh chức năng nếu chỉ còn tài liệu nhỏ; test phải đi cùng code tương ứng, không dời toàn bộ test đến cuối.

## 13. Những quyết định đã chốt ngày 12/11/2026

1. **Gemini** là nhà cung cấp AI. Model ID cấu hình riêng, xác minh trước lúc tích hợp; khóa không dán vào tài liệu/chat.
2. Bản đầu trả câu trả lời hoàn chỉnh, **chưa streaming**.
3. Cursor lazy load, thứ tự hội thoại theo lúc tạo, 20 hội thoại/30 tin mỗi đợt; chat tiếp không tự đưa hội thoại cũ lên đầu.
4. Câu hỏi tối đa 2.000 ký tự, 10 lượt/phút và 100 lượt/ngày/Customer; các giới hạn kỹ thuật khác theo bảng mục 9.
5. Lưu lịch sử 90 ngày; Customer có thể xóa trước hạn. Phải triển khai tác vụ purge trước khi ghi nhận tự xóa đã hoạt động.
6. Nguồn hướng dẫn bám Word và code đã kiểm chứng; điều khoản/giá/trạng thái lấy từ nguồn được phép. Không lấy câu trả lời AI làm nguồn tự bổ sung kiến thức đã duyệt.

Bước còn lại: nghiệm thu rộng đơn/ETA/snapshot policy, độ trễ/quota và prompt-injection ở lớp mô hình. Key đã được nạp, Gemini thật PASS 4 ca cơ bản. Chưa có FE; đã chia nhánh/commit, người dùng tự push/tạo PR. Không coi các ca fixture hoặc 4 live test là toàn bộ ma trận AI đã PASS.
