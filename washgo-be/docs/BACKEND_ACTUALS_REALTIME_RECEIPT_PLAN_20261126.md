# Kế hoạch kiểm đếm thực tế, realtime đơn hàng và PDF biên lai

Ngày rà soát 26/11/2026, BE tại commit `63c8f4a`. Yêu cầu lấy từ [BACKEND.md](BACKEND.md) và [BACKEND_RECEIPT.md](BACKEND_RECEIPT.md). Tên endpoint và trường mới bên dưới là đề xuất để BE và FE thống nhất trước khi tích hợp.

## Thứ tự triển khai

1. **PDF biên lai** có thể làm ngay vì thay đổi nằm ở trình render, có thể bàn giao trước.
2. **Xác nhận đồ thực tế** chốt quy tắc giá, voucher và đơn cũ, sau đó làm migration, API và tích hợp với thanh toán.
3. **Realtime đơn hàng** dùng hợp đồng revision và quyền truy cập đã rà ở hai bước trên. Có thể chuẩn bị hợp đồng sự kiện trong khi chốt quy tắc giá.

## 1. PDF biên lai — BACKEND_RECEIPT.md

### Hiện trạng

- FE tải file dạng blob từ `GET /wash-order/{id}/receipt/pdf`. BE dùng `OrderReceiptAppService` để tạo cùng bộ dữ liệu cho JSON và PDF, rồi render bằng QuestPDF trong `ReceiptPdfRenderer`. Biên lai có số và ngày phát hành cố định từ lần xuất đầu.
- PDF hiện khổ A5, header chữ WashGo, font Be Vietnam Pro nhúng sẵn. Đã có hai cột bên mua/bên bán, bảng dịch vụ và header bảng. Giao dịch đang hiển thị chung một dòng với mã đầy đủ, tổng thanh toán chỉ in đậm, footer còn đoạn mô tả cần bỏ.
- BE đã có PNG logo WashGo nhúng trong assembly. Quyền và cách ẩn dữ liệu phụ thuộc vai trò. Customer thấy WashGo là bên cung cấp, Merchant không thấy thông tin liên hệ của khách, Admin đọc theo quyền hiện có. Chi tiết các khoản tiền lấy từ giao dịch đã ghi nhận, gồm cả trường hợp thanh toán gộp.
- Có smoke test `tests/smoke/order-receipt.ps1` cho JSON, PDF và quyền truy cập.

### Hướng sửa

- Đổi renderer sang A4, lề 18–20 mm, dùng font hiện tại và tái sử dụng logo PNG nhúng. Header xanh nhạt, tách rõ tiêu đề, số biên lai, mã đơn và thời điểm phát hành. Giữ hai cột thông tin và chỉ in dòng có dữ liệu được cấp quyền.
- Giữ header bảng lặp qua trang, cho tên dài xuống dòng, giữ từng dòng dịch vụ đọc được khi ngắt trang. Dùng khối giao dịch thoáng, có nhãn phương thức, thời điểm, số tiền và mã đối soát. Căn phải phần tổng tiền, làm nổi bật **TỔNG ĐÃ THANH TOÁN**. Đối chiếu các nhãn với JSON để phân biệt tiền dịch vụ trước giảm giá, tiền thực thu và số điểm đã dùng.
- Gỡ đoạn mô tả khỏi PDF tại renderer. Giữ hợp đồng JSON hiện tại, gồm `note`, để tránh ảnh hưởng trang in của FE. Không thay đổi logic tính tiền, tên endpoint, số biên lai và quyền truy cập.
- Ưu tiên mã giao dịch do cổng cung cấp và hiển thị nguyên mã. Với tham chiếu nội bộ, chỉ rút gọn khi nhận diện đúng prefix, đúng mã đơn và hậu tố GUID; mã lạ giữ nguyên. Mọi dữ liệu gốc phục vụ đối soát vẫn dùng nguyên giá trị.
- Nghiệm thu PDF một trang và nhiều trang với tên dịch vụ tiếng Việt dài, nhiều giao dịch, phí hủy/lưu kho, voucher và điểm. So PDF với JSON theo từng vai trò, kiểm tra không tràn bảng, không có đoạn footer cũ và không lộ dữ liệu bị ẩn. Giữ smoke hiện có làm hồi quy.

## 2. Kiểm đếm thực tế — BACKEND.md

### Hiện trạng

- `WashOrderItem` đã lưu ID dòng, số lượng đặt, `IsWeightBased`, đơn giá và thành tiền lúc tạo đơn. Dịch vụ theo món tính đơn giá nhân số lượng đặt. BE chưa lưu số lượng đã kiểm đếm hay mã snapshot phân biệt cái/đôi.
- `PUT /wash-order/{id}/actual-weight` chốt một giá trị kg cấp đơn ở `Collected`. Endpoint đã khóa dòng đơn trong ABP Unit of Work, kiểm tra Merchant phụ trách và dùng `OrderWriteGuard`. API có `expectedRevision`, `expectedStatus`, `operationId` và endpoint tra thao tác. Luồng bắt đầu giặt đã chặn đơn kg thiếu cân.
- Chốt kg hiện tính từ tiền dịch vụ đã lưu, phụ phí mới và giảm giá đã lưu. Chưa có preview hoặc một lần xác nhận toàn bộ dòng cái/đôi. Biên lai đang lấy `Quantity` và `SubTotal` từ dòng đơn; vì thế cần cập nhật phần chiếu dữ liệu khi số thực tế trở thành số tính tiền.

### Hướng sửa

1. **Snapshot và tương thích.** Thêm mã đơn vị snapshot ở dòng đơn, cùng `actualQuantity` nullable và thành tiền sau xác nhận. Giữ số lượng và thành tiền lúc đặt để đối chiếu, lưu một thời điểm/người xác nhận ở cấp đơn và ghi chênh lệch vào timeline. Migration đánh dấu đơn thuộc quy trình mới khi tạo; đơn cũ tiếp tục đi theo quy trình hiện hành. Không suy ra cái/đôi từ tên dịch vụ và không tự ghi số thực tế cho đơn cũ.
2. **Preview và xác nhận.** Đề xuất `POST /wash-order/{id}/actuals/preview` và `PUT /wash-order/{id}/actuals`. Request có toàn bộ `orderItemId` theo cái/đôi, kg cấp đơn nếu có, lý do nếu có chênh lệch, revision và operation ID khi ghi. Preview trả giá cũ/mới, thay đổi voucher và lý do theo dòng. Khi ghi, BE khóa đơn, xác minh đúng Merchant, `Collected`, chưa bắt đầu giặt/chưa thanh toán, kiểm tra revision và tính lại giá. Nếu kết quả khác preview, trả xung đột và yêu cầu chốt lại. Tái sử dụng guard và endpoint `GET /wash-order/{id}/operations/{operationId}`, cùng ID và payload phát lại trả kết quả cũ.
3. **Xác nhận đủ và tính một lần.** Kiểm tra dòng thiếu/trùng/sai đơn, số nguyên theo cái/đôi, giới hạn số lượng, lý do chênh lệch và đơn còn ít nhất một dịch vụ thực tế. Đơn hỗn hợp chốt kg và cái/đôi trong một giao dịch; dùng nguyên mô hình gói/phụ phí kg hiện tại. Giữ endpoint cân cho luồng kg hiện có, còn đơn hỗn hợp thuộc quy trình mới phải qua chốt chung để mở bước giặt. Lưu các dòng, tổng tiền, audit và operation cùng một UoW.
4. **Đồng bộ giá tới các luồng đọc.** Dùng đơn giá snapshot, áp dụng số thực tế cho dòng theo món. Dùng chung quy tắc tính cho preview và xác nhận, bảo đảm `TotalAmount`, giảm giá, phụ phí và `FinalAmount` khớp. Guard và capability dùng cùng luật tại bước bắt đầu giặt và trước thanh toán. Rà báo giá checkout gộp, phí hủy, báo cáo đọc subtotal và JSON/PDF biên lai; hiển thị đúng số lượng tính tiền sau xác nhận. Số đặt vẫn có trong chi tiết đơn.
5. **Nghiệm thu.** Test đơn nhiều dịch vụ cùng đơn vị, hai loại đơn vị, đơn có cả kg, số 0, số lẻ, số âm, vượt giới hạn, voucher rớt ngưỡng, catalog đổi giá, hai request cùng revision, gửi lại command sau timeout và đơn cũ đang chạy dở. Kiểm tra BE chặn vượt quyền và không ghi dở khi lỗi. Kiểm tra giá hiển thị khớp tổng phải thanh toán và biên lai.

### Quy tắc đề xuất cần xác nhận trước khi code

| Điểm cần chốt | Đề xuất |
| --- | --- |
| Dòng có số thực tế bằng 0 | Cho phép nếu có lý do và còn ít nhất một dịch vụ thực tế trong đơn. Đơn không còn dịch vụ cần xử lý ngoại lệ. Duy trì chặn nếu tổng cuối không hợp lệ theo quy tắc thanh toán hiện có. |
| Voucher sau kiểm đếm | Dùng điều khoản ưu đãi tại lúc đặt và xét lại theo số tiền mới, không cấp lượt dùng mới. Nếu rớt điều kiện, báo ở preview. Rà snapshot ưu đãi và cách thu hồi lượt dùng trên xác nhận trước khi chốt migration. |
| Cơ sở phí hủy | Giữ tỷ lệ và trạng thái đủ điều kiện hiện hành. Nếu nhánh hủy dùng giá tạm tính thì lấy số lúc đặt; nếu nhánh dùng giá thực tế đã chốt thì lấy giá đã xác nhận. Lưu cơ sở tính lúc hủy. |
| ETA và giới hạn | Giữ cách tính ETA hiện tại cho phần kg, giữ thời lượng snapshot cho phần theo món đến khi có bảng thời lượng theo số lượng. Dùng giới hạn số lượng hiện hành cho mỗi dòng. |
| Đơn cũ và quyền Admin | Luồng mới áp dụng cho đơn tạo sau migration. Merchant phụ trách là người chốt. Thao tác Admin đính chính là nghiệp vụ riêng nếu nhóm cần sau này. |

## 3. Realtime đơn hàng — BACKEND.md

### Hiện trạng

- `/hubs/notifications` đã có định danh user và sự kiện `notification`, `notificationState`. FE đã lắng nghe các event thông báo. Chưa có hợp đồng riêng bảo đảm tất cả thay đổi đơn, phân công, thanh toán và job đều kích hoạt tải lại dữ liệu.
- Revision hiện tăng bằng trigger PostgreSQL khi hàng `washgo.orders` được UPDATE. Thay đổi chỉ ghi ở bảng con như phiên thanh toán hoặc phân công cần rà riêng. Trigger tự tăng revision không đồng nghĩa event đã được phát.

### Hướng sửa

1. **Hợp đồng.** Thêm `orderChanged` trên hub hiện có với `eventId`, `orderId`, `revision`, `changeType`, `occurredAt`. API hiện tại là nguồn trả chi tiết, giá và quyền thao tác. Event dùng để báo tải lại dữ liệu và chỉ mang metadata tối thiểu.
2. **Commit và phát.** Lưu outbox đơn hàng trong cùng transaction, worker chỉ đọc record đã commit và retry khi gửi lỗi. Tái sử dụng mô hình worker thông báo nếu phù hợp, giữ event đồng bộ độc lập với thông báo người dùng. Lập bảng các đường ghi và kiểm chứng revision cho cập nhật trạng thái, phân công, kiểm đếm, IPN/đối soát và lưu kho tự động. Với thay đổi chỉ ghi ở bảng con, phát event độc lập và để FE nhận cả event mới có cùng revision.
3. **Người nhận.** Xác định người nhận theo quyền BE lúc phát cho Customer sở hữu đơn, Merchant đang phụ trách và Admin được cấp quyền. Khi đổi Merchant, bên cũ nhận tín hiệu tải lại danh sách trong phạm vi của mình, bên mới nhận event của đơn. Tái sử dụng cơ chế thu hồi phiên trên hub. Không gửi QR, PIN, token, thông tin liên hệ hay DTO chi tiết.
4. **Tích hợp FE.** Dùng một kết nối theo phiên, gộp event gần nhau, dùng revision/eventId để bỏ trùng và response cũ. Tải lại chi tiết, danh sách, đếm và tổng hợp theo bộ lọc kể cả khi đơn chưa có trên trang. Khi reconnect/focus thì đọc lại để bù event lỡ; modal cũ yêu cầu người dùng kiểm tra lại trước khi gửi. Trạng thái thanh toán vẫn lấy từ API.
5. **Nghiệm thu.** Mở Customer, Merchant và Admin song song, test thay đổi từ API, Shipper, payment callback và worker. Kiểm tra rollback không phát event, worker retry không nhân thao tác, event đảo thứ tự không làm UI lùi trạng thái và tài khoản/tiệm đã mất quyền không nhận dữ liệu đơn.

## Bàn giao

Cập nhật Swagger và ví dụ request/response cho đơn nhiều dòng và đơn hỗn hợp. Bàn giao bảng capability/mã lỗi, chính sách đơn cũ, hợp đồng SignalR và mẫu PDF đối chiếu được với JSON. Hợp đồng BE hiện thực được ghi ở phần bên dưới.

## Trạng thái triển khai BE

Migration `20261126040725_AddOrderActualsAndRealtime` bổ sung snapshot, số lượng đã chốt và outbox PostgreSQL. Cần áp migration trước khi sử dụng phiên bản BE này. Smoke/API và kiểm tra bố cục PDF do nhóm chạy ở bước nghiệm thu.

### Hợp đồng kiểm đếm để FE tích hợp

- Merchant gọi `POST /wash-order/{id}/actuals/preview` với danh sách đầy đủ dòng theo món, `actualWeightKg` nếu có dịch vụ kg và `note` nếu số lượng khác số đặt. Dùng danh sách `items` từ chi tiết đơn để có ID dòng. Giá, voucher sau điều chỉnh và revision lấy từ response của BE.
- Gọi `PUT /wash-order/{id}/actuals` với cùng dữ liệu, bổ sung `expectedRevision`, `expectedFinalAmount` và `operationId` (UUID). Có thể gửi thêm `expectedStatus`. Ví dụ dưới đây dành cho đơn chỉ theo cái/đôi:

```json
{
  "expectedRevision": 12,
  "operationId": "41e09abb-0a17-4dc6-8099-af8d36fab209",
  "expectedStatus": 3,
  "expectedFinalAmount": 150000,
  "items": [
    { "orderItemId": "8d5d2e39-b712-4e33-8535-3a6820c934a2", "actualQuantity": 3 }
  ],
  "note": "Tiệm nhận thêm một món so với số lượng đã đặt."
}
```

- Đọc capability `allowedActions.canConfirmActuals` và lý do tương ứng. `items[].quantity` và `subTotal` là lúc đặt. `actualQuantity` và `actualSubTotal` nullable là thực tế đã chốt. `unitSnapshot` hiện có `item`, `pair`, `kg`; giá kg vẫn tính cho gói. Preview trả `items[].unit` cho từng dòng kiểm đếm. Hiển thị `actualsConfirmedAt` và `actualsNote` theo quyền.
- Lỗi theo dòng dùng `meta.code` và `meta.orderItemId` nếu có, ví dụ `InvalidActualsLine`, `InvalidActualQuantity` hoặc `ActualsNoteRequired`. Nếu nhận `409`, tải lại. Sau timeout, dùng `GET /wash-order/{id}/operations/{operationId}` hoặc gửi lại cùng operation và payload.
- Đơn mới có dịch vụ theo món phải xác nhận thực tế trước khi bắt đầu giặt. Cần rollout cùng form Merchant trên FE. Có thể dùng Swagger để làm bước này trong lúc FE chưa tích hợp. Đơn cũ có snapshot đơn vị rỗng giữ luồng cũ. Đơn mới chỉ kg giữ API cân riêng.
- Dòng bằng 0 được phép nếu có lý do. Nếu toàn bộ dịch vụ thực tế bằng 0, BE chặn chốt giá và yêu cầu xử lý ngoại lệ. Chưa tự áp phí hoặc tạo luồng hủy mới cho trường hợp này.
- Voucher dùng điều khoản tại lúc đặt. Khi giá mới làm voucher mất hiệu lực, preview báo `PromotionNoLongerEligible`. Xác nhận sẽ trả lại lượt dùng và gỡ voucher khỏi đơn. Chỉnh lại số lượng sau đó không tự áp lại voucher. Phí hủy ở `Collected` của đơn đã kiểm đếm dùng giá cuối và tỷ lệ hiện hành.

### Hợp đồng realtime để FE tích hợp

- Kết nối JWT vào `/hubs/notifications` như hiện tại. Event `orderChanged` có `eventId`, `orderId`, `revision`, `changeType`, `occurredAt`. Có các loại `OrderCreated`, `OrderUpdated`, `StatusChanged`, `MerchantChanged`, `AssignmentChanged`, `ActualsChanged`, `PaymentChanged` và `StorageChanged`. Dùng event để tải lại API của vai trò đang đăng nhập.
- Khi đổi tiệm, `orderScopeChanged` gửi cho tiệm cũ với `eventId` và `occurredAt`. FE tải lại danh sách của tiệm và đóng chi tiết nếu API báo đã mất quyền.
- Revision vẫn là phiên bản ghi của hàng đơn. Thay đổi chỉ ở bảng con phát event với revision hiện hành để tránh khóa ngược thứ tự với giao dịch thanh toán. Với revision bằng nhau, vẫn tải lại nếu `eventId` mới. Dùng `eventId` để bỏ bản phát lại, gộp các lần tải và đọc lại khi reconnect.
- Worker gửi sau commit, có lease và thử lại. Client cần kết nối vào cùng host với worker hoặc triển khai SignalR backplane khi dùng nhiều replica; staging hiện dùng một backend.

### Biên lai và nghiệm thu

Giữ `GET /wash-order/{id}/receipt/pdf` và tên file tải. JSON giữ mã nguyên gốc và trường note. PDF chỉ rút gọn tham chiếu nội bộ được nhận diện, mã giao dịch của cổng giữ nguyên. Kiểm tra một đơn chỉ theo món có chỉnh số lượng, một đơn hỗn hợp, PDF một trang/nhiều trang và ba tài khoản kết nối SignalR đồng thời.
