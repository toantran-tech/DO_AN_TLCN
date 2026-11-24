# Yêu cầu BE — Thanh toán gộp các khoản còn thiếu của đơn hàng

Ngày 24/11/2026. Thay thế toàn bộ nội dung cũ. Đây là yêu cầu cần triển khai/đối chiếu, không phải xác nhận API thanh toán gộp đã có. Chỉ cập nhật tài liệu, không sửa BE hoặc giao dịch thật.

## 1. Mục tiêu

Khách xem một bảng kê và thanh toán một tổng tiền còn thiếu của đơn qua VNPay hoặc MoMo. Không bắt khách trả tiền dịch vụ rồi tạo thêm giao dịch riêng cho phí lưu giữ/phí hủy.

Danh sách đơn, chi tiết đơn, phần đồ được lưu giữ và màn hình thanh toán phải dùng cùng bảng kê có thẩm quyền từ BE. FE không tự tính phí hay cộng lịch sử các vòng lưu giữ.

## 2. Quy tắc cho ba khoản tiền

| Khoản | Quy tắc |
| --- | --- |
| Tiền dịch vụ | Đơn tiếp tục xử lý: thu phần tiền dịch vụ hợp lệ còn thiếu; đã trả thì không thu lại. |
| Phí quá hạn/lưu giữ | Thu phần phí hợp lệ đã phát sinh nhưng chưa trả, theo chính sách BE; không cộng trùng tổng nghĩa vụ và lịch sử từng vòng. |
| Phí hủy | Tính theo tỷ lệ phần trăm của giá trị dịch vụ đã chọn, theo chính sách hủy. Khi hủy, không cộng thêm toàn bộ tiền dịch vụ như một khoản phải thu nữa. |

### Đơn tiếp tục xử lý hoặc đã chuyển lưu kho

**Tổng còn thiếu = tiền dịch vụ còn thiếu + phí quá hạn/lưu giữ còn thiếu.**

Ví dụ đơn chưa trả tiền dịch vụ, quá 7 ngày chuyển lưu kho, BE xác định phí lưu giữ 100.000 đ:

| Bảng kê | Số tiền |
| --- | ---: |
| Tiền dịch vụ còn thiếu | 130.000 đ |
| Phí quá hạn/lưu giữ còn thiếu | 100.000 đ |
| **Tổng cần thanh toán** | **230.000 đ** |

Nếu dịch vụ đã thanh toán đủ thì chỉ thu 100.000 đ phí lưu giữ. Các mốc 7 ngày và 100.000 đ là ví dụ theo tình huống trao đổi; số tiền/mốc thực tế phải lấy từ chính sách và dữ liệu BE, không hardcode trên FE.

### Đơn bị hủy

**Phí hủy = giá trị dịch vụ làm căn cứ × tỷ lệ phí hủy áp dụng.**

**Tổng còn thiếu = phí hủy còn thiếu + phí quá hạn/lưu giữ còn thiếu nếu có phát sinh hợp lệ.** Không cộng thêm toàn bộ tiền dịch vụ của đơn hủy.

Ví dụ giá trị làm căn cứ 200.000 đ, phí hủy 20% → 40.000 đ, không phải 240.000 đ. Không có phí lưu giữ và chưa trả khoản nào thì chỉ thanh toán 40.000 đ.

BE cần chốt và trả rõ căn cứ tính phần trăm là trước hay sau giảm giá, những dịch vụ được tính, tỷ lệ và phí hủy cuối cùng. FE chỉ hiển thị, không tự chọn căn cứ/tỷ lệ.

### Đã thanh toán trước khi hủy

- BE tính lại nghĩa vụ theo chính sách hủy, đối chiếu tiền đã thu, đã hoàn và đang chờ hoàn/xác minh.
- Nếu chính sách cho đối trừ tiền dịch vụ đã thu vào phí hủy, ghi nhận phân bổ/điều chỉnh minh bạch, giữ lịch sử giao dịch cũ; không yêu cầu trả lại khoản đã đối trừ.
- Trả riêng tổng còn phải thu và số tiền đủ điều kiện hoàn/cần xử lý hoàn. Tổng cần thanh toán không âm.
- Đối trừ hoặc tạo yêu cầu hoàn không đồng nghĩa cổng đã hoàn thành công. Trạng thái hoàn phải theo dõi riêng, theo kết quả thực tế.

## 3. Bảng kê thanh toán toàn đơn do BE trả

Đề xuất mở rộng/bổ sung API báo phí gộp. BE bàn giao endpoint/DTO cuối cùng cho FE, gồm:

- ID/mã đơn, trạng thái đơn, thời điểm tính phí tại server.
- Với từng khoản dịch vụ/lưu giữ/hủy: nghĩa vụ, tiền đã trả hoặc được phân bổ, số còn thiếu; căn cứ/tỷ lệ phí hủy khi áp dụng.
- Tổng cần thanh toán, đơn vị tiền, thông tin đối trừ và hoàn tiền nếu có.
- Mã báo phí/phiên bản và thời hạn hiệu lực để kiểm tra thay đổi trước khi tạo giao dịch.
- Phiên thanh toán đang hoạt động, khả năng tiếp tục/xác minh/tạo lại và khả năng yêu cầu giao lại đồ.
- Nếu có điểm/ưu đãi: khoản nào được giảm, số giảm, tổng thực trả và chính sách hoàn điểm. Không mặc định áp dụng giảm tiền dịch vụ cho phí lưu giữ/phí hủy.

API tổng hợp phải loại trừ trùng lặp giữa tổng nghĩa vụ hiện tại và các dòng lịch sử. Không yêu cầu FE cộng dồn lịch sử để tính tổng phải trả.

## 4. Một giao dịch gộp và phân bổ đúng từng khoản

1. FE đọc bảng kê toàn đơn, hiển thị chi tiết và một nút Thanh toán tổng còn thiếu.
2. Khách chọn VNPay/MoMo; FE gửi mã báo phí/phiên bản, không gửi số tiền tự tính làm nguồn quyết định.
3. BE kiểm tra lại trạng thái và báo phí. Nếu thay đổi, trả bảng kê mới để khách xem lại; không âm thầm thu số khác với số đã xác nhận.
4. Một giao dịch cổng mang tổng cần thu và có chi tiết phân bổ cho từng nghĩa vụ; chốt cách phân bổ ở BE.
5. Sau khi IPN hoặc truy vấn cổng xác minh thành công, BE ghi nhận tiền thu và các phân bổ đồng bộ trong một giao dịch dữ liệu. Không ghi nhận một phần rồi báo thành công toàn bộ.
6. Giữ idempotency, guard phiên đang hoạt động và phục hồi kết quả chưa xác định. Callback trùng, tải lại hoặc nhiều tab không gây thu/phân bổ trùng.

Khi tổng bằng 0, không tạo URL cổng. BE trả trạng thái đã hoàn tất nghĩa vụ hoặc còn chờ xác minh phù hợp. Các khoản cũ đã thanh toán theo purpose riêng vẫn phải được đối chiếu, không thu lại.

## 5. Phí mới phát sinh khi đang thanh toán

- Chốt số tiền và phân bổ của phiên theo bảng kê lúc tạo; không sửa số tiền phiên đã gửi cổng.
- Nghĩa vụ mới phát sinh hợp lệ trở thành số dư còn thiếu mới. Tiền thanh toán thành công của phiên cũ vẫn phải được ghi nhận đúng.
- Kết quả phải phân biệt giao dịch này thành công và toàn đơn hiện đã trả đủ hay chưa.
- Nếu thay đổi trạng thái đơn khiến tiền thu không thể áp dụng đúng bảng kê, đưa vào đối soát; không tự đổi mục đích thu hoặc báo đã trả đủ.

## 6. Đồng bộ giao diện và điều kiện giao lại

- Mọi màn hình dùng một bảng kê, một nút Thanh toán tổng còn thiếu; chỉ hiển thị các khoản thực tế áp dụng, không bắt chọn loại phí.
- Với đơn lưu giữ, dùng locker nhận lại của đơn/yêu cầu hợp lệ; không bắt chọn lại locker chỉ để xem/thanh toán phí.
- Chỉ cho xác nhận yêu cầu giao lại khi BE xác nhận đã trả đủ các nghĩa vụ bắt buộc và đủ điều kiện giao lại khác. Kiểm tra tại API ghi, không chỉ khóa nút FE.
- Kiểm tra lại số dư khi xác nhận yêu cầu và xuất/giao lại theo chính sách. Nếu còn phí mới, trả bảng kê và lý do rõ ràng.
- Thanh toán không tự xác nhận xuất kho/giao đồ/hoàn thành đơn; các bước giữ và bàn giao đồ vẫn theo workflow hiện tại.

## 7. Kết quả thống nhất cho VNPay và MoMo

VNPay và MoMo chỉ dùng **ba trạng thái kết quả cuối cùng** và ba URL FE tương ứng: `success`, `failed`, `cancelled`. Không triển khai trang kết quả riêng `/payment/pending` hoặc `/payment/review`.

| Trang | Ý nghĩa |
| --- | --- |
| `/payment/success` | Giao dịch đã xác minh và phân bổ hợp lệ; API trả thêm tổng còn thiếu hiện tại. |
| `/payment/failed` | Thất bại đã xác định, không ghi nhận tiền đã trả. |
| `/payment/cancelled` | Khách hủy tại cổng, kết quả đã xác minh. |

BE chỉ chọn một trong ba kết quả trên khi đã có căn cứ xác minh. Gián đoạn, IPN chưa đến hoặc cần đối soát không được tự gán thành `failed`, `cancelled` hay `success`. Các trạng thái xử lý nội bộ này vẫn được BE quản lý; trình duyệt quay về chi tiết đơn với thông báo đang xác minh/cần hỗ trợ, không mở thêm trang kết quả thứ tư và không cho tạo giao dịch trùng. Khi xác minh hoàn tất, FE đọc lại kết quả và hiển thị một trong ba trạng thái cuối cùng phù hợp.

URL chỉ mang định danh an toàn của đơn/phiên; không lấy query cổng làm bằng chứng đã trả. API kết quả trả trạng thái giao dịch, các khoản đã phân bổ, số dư hiện tại, trạng thái hoàn/đối soát và khả năng giao lại.

BE bàn giao mục đích/định danh phiên gộp và cách tương thích purpose 1/2/3 cũ. FE không tự gán phiên gộp vào một purpose cũ làm mất phân bổ. Mọi API Customer phải kiểm tra quyền sở hữu đơn và phiên thanh toán.

## 8. Tiêu chí nghiệm thu

- Chưa trả dịch vụ + phí lưu giữ: thanh toán một lần đủ cả hai.
- Đã trả dịch vụ + phí lưu giữ: chỉ thu phí còn thiếu.
- Đơn hủy chưa trả: phí hủy theo phần trăm và phí lưu giữ hợp lệ nếu có; không thu thêm toàn bộ dịch vụ.
- Đã trả trước khi hủy: đối trừ/hoàn theo chính sách, không thu trùng, không báo hoàn thành công khi mới tạo yêu cầu.
- Thanh toán một phần/nhiều vòng lưu giữ: số dư đúng, không cộng trùng lịch sử.
- Phí thay đổi sau báo phí/trong khi thanh toán: không sửa số tiền phiên cũ, hiển thị số dư mới.
- VNPay/MoMo thống nhất ba trạng thái kết quả `success`, `failed`, `cancelled`; không có trang pending/review. Gián đoạn/cần đối soát được theo dõi tại đơn cho đến khi có kết luận, không biến thành thất bại giả; callback trùng không thu/phân bổ trùng.
- Không thể gọi trực tiếp API yêu cầu giao lại để vượt qua điều kiện trả đủ.
- Phiên cũ theo từng purpose và phiên mới gộp không gây thu trùng hoặc mất lịch sử; phiên chưa rõ kết quả phải được xử lý trước khi tạo lại.

## 9. BE cần phản hồi trước khi tích hợp FE

1. Căn cứ/tỷ lệ phí hủy và chính sách đối trừ/hoàn khi đã trả dịch vụ.
2. Quy tắc phát sinh/chốt phí lưu giữ và thời điểm dừng tính phí sau yêu cầu giao lại.
3. Endpoint/DTO báo phí, tạo giao dịch, kết quả/phân bổ và tương thích phiên cũ.
4. Chính sách điểm/ưu đãi/hoàn điểm cho từng khoản nếu có.

Nguyên tắc gộp và cách hiển thị đã chốt; những chi tiết chính sách chưa rõ cần BE xác nhận, không để FE tự suy đoán.
