# Nội dung cập nhật đặc tả và điều khoản — dự thảo 14/11/2026

Đây là phần thay thế/bổ sung để nhóm đưa vào Word và phiên bản điều khoản mới. Chưa sửa file Word gốc, chưa publish policy đang áp dụng. Giữ số UC hiện có, không thêm bản sao các mục 1.3.1.

## Đặc tả: chuyển đồ quá hạn từ locker về khu lưu tại tiệm

Kho là khu lưu đồ tại tiệm Merchant đang xử lý đơn; không quản lý một danh mục kho riêng. Merchant phân công Shipper của mình; Admin có thể điều phối với lý do và audit. Khi phân công, ô tủ vẫn có đồ. Shipper xác thực mã nhiệm vụ và xác nhận lấy đồ: hệ thống giải phóng ô, chốt phí tại locker, ghi trạng thái đang chuyển về tiệm. Shipper xác nhận bàn giao tại tiệm kèm mã biên nhận: hệ thống lưu thời gian/người giao/biên nhận, hoàn tất nhiệm vụ, chuyển đơn sang lưu tại tiệm và bắt đầu tính phí kho.

SLA chuyển về tiệm là 4 giờ từ phân công đầu đến bàn giao. Trễ thì cảnh báo và ghi lịch sử, vẫn cho thực hiện. Phân công lại không đặt lại SLA. Sau khi đã lấy đồ, không hủy hoặc phân công lại bằng thao tác thường; ghi sự cố và giữ lịch sử người đang giữ hàng để điều phối xử lý.

Đồ giặt xong chưa thanh toán quá 7 ngày đã nằm tại tiệm thì hệ thống ghi nhận bắt đầu lưu kho ngay tại đó, không tạo chuyến lấy từ locker hoặc biên nhận Shipper giả.

## Đặc tả: yêu cầu giao lại và nhận đồ

Customer là chủ đơn chọn locker đang được tiệm phục vụ và xác nhận báo phí từ server. Một yêu cầu hiệu lực cho mỗi vòng lưu kho. Không thu thêm phí giao lại trong phạm vi này. Tiền giặt và phí lưu kho phải thanh toán đủ trước lúc xuất kho. Phí tiếp tục phát sinh đến thời điểm xuất kho thực tế; nếu tăng so với báo phí khách đã chấp thuận thì phải lấy/xác nhận báo phí mới và thanh toán phần thiếu trước khi xuất.

Merchant phân công riêng chuyến giao lại. SLA là 24 giờ từ yêu cầu hợp lệ đến lúc đặt đồ vào locker, gồm thời gian chờ phân công. Shipper xác nhận xuất kho để dừng phí kho và ghi đang vận chuyển. Tới locker, hệ thống chọn ô trống cùng/lớn hơn ô gửi trong transaction. Hết ô thì giữ nguyên trạng thái và người giữ hàng, cho thử lại/báo sự cố; không tự chọn trạm khác.

Chỉ sau đặt đồ thành công mới phát mã nhận mới, hủy mã/session cũ và bắt đầu 24 giờ nhận miễn phí mới. Customer mở ô bằng QR/PIN rồi xác nhận đã lấy hết đồ và đóng cửa; sau đó mới giải phóng ô và tự hoàn tất đơn. Đây là mô phỏng, việc khách lấy đồ dựa vào xác nhận của khách, không phải bằng chứng cảm biến.

## Đặc tả: phí và lịch sử nhiều vòng

Phí locker chốt tại lúc Shipper lấy đồ; phí kho tính từ lúc bàn giao tại tiệm đến lúc xuất kho. Phí kho mặc định 100.000đ cho mỗi chu kỳ 24 giờ bắt đầu, làm tròn lên; ngay thời điểm vào kho chưa phát sinh phần phí kho. Không tính thời gian vận chuyển thành thời gian lưu kho. Tổng phí gồm các chặng/vòng đã chốt cộng chặng hiện tại, trừ khoản đã thanh toán. Giữ chính sách snapshot của đơn, không áp giá mới ngược về quá khứ.

## Đặc tả: Shipper tự đổi trạng thái

Shipper được đổi Available/Offline cho chính mình. Offline không khóa tài khoản nhưng ngăn phân công mới. Không được Offline khi còn nhiệm vụ hiệu lực; không được tự đặt Busy hoặc ghi đè nhân sự inactive/Busy do quản lý kiểm soát. Request có version và operationId, dùng chung khóa với phân công để tránh hai phiên làm lệch trạng thái.

## Điều khoản Customer: đoạn đề nghị thay phần lưu kho/nhận tại kho

Đồ quá hạn nhận tại locker có thể được chuyển về khu lưu đồ tại tiệm đang xử lý đơn. Phí tại locker được chốt khi nhân viên xác nhận lấy đồ khỏi tủ. Phí lưu kho bắt đầu khi bàn giao tại tiệm và dừng khi xác nhận xuất kho để giao lại; thời gian vận chuyển không tính phí lưu kho. Mức mặc định là 100.000đ cho mỗi chu kỳ 24 giờ bắt đầu, theo chính sách áp dụng cho đơn.

Bạn yêu cầu giao lại đến locker trong phạm vi phục vụ và xác nhận báo phí trên ứng dụng. Hiện không thu thêm phí giao lại; bạn cần thanh toán đủ tiền giặt và phí còn thiếu trước khi xuất kho. Khi có phí mới phát sinh, hệ thống yêu cầu bạn xác nhận lại báo phí. Bạn có thể hủy yêu cầu trước lúc xuất kho; hủy yêu cầu không tự hoàn hoặc miễn phí đã thu.

Sau khi đồ được đặt lại vào locker thành công, hệ thống gửi mã nhận mới và thời hạn nhận miễn phí mới 24 giờ. Nếu tiếp tục quá hạn, phí của các vòng trước vẫn được giữ và phí vòng mới tính theo chính sách của đơn. Bạn nhận đồ tại locker bằng QR/PIN và xác nhận đã lấy hết đồ/đóng cửa trên giao diện mô phỏng.

Nhóm cần duyệt và phát hành phiên bản điều khoản qua chức năng Admin hiện có; không coi việc thêm file dự thảo là đã thông báo/được khách chấp thuận. Với đơn legacy, Admin phải đối chiếu vị trí thực tế trước khi chuyển sang giao lại; không sửa lịch sử hay số tiền chỉ để hợp thức hóa quy trình mới.
