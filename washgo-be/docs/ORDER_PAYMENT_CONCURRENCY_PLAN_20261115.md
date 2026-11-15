# Kế hoạch xử lý đồng thời: thanh toán và phân công đơn hàng

- Ngày lập: 15/11/2026.
- Trạng thái: đã triển khai code cho A–D, migration và các test liên quan; **chưa chạy test/build hoặc áp migration** cho đợt sửa này, theo yêu cầu người dùng. Chưa coi là nghiệm thu. Hợp đồng bàn giao: [PAYMENT_CONCURRENCY_API_REVIEW.md](PAYMENT_CONCURRENCY_API_REVIEW.md).
- Phạm vi: Backend, hệ thống hiện sử dụng **một Admin**. Vẫn bảo vệ trường hợp một người mở nhiều tab, bấm nhiều lần hoặc client gửi lại request.
- Cơ sở: rà source tại commit `37e0e50`; test tranh ô cuối đã chạy trên Docker local. Các lỗi thanh toán/phân công dưới đây là phát hiện từ source, chưa tái hiện bằng HTTP đồng thời.

## 1. Mục tiêu và phần giữ nguyên

1. Cùng một đơn/mục đích thanh toán không được tạo nhiều phiên thanh toán đang hiệu lực do request đồng thời.
2. Callback lặp không ghi tiền, chuyển trạng thái hoặc gửi thông báo thành công nhiều lần.
3. Khoản tiền cổng xác nhận đã thu phải có dấu vết đối soát, kể cả khi đơn đã thanh toán bằng giao dịch khác.
4. Gửi lại cùng thao tác phân công trả kết quả replay; không phân công hoặc ghi lịch sử hai lần.
5. Phân công hàng loạt khóa đơn theo thứ tự thống nhất, giữ tính toàn bộ thành công hoặc toàn bộ rollback.
6. Không thay đổi tỷ lệ phí hủy, giá dịch vụ, SLA, quy tắc giao nhận hay mở rộng nghiệp vụ nhiều Admin.
7. Giữ các route đang sử dụng; chỉ bổ sung metadata/DTO cần thiết, có hướng dẫn FE nếu thêm mã lỗi hoặc trạng thái.

Không coi việc tắt nút trên FE là cơ chế chống race condition. Quyết định cuối cùng nằm tại BE và DB, có hiệu lực khi chạy nhiều instance.

## 2. Hiện trạng đã xác minh

| Hạng mục | Hiện trạng | Kết luận |
| --- | --- | --- |
| Tranh ô cuối | Khóa locker, cập nhật Available → Reserved có điều kiện, tạo đơn trong transaction | Đã chạy PASS 5/5 lượt với hai Customer khác nhau; mỗi lượt một 201, một 400, đúng một đơn và một lần giảm bộ đếm |
| Hủy/đổi trạng thái/cân/giao nhận | Khóa dòng đơn; kiểm tra trạng thái, một số thao tác có revision và operationId | Giữ cơ chế hiện tại; không suy ra toàn bộ tổ hợp đã được kiểm thử |
| Gán Shipper | Khóa đơn và Shipper, kiểm tra nhiệm vụ hiệu lực | Đã có bảo vệ; cần giữ trong hồi quy |
| Tạo URL tiền giặt | `CreatePaymentAsync` đọc DTO qua GetByIdAsync, chưa khóa đơn, chưa chặn Pending | Có thể tạo nhiều reference/URL cho cùng đơn, kể cả gọi tuần tự |
| Tạo URL phí kho/phí hủy | Đã khóa đơn và kiểm tra Pending | Dùng chung nguyên tắc mới để tránh ba cách xử lý khác nhau |
| Callback | Khóa transaction rồi khóa order; chặn ghi nhận đơn Paid lần hai | Chống ghi hai lần cùng đơn; chưa xử lý đầy đủ tiền thực nhận từ giao dịch thứ hai |
| MoMo tạo URL | Gọi cổng trước khi insert PaymentTransaction | Cần lưu reference trước khi gọi cổng để xử lý timeout, crash và IPN đến sớm |
| Phân công Admin | Kiểm tra replay trước capacity lock; chưa khóa operationId | Request đồng thời cùng key có thể không trả replay như cam kết |
| Batch phân công | Merchant được khóa theo ID; order được khóa theo thứ tự payload | Có nguy cơ deadlock khi hai batch chọn Merchant khác nhau và xử lý các đơn theo thứ tự ngược nhau |
| CI | Có suite tranh ô cuối mới nối; một số test đồng thời khác chưa nằm trong runner | Phải nối test mới vào CI, không chỉ tạo file test |

Các vị trí chính:

- `src/WashGo.Application/Features/PaymentApplication/Services/PaymentAppService.cs`.
- `src/WashGo.Application/Features/PaymentApplication/Services/MomoGateway.cs`.
- `src/WashGo.EntityFrameworkCore/Repositories/PaymentTransactionRepository.cs`.
- `src/WashGo.Application/Features/OrderAssignmentApplication/Services/OrderAssignmentAppService.cs`.
- `src/WashGo.Application/Features/WashOrderApplication/Services/OrderWriteGuard.cs`.
- `src/WashGo.EntityFrameworkCore/Repositories/WashOrderRepository.cs`.
- `tests/ci/Run-ApiSmoke.ps1` và `.github/workflows/backend-ci.yml` ở gốc repo.

## 3. Thứ tự thực hiện và chia PR

| Chặng | Ưu tiên | Nội dung | Điều kiện hoàn tất |
| --- | --- | --- | --- |
| A | Cao | Một phiên thanh toán hiệu lực, lưu reference trước khi gọi cổng | Test đồng thời, timeout, IPN đến sớm và migration đạt |
| B | Cao | Ghi nhận/đối soát tiền thu dư và callback lặp | Không mất dấu tiền thực nhận, không tăng doanh thu/hoàn tiền hai lần |
| C | Trung bình | Replay phân công an toàn khi request đồng thời | Một thay đổi nghiệp vụ; retry cùng key replay, key khác nội dung trả 409 |
| D | Thấp hơn | Thứ tự khóa batch và hồi quy toàn bộ | Hai batch ngược thứ tự không deadlock trong test; rollback trọn batch khi xung đột |

Đề xuất nhánh:

- A: `fix/toantran/payment-session-concurrency`.
- B: `fix/toantran/payment-reconciliation`.
- C–D: `fix/toantran/order-assignment-concurrency`.

Mỗi PR có migration/DTO/test/tài liệu tương ứng nếu cần. Không đánh dấu hoàn tất toàn bộ kế hoạch chỉ vì đã xong chặng A. Không merge chặng B trước các phần phụ thuộc của A.

## 4. Chặng A — quản lý phiên thanh toán

### 4.1 Quy tắc chung cho tiền giặt, phí kho và phí hủy

- Phạm vi phiên: `(OrderId, Purpose)`; chỉ một phiên đang hiệu lực, kể cả khách yêu cầu đổi VNPay sang MoMo.
- Mỗi request kiểm tra JWT, chủ đơn, trạng thái hợp lệ và số phải trả trên server trước khi trả dữ liệu phiên.
- Cùng phương thức, cùng số tiền và phiên vẫn dùng được: trả lại reference/URL đã lưu; không gọi cổng tạo thêm phiên.
- Khác phương thức hoặc số tiền đã thay đổi: trả lỗi có mã rõ ràng, không tự đóng phiên cũ rồi tạo phiên mới khi chưa biết cổng đã thu tiền hay chưa.
- Phiên đang khởi tạo/chưa rõ kết quả: trả `409 PaymentInitializing` hoặc `409 PaymentVerificationRequired`, có hướng dẫn tải lại/chờ xác minh. Không trả thành công với URL rỗng.
- Đơn đã Paid hoặc không còn đủ điều kiện thanh toán: không tạo URL mới. Callback vẫn phải được xử lý/đối soát độc lập.
- Phí phát sinh thêm được tạo thành phiên tiếp theo sau khi phiên trước đã xử lý; không sửa Amount của một reference đã gửi cổng.

### 4.2 Lưu bền trước khi gọi cổng

Tách xử lý thành ba bước, không giữ khóa DB trong suốt HTTP ra cổng:

1. **Transaction chuẩn bị:** khóa dòng đơn, kiểm tra điều kiện/số tiền và phiên hiện có; lưu transaction/reference/snapshot, đánh dấu phiên đang khởi tạo; commit.
2. **Gọi cổng:** dùng đúng reference đã lưu. MoMo tiếp tục dùng reference làm requestId và orderId. Không tự sinh reference mới khi timeout; không cấu hình retry mù cho POST tạo giao dịch.
3. **Transaction hoàn thiện:** khóa transaction rồi order, đọc lại trạng thái; lưu URL/kết quả nếu còn phù hợp. Nếu IPN đã xử lý trước, không ghi đè Succeeded bằng Pending hoặc mở lại phiên đã đóng.

URL VNPay được tạo cục bộ vẫn đi qua cùng coordinator/lưu phiên để bảo đảm quy tắc thống nhất. Không khẳng định thao tác DB và cổng bên ngoài là một transaction nguyên tử.

**Thứ tự khóa thanh toán:** callback, hoàn tiền và bước hoàn thiện phiên đang khóa transaction → order. Bước chuẩn bị không được khóa order rồi tiếp tục lấy FOR UPDATE trên một transaction cũ, vì sẽ tạo chiều khóa ngược. Khi đã có transaction cần cập nhật: xác định reference bằng truy vấn đọc, sau đó khóa transaction → order và kiểm tra lại. Nhánh chỉ tạo transaction mới có thể khóa order, đọc các phiên cũ không khóa/không sửa chúng, rồi insert mới. Mọi nhánh chuyển phiên cũ sang đóng/cần đối soát phải theo cùng thứ tự và đọc lại dưới khóa. Unique index conflict phải rollback rồi đọc lại phiên, không nuốt lỗi hoặc tiếp tục dùng transaction đã lỗi.

Phân biệt rõ:

- Cổng từ chối chắc chắn: ghi thất bại đã xác định; xử lý đóng phiên theo quy tắc cổng.
- Timeout/mất mạng/crash: chưa biết cổng đã tạo/thu tiền; giữ trạng thái chưa xác định và reference để tra soát, không coi là Failed chắc chắn.
- IPN đến trước khi lưu URL: transaction đã tồn tại từ bước 1 nên vẫn tìm được reference.

### 4.3 DB và cấu trúc code dự kiến

- Bổ sung dữ liệu tối thiểu: trạng thái khởi tạo, cờ phiên hiệu lực, thời điểm tạo/hết hạn nếu được cổng xác định, thời điểm xác minh và kết quả đối soát cần thiết.
- Tách trạng thái khởi tạo URL khỏi trạng thái tiền thanh toán hiện có; không đổi ý nghĩa/giá trị số của enum cũ.
- Unique partial index cho `(OrderId, Purpose)` khi phiên đang hiệu lực và chưa bị xóa. Khóa đơn là lớp chính; index là lớp phòng vệ.
- Repository có method khóa/tìm phiên và cập nhật có điều kiện; không dùng in-memory lock hoặc chỉ cache.
- Coordinator dùng chung cho ba mục đích; gateway client chỉ ký/gọi cổng, không quyết định nghiệp vụ đơn hàng.
- Entity, enum, DTO, service interface, mapping và migration đặt đúng layer/thư mục hiện có; không gom nhiều namespace vào một file.
- Phản hồi giữ PaymentResultDto và các field cũ; bổ sung metadata như code/reused khi cần, không để lộ bí mật ký.

### 4.4 Hết hạn và dữ liệu đang có

- Không tự đặt thời hạn phiên theo một con số chưa có trong cấu hình/hợp đồng cổng. Không nhầm TTL URL với bằng chứng giao dịch không thể thu tiền.
- Không xóa hoặc tự đánh dấu Failed các giao dịch Pending cũ để ép migration/index chạy qua.
- Migration phải thống kê nhóm Pending trùng theo order/purpose trước. Nhóm chưa rõ trạng thái được đưa vào danh sách cần đối soát và chặn tạo phiên mới, không tự chọn một reference rồi bỏ phần còn lại.
- Chỉ cho tạo phiên thay thế khi có căn cứ phiên trước đã đóng/hết hiệu lực. Nếu adapter tra cứu/hủy cổng chưa có, trả trạng thái cần xác minh, không giả lập đã xác minh.
- Callback đến muộn của phiên cũ vẫn phải có đường ghi nhận, đối soát và xử lý tiền; không bỏ qua vì URL đã hết hạn tại BE.

## 5. Chặng B — callback và khoản tiền ngoài lần thanh toán chính

### 5.1 Phân biệt tiền cổng xác nhận với kết quả áp dụng vào đơn

1. Xác minh chữ ký, merchant, reference, số tiền và dữ liệu callback theo adapter đang dùng.
2. Khóa transaction theo reference rồi order, giữ cùng thứ tự với luồng hoàn tiền hiện có.
3. Callback trùng cùng giao dịch: trả acknowledgment/replay phù hợp, không ghi timeline, thu tiền, cộng phí hay gửi thông báo lần nữa.
4. Giao dịch hợp lệ đầu tiên: áp dụng vào đơn đúng mục đích trong cùng transaction.
5. Giao dịch khác đã thu thành công nhưng không thể áp dụng (đơn đã Paid, không còn nợ hoặc trạng thái không phù hợp): lưu bằng chứng nhận tiền và đánh dấu cần đối soát. Không để giao dịch tiếp tục Pending chỉ vì không thể đổi đơn.
6. Callback sai chữ ký không được biến thành bằng chứng đã nhận tiền. Sai tiền/reference được ghi cảnh báo phù hợp, không tự chốt đơn.

Trạng thái cần đối soát là trạng thái mới rõ nghĩa, không giả làm doanh thu đơn thành công. Nếu bổ sung enum, chỉ thêm giá trị mới, không đánh lại số của Pending/Succeeded/Failed/Refunded.

### 5.2 Báo cáo và hoàn tiền

- Trang chi tiết giao dịch/Admin có lý do cần đối soát và mã giao dịch cổng; dùng luồng cảnh báo/nhật ký hiện có.
- Không cộng khoản thu dư vào doanh thu tiền giặt, hoa hồng hoặc payout của Merchant; đồng thời vẫn thể hiện khoản tiền cần xử lý trong đối soát.
- Tận dụng RefundRequest, liên kết đúng `OriginalPaymentTransactionId`. Kiểm tra phần tiền đã hoàn trên từng giao dịch, không luôn chọn giao dịch thành công đầu tiên của đơn.
- Tạo lại yêu cầu đối soát/hoàn tiền phải idempotent; callback lặp không sinh hai yêu cầu.
- Đề xuất xử lý tiền dư qua quy trình Admin xác nhận hoàn tiền hiện có; không tự gọi API hoàn tiền VNPay/MoMo trong phạm vi plan này.
- Nếu tài liệu hiện hành chưa cho phép trường hợp tiền dư, cập nhật use case và hợp đồng API trước khi đánh dấu luồng đã hoàn thiện. Không tự đặt phí xử lý hoặc trừ phí hủy hai lần.

## 6. Chặng C — phân công an toàn với một Admin

### 6.1 Khóa thao tác trước khi kiểm tra replay

- Khi có operationId: lấy advisory transaction lock theo key thống nhất rồi mới đọc OrderOperation.
- Khóa phải dùng chung cho các đường cùng ghi bảng OrderOperation, tránh hai request dùng cùng key cho hai đơn khác nhau cùng vượt kiểm tra rồi lỗi unique constraint.
- Cùng key, actor, loại thao tác và payload chuẩn hóa: trả replay; không đổi đơn, capacity, nhiệm vụ, audit thành công hoặc notification thêm lần nữa.
- Cùng key nhưng actor/đơn/payload khác: `409 OperationIdReused`, không trả dữ liệu của người khác.
- Thực hiện kiểm tra quyền trước khi tiết lộ kết quả replay. Một Admin không có nghĩa có thể bỏ kiểm tra JWT/policy.
- Sau operation lock, lấy capacity lock và order lock; đọc lại điều kiện/revision dưới khóa rồi mới ghi.
- Batch dùng GroupId/item ID hiện có: khóa các key có thể ghi theo thứ tự ổn định; kiểm tra va chạm với thao tác đơn trước khi ghi.

### 6.2 Tương thích client

- Không âm thầm biến các field optional thành bắt buộc làm hỏng FE hiện tại.
- Có operationId thì bảo đảm replay an toàn; không có key vẫn phải bảo đảm không phân công trùng và trả lỗi nghiệp vụ rõ ràng.
- Có expectedRevision thì từ chối dữ liệu cũ bằng 409; không có revision không được quảng bá là đã chống mọi thao tác từ giao diện cũ.
- Fingerprint batch chuẩn hóa theo cặp OrderId/MerchantId và các field có ý nghĩa; không coi khác thứ tự mảng là thao tác khác nếu nghiệp vụ như nhau.

## 7. Chặng D — thứ tự khóa batch

Thứ tự khóa cho phân công:

1. Các key operation liên quan, sắp xếp ổn định nếu có nhiều key.
2. Capacity của các Merchant theo ID tăng dần.
3. Các OrderId theo ID tăng dần, không theo thứ tự người dùng chọn trên UI.
4. Đọc lại điều kiện và thực hiện thay đổi trong transaction hiện tại.

Có thể giữ thứ tự response như request để FE dễ ánh xạ; thứ tự khóa không phải thứ tự hiển thị.

- Request thất bại ở bất kỳ đơn nào: rollback toàn bộ đơn, audit nghiệp vụ, operation record và notification trong batch.
- Giữ cơ chế đẩy realtime sau commit hiện có; không gửi thành công trước commit.
- Rà các đường liên quan để tránh thêm chiều khóa ngược (order → capacity trong khi nơi khác capacity → order).
- Khóa ổn định không phải cam kết không thể deadlock toàn hệ thống. Nếu cần retry SQLSTATE 40P01/40001: retry hữu hạn toàn transaction với DbContext mới, không lặp HTTP cổng hoặc side effect bên ngoài; khi hết retry trả lỗi có thể thử lại. Không bắt mọi lỗi DB rồi trả thành công.

## 8. Ma trận kiểm thử bắt buộc

Tạo fixture riêng trên DB local/CI; dùng barrier hoặc khóa fixture để chứng minh request chồng lấn. Không chỉ gửi tuần tự rồi gọi là test race. Không dùng secret/cổng production trong CI.

| Mã | Tình huống | Kết quả phải chứng minh |
| --- | --- | --- |
| A01 | Hai request tạo URL tiền giặt cùng đơn/cổng | Một phiên hiệu lực, một reference; request sau tái sử dụng hoặc nhận đang khởi tạo rồi truy vấn lại cùng phiên |
| A02 | Hai request chọn VNPay và MoMo | Chỉ một phiên được mở; bên còn lại báo xung đột rõ ràng, không tạo URL cổng thứ hai |
| A03 | Bấm lại khi phiên đã có URL | Reference/URL không đổi, không gọi tạo cổng lần nữa |
| A04 | Gọi đồng thời tạo phí kho/phí hủy | Cùng nguyên tắc một phiên/mục đích, không tăng số tiền phải trả |
| A05 | MoMo nhận request nhưng response timeout | Reference giữ nguyên, trạng thái chưa xác định, không sinh giao dịch mới khi retry |
| A06 | IPN đến trước bước lưu URL | Tìm được giao dịch, kết quả Paid không bị bước lưu URL ghi lùi |
| A07 | Migration có Pending cũ trùng | Không mất giao dịch/tiền; có danh sách cần đối soát, chặn tạo phiên mới chưa rõ trạng thái |
| A08 | Caller sai chủ đơn hoặc không có JWT | 404/403/401 phù hợp; không lộ URL/reference, không mở phiên |
| B01 | Cùng callback gửi đồng thời nhiều lần | Một lần ghi tiền/trạng thái/timeline/notification |
| B02 | Hai reference khác nhau cùng báo thành công | Một lần áp dụng vào đơn; khoản còn lại có bản ghi cần đối soát, không tăng doanh thu Merchant |
| B03 | Callback sai chữ ký/sai số tiền | Không đánh Paid/không coi là bằng chứng thu tiền hợp lệ |
| B04 | Callback cũ đến muộn hoặc đơn đã đổi trạng thái | Không bỏ mất dấu tiền; áp dụng hoặc đối soát có lý do, không ép đơn về Paid sai luồng |
| B05 | Hoàn tiền khoản dư và retry xác nhận | Đúng giao dịch nguồn, không hoàn quá phần còn lại, không ghi hai lần |
| C01 | Một Admin gửi đồng thời cùng key/payload | Một thay đổi; request lặp nhận replay cùng kết quả nghiệp vụ |
| C02 | Cùng key nhưng khác đơn/payload | Một thành công và một 409; không lỗi 500 do PK trùng |
| C03 | Hai key khác nhau cùng phân công một đơn | Một kết quả phân công, bên thua lỗi nghiệp vụ/version; không hai timeline phân công |
| C04 | Đổi expectedRevision hoặc dùng key của actor khác | 409/403 phù hợp; không rò kết quả replay |
| D01 | Hai tab gửi batch thứ tự đơn ngược nhau, Merchant khác nhau | Không deadlock do thứ tự khóa; không đơn nào nhận hai Merchant |
| D02 | Một đơn trong batch không còn hợp lệ | Không ghi một phần batch hoặc hao capacity |
| D03 | Retry sau mất response nhưng transaction đã commit | Lấy lại operation cũ; không phân công lại |
| R01 | Hai Customer tranh ô cuối | Giữ PASS 5 lượt hiện có: một 201, một 400, đúng một đơn/ô/counter |
| R02 | Luồng giặt, hủy, nhận lại đồ, kho, gán Shipper | Không làm hỏng vòng đời và quy tắc tiền đang áp dụng |

Fake gateway trong test phải mô phỏng được delay, timeout và IPN đến sớm; đếm số lần gọi tạo URL. Kết quả fake không được báo là đã nghiệm thu gateway sandbox thật. Test sandbox thật là bước tích hợp riêng, có tài khoản test hợp lệ.

## 9. CI, triển khai và bàn giao

1. Viết test tái hiện lỗi trước khi sửa mỗi chặng; ghi nhận thất bại đúng nguyên nhân thay vì nới assertion.
2. Nối suite mới vào `tests/ci/Run-ApiSmoke.ps1`; rà cả test hiện có về phân công/QR/PIN trước khi bổ sung để tránh chạy trùng.
3. CI dùng PostgreSQL cô lập và gateway test; kiểm tra cả schema mới lẫn dữ liệu legacy có Pending trùng.
4. Artifact có tên scenario, mã HTTP, số lần gọi cổng và số dòng DB; không log JWT, PIN, hash secret hoặc khóa API.
5. Theo workflow hiện tại: chạy CI khi PR; không tự build Docker local trong đợt triển khai trừ khi người dùng yêu cầu.
6. Trước staging: backup DB, rà Pending cũ và áp migration; không xóa DB/volume hoặc chốt giao dịch giả để vượt kiểm tra.
7. Deploy theo nhánh Render thực tế đang theo dõi; nếu main thì merge develop → main sau CI, không coi merge develop là đã deploy.
8. Smoke staging bằng dữ liệu test: URL tái sử dụng, callback lặp, replay phân công. Bước cổng thật chỉ dùng sandbox chính thức và số tiền test.
9. Bàn giao FE mã lỗi/metadata mới, cách xử lý đang khởi tạo/đổi cổng và operationId. FE chống double-click là bổ trợ; BE vẫn an toàn nếu client gửi lặp.
10. Cập nhật use case, tiến độ và checklist theo kết quả thực tế. Ghi rõ chưa chạy/chờ CI/chưa nghiệm thu sandbox nếu còn thiếu.

## 10. Checklist nghiệm thu theo chặng

- [x] Rà source và thống nhất phạm vi một Admin.
- [x] Chạy thực nghiệm tranh ô cuối 5/5 lượt, dọn Customer/locker/đơn fixture; nối script vào runner CI (toàn bộ CI chờ PR chạy).
- [ ] A: test tái hiện tạo nhiều URL và callback đến sớm.
- [ ] A: coordinator/khóa DB/phiên bền trước cổng/DTO/migration hoàn tất.
- [ ] A: timeout và Pending legacy có hướng xử lý được kiểm thử.
- [ ] B: tiền nhận dư có dấu vết đối soát, báo cáo không tính sai doanh thu/hoa hồng.
- [ ] B: đường hoàn tiền đúng giao dịch và callback replay đạt test.
- [ ] C: khóa operation trước replay; không 500/không ghi hai lần khi request chồng lấn.
- [ ] D: batch khóa OrderId ổn định, xung đột rollback trọn lô.
- [ ] Test mới đã chạy trong CI, không chỉ có trên máy local.
- [ ] Backup/migration/deploy staging và test tích hợp sandbox cần thiết hoàn tất.
- [ ] Tài liệu API/use case/tiến độ/checklist đồng bộ, nhóm review và xác nhận nghiệm thu.

## 11. Những điểm chưa tự coi là đã chốt

- Thời hạn phiên và khả năng tra cứu/hủy phải đối chiếu cấu hình/adapter VNPay/MoMo khi thực hiện; không tự đặt TTL để mở lại thanh toán.
- Quy trình Admin xử lý tiền dư là phương án đề xuất tận dụng hoàn tiền hiện có; không mặc định đã có quyền tự động hoàn qua cổng.
- Không thêm mức phí mới hoặc mở rộng nhiều Admin. Các lựa chọn kỹ thuật về enum/tên helper được quyết định khi code, phải giữ tương thích và cập nhật hợp đồng API.

## 12. Tiến độ triển khai source — 15/11/2026

| Chặng | Source đã bổ sung | Trạng thái xác minh |
| --- | --- | --- |
| A | PaymentAppService.Sessions, trạng thái phiên, lưu reference trước HTTP, khóa đơn + unique index; gateway phân biệt kết quả cuối và chưa xác định | Chưa chạy test/build/migration |
| B | Receipt RequiresReview, queue Admin, RefundRequest tiền dư đúng giao dịch, loại khỏi doanh thu/hoa hồng/payout; callback replay | Chưa chạy test/build/migration |
| C | OrderOperationLocks dùng chung cho phân công, trạng thái, cân và giao nhận; snapshot kết quả replay | Chưa chạy test/build |
| D | Khóa Merchant và Order theo ID, response theo thứ tự request, kiểm tra key con của batch | Chưa chạy test/build |

- Migration: `20261115190000_PaymentSessionConcurrency`, có snapshot/Designer. Chưa áp DB.
- Test viết mới: `tests/smoke/Payments/Payments.csproj`, `tests/smoke/payment-assignment-concurrency.ps1`; đã nối runner CI, chưa chạy.
- Test phí hủy hiện có đã đổi kỳ vọng từ từ chối request lặp sang tái sử dụng phiên trước khi thanh toán.
- Chỉ rà source, kiểm tra cú pháp PowerShell và whitespace; không tuyên bố CI, sandbox hoặc hồi quy đã PASS.
- Không tự đóng phiên chưa rõ kết quả, không tự hoàn qua cổng; có queue và luồng Admin duyệt/xác nhận hoàn tiền dư. Tra cứu/hủy gateway và nghiệm thu sandbox là bước tích hợp riêng như mục 11.
