# Kế hoạch thanh toán gộp theo đơn

Đối chiếu yêu cầu ngày 24/11/2026 trong [BACKEND_PAYMENT.md](BACKEND_PAYMENT.md) với BE `develop` tại `92423a0` đã đối chiếu GitHub, và FE `main` local tại `de99851`. Hợp đồng BE đã triển khai được mô tả tại [PAYMENT_COMBINED_API_REVIEW.md](PAYMENT_COMBINED_API_REVIEW.md).

## Hiện trạng

| Yêu cầu | Source hiện tại và phần cần làm |
| --- | --- |
| Một bảng kê và một giao dịch | `PaymentAppService.Quotes.cs`, `PaymentAppService.Sessions.cs` và `PaymentHandler.cs` báo phí và tạo phiên theo purpose 1 tiền giặt, 2 phí lưu giữ, 3 phí hủy. Luồng phí lưu giữ riêng còn yêu cầu đã trả tiền giặt. Cần một bảng kê toàn đơn và phiên có nhiều khoản phân bổ. |
| Chống thu trùng | Có khóa dòng đơn khi tạo phiên và xác minh bất đồng bộ theo phiên. `BlockersAsync` và index `ux_payment_active_session` giới hạn theo đơn + purpose. Phải kiểm tra chéo giữa phiên gộp và mọi phiên cũ. |
| Ba trang kết quả | `PaymentAppService.Outcome.cs` hiện redirect tới 5 trang. FE local hiện đăng ký `/payment/return` và đã có cơ chế theo dõi xác minh để tái sử dụng. |
| Giao lại từ kho | `StorageAppService.RequestReturnAsync` kiểm tra báo phí và locker nhưng chưa kiểm tra thanh toán đủ tại bước ghi yêu cầu. Xuất kho và đặt đồ vào locker đã có kiểm tra tiền dịch vụ/phí kho riêng. |
| Doanh thu và điểm | Báo cáo, biên nhận, đối soát Merchant và logic hủy/hoàn đang đọc purpose và số tiền trên giao dịch. Giao dịch gộp cần đưa các luồng này về đúng khoản phân bổ. |

## Quy tắc nghiệp vụ dùng làm nền

- Hủy do khách theo `OrderCancellationPolicy`. Đơn chờ gửi trong hạn miễn phí. Chờ gửi quá hạn, đã gửi, đã gán Merchant hoặc Shipper lấy đồ dùng `TotalAmount` trước giảm. Đã thu gom và có cân thực tế dùng `FinalAmount` đã chốt. Lấy tỷ lệ từ chính sách theo đơn, hiển thị số tiền làm căn cứ, tỷ lệ và phí thực tế. Giữ luồng hủy Admin và quyết định miễn/hoàn riêng. Lưu căn cứ và tỷ lệ tại thời điểm hủy cho đơn mới. Với đơn cũ, trả phí đã ghi nhận và biểu thị trường căn cứ còn thiếu khi dữ liệu lịch sử không đủ.
- Giữ cách phân bổ hoàn tiền/điểm theo tỷ lệ đã dùng khi hủy. Phân biệt phần giá trị đã đối trừ vào phí hủy, tiền có thể hoàn, yêu cầu đang xử lý và đã hoàn thực tế. Trừ phần đã hoàn hoặc đã dành cho yêu cầu hoàn khi xác định khoản có thể hoàn tiếp. Hạn chế thay đổi luồng hoàn hiện có trong đợt này.
- Phí lưu giữ lấy từ `OverdueFeeCalculator.GetTotalStorageDue` và chính sách theo đơn. Dùng tổng lũy kế đã chốt cộng phần đang phát sinh rồi trừ `StorageFeeTotal`. Chốt phần phí kho tại lúc xuất kho theo luồng hiện tại. Lịch sử từng vòng dùng để tra cứu.
- Giữ giảm giá của đơn trong tiền dịch vụ đã chốt. Điểm chỉ đổi/trả thưởng trên phần tiền dịch vụ đủ điều kiện, giữ giới hạn và quy tắc hoàn hiện hành. Phần tiền lưu giữ và phí hủy chịu nguyên mức theo chính sách.
- Đã chốt với nhóm. Khi đơn hủy đã thu tiền dịch vụ và còn phí lưu giữ, chỉ cấn tiền dịch vụ vào phí hủy. Phần dư đi theo quy trình hoàn. Phí lưu giữ hợp lệ còn thiếu xuất hiện trong bảng kê.

## Chặng 1 — Bảng kê có thẩm quyền từ BE

1. Thêm bộ tính nghĩa vụ dùng chung cho bảng kê, mở phiên, xác minh hoàn tất và các bước giao lại. Nhận một mốc thời gian tính phí nhất quán. Tái sử dụng logic phí, hủy, ưu đãi và điểm hiện có.
2. Đề xuất `GET /payment/orders/{orderId}/summary?pointsToRedeem=0`. Trả mã/trạng thái đơn, thời điểm tính, từng khoản phải thu/đã trả/còn thiếu, VND, số trước và sau trừ điểm, thông tin hoàn/đối soát, phiên đang hoạt động, phương thức khả dụng, điều kiện giao lại, mã báo phí và hạn hết hiệu lực.
3. Đơn đang xử lý thu phần dịch vụ hợp lệ và phí lưu giữ đã phát sinh. Đơn hủy chỉ thu phí hủy và phí lưu giữ hợp lệ. Tách khoản chưa đến lúc thu khỏi trạng thái đã trả đủ, kể cả khi bảng kê trả tổng thu lúc này bằng 0.
4. Xác thực Customer sở hữu đơn. List/chi tiết/kho/checkout dùng cùng nguồn tính, đọc theo lô nếu cần đưa số dư vào danh sách để tránh truy vấn lặp theo từng đơn. Báo phí không yêu cầu chọn lại locker.

## Chặng 2 — Một phiên cổng và phân bổ nguyên tử

1. Đề xuất `POST /payment/orders/{orderId}/checkout` nhận `paymentMethod`, `quoteToken` và số điểm đã chọn. Thêm định danh phiên gộp riêng, đề xuất `purpose=4`; các mục phân bổ vẫn là dịch vụ, lưu giữ, hủy. Giữ hợp đồng lịch sử của 1/2/3 cho đến khi FE chuyển xong.
2. Thêm migration lưu các dòng phân bổ bất biến gắn với giao dịch, gồm khoản, tiền mặt, điểm, mã báo phí và dữ liệu cần đối soát. Dữ liệu cũ đọc theo purpose hiện có. Không nhân bản giao dịch cũ thành giao dịch mới.
3. Dùng khóa dòng đơn sẵn có. Khi tạo gộp kiểm tra phiên 1/2/3 còn chờ hoặc đang đối soát. Tất cả route tạo phiên cũ kiểm tra ngược phiên gộp. Xử lý dữ liệu cũ có nhiều phiên chờ trước khi cân nhắc siết index theo toàn đơn. Bảo toàn thứ tự khóa giao dịch → đơn ở đường hoàn tất.
4. Mã báo phí phải bắt buộc, gắn chủ đơn, trạng thái, từng dòng tiền/điểm và thời hạn. Nếu hết hạn hoặc thay đổi, trả `409` với bảng kê mới. Tổng cần thu bằng 0 thì trả trạng thái nghĩa vụ/xác minh và bỏ qua mở cổng. Kiểm tra hạn mức cổng trên số tiền thực thu.
5. Tạo một reference, một giao dịch cổng và giữ điểm theo quy trình hiện tại. Sau khi IPN hoặc truy vấn cổng xác minh, rà tất cả phân bổ trước rồi ghi thu, số dư, điểm, timeline và trạng thái trong cùng transaction. Callback lặp trả kết quả đã ghi. Phí mới chỉ làm tăng số dư hiện tại. Trường hợp tiền thu về không còn áp dụng được đúng khoản đưa vào đối soát.
6. Rà báo cáo Admin/Merchant, hoa hồng, đối soát kỳ, biên nhận, hoàn/hủy và điểm. Các phép tính dùng phần tiền dịch vụ, kho hoặc hủy tương ứng; đếm mỗi lần thu cổng một lần. Đặc biệt điểm thưởng/đảo điểm dùng tiền mặt phần dịch vụ; chia hoàn dựa trên khoản thu đủ điều kiện và lịch sử hoàn. Hoàn thành mục này trước khi mở checkout gộp cho FE.

## Chặng 3 — Kết quả FE và điều kiện giao lại

1. Dùng chung redirect cho VNPay/MoMo. Chỉ chọn `/payment/success`, `/payment/failed`, `/payment/cancelled` khi kết quả có căn cứ xác minh tương ứng. Đang chờ hoặc cần hỗ trợ quay về `/orders/{orderId}` với định danh phiên nếu đã xác thực và tìm được giao dịch. Khi thiếu chữ ký hợp lệ hoặc không xác định được đơn, quay về `/orders` và không đưa định danh trên query.
2. Mở rộng API kết quả để FE thấy phân bổ đã ghi, số dư toàn đơn hiện tại, hoàn/đối soát và quyền giao lại. Phân biệt giao dịch thành công với toàn đơn đã trả đủ. FE đọc API theo đơn/phiên và tái sử dụng cơ chế theo dõi xác minh. Phối hợp thời điểm deploy để FE có đủ ba route trước khi BE redirect.
3. Kiểm tra lại bảng kê trong API xác nhận yêu cầu giao lại, xuất kho và đặt đồ vào locker. Trả lý do và bảng kê mới khi còn thiếu. Giữ locker từ yêu cầu/đơn khi còn hợp lệ. Bước xác nhận giao lại vẫn chỉ chạy sau hành động của khách và các bước bàn giao theo workflow kho.

## Chặng 4 — Nghiệm thu và bàn giao

- Nối hồi quy vào CI hiện có. Kiểm tra dịch vụ + kho một lần, kho sau khi đã trả dịch vụ, hủy có/không kho, hủy sau khi trả có phần hoàn, nhiều vòng và phí bắt đầu phát sinh sau báo phí.
- Kiểm tra VNPay/MoMo: thành công, thất bại, hủy, chờ và đối soát quay về đúng nơi; thiếu chữ ký; IPN và truy vấn đến đồng thời; callback lặp; đổi phương thức; hai tab mở phiên cũ/gộp đồng thời; timeout tạo URL. Kiểm tra quyền sở hữu đơn và phiên.
- Kiểm tra tổng bằng 0, hạn mức cổng, hoàn/điểm và báo cáo khớp phân bổ, chặn yêu cầu giao lại trực tiếp qua API, luồng cũ 1/2/3 và migration có giao dịch cũ đang chờ. Chạy sandbox chính thức cho hai cổng khi có dữ liệu test hợp lệ.
- Bàn giao endpoint/DTO và ví dụ trước khi FE đổi checkout. Trong lúc BE và FE chưa đồng bộ, triển khai tương thích trước rồi bật chức năng sau khi API và các route đã có.

