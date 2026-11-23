# Merchant quản lý giới hạn nhiệm vụ của từng Shipper

Yêu cầu thống nhất ngày 22/11/2026. Nội dung này thay thế toàn bộ tài liệu cũ; đây là yêu cầu cần triển khai, không phải mô tả tính năng đã hoàn thành.

Chốt bổ sung ngày 23/11/2026: giới hạn riêng **1–20 nhiệm vụ**, tạo mới mặc định **3**. Word hiện còn quy tắc một chuyến/Shipper và chưa được cập nhật; triển khai đợt này theo yêu cầu MD này.

## 1. Phạm vi và phân quyền

- Merchant quản lý **Số nhiệm vụ tối đa** cho từng Shipper trong trang Quản lý Shipper, khi tạo hoặc chỉnh sửa hồ sơ.
- Merchant chỉ được chỉnh Shipper thuộc cửa hàng mình. BE xác định Merchant từ phiên đăng nhập, không tin merchantId do FE gửi.
- Admin không có chức năng cấu hình hoặc chỉnh giới hạn này, kể cả cấu hình điều phối trong Cài đặt hệ thống.
- Shipper được xem giới hạn và số nhiệm vụ của mình, không tự chỉnh giới hạn.
- Chuyển từ một giới hạn chung của hệ thống sang giới hạn riêng lưu trên từng Shipper. Giới hạn đang áp dụng phải do BE trả, FE không tự đặt mặc định hoặc suy ra từ trạng thái Sẵn sàng.

## 2. Quy tắc thay đổi giới hạn

- Số nhiệm vụ tối đa là số nguyên từ **1 đến 20**, bao gồm hai đầu mút; không dùng 0/null để biểu thị không giới hạn.
- Tên trường thống nhất: `maxActiveTasks`. Merchant tạo mới không gửi trường này thì BE áp dụng **3**; khi sửa không gửi thì giữ giới hạn cũ. Gửi null tường minh, số thập phân hoặc ngoài khoảng bị từ chối.
- Admin vẫn tạo được hồ sơ Shipper theo chức năng hiện hành nhưng BE tự khởi tạo giới hạn **3**; Admin không được gửi/chỉnh `maxActiveTasks`, kể cả khi tạo qua trang quản lý tài khoản. Các quyền quản lý trường hồ sơ khác giữ nguyên.
- **Không cho lưu giới hạn thấp hơn số nhiệm vụ chưa hoàn tất tại thời điểm lưu.**
- Cho phép giới hạn **bằng** số nhiệm vụ chưa hoàn tất. Khi đó Shipper đã đầy và không được nhận thêm nhiệm vụ mới.
- Không tự hủy, chuyển người hoặc kết thúc nhiệm vụ để hợp thức hóa việc giảm giới hạn.

Ví dụ Shipper đang có 3 nhiệm vụ chưa hoàn tất:

| Giới hạn muốn lưu | Xử lý |
| --- | --- |
| 2 | Chặn lưu, giữ nguyên giới hạn cũ |
| 3 | Cho lưu; không nhận thêm nhiệm vụ mới |
| 4 trở lên | Cho lưu nếu nằm trong khoảng cho phép; còn chỗ nhận thêm |

Thông báo khi bị chặn: **“Shipper đang có 3 nhiệm vụ chưa hoàn tất. Số nhiệm vụ tối đa phải từ 3 trở lên.”** Số nhiệm vụ trong thông báo lấy từ kết quả kiểm tra mới nhất của BE.

## 3. Cách tính nhiệm vụ và phân công

- Dùng cùng quy tắc đếm nhiệm vụ chưa hoàn tất cho quản lý Shipper, hồ sơ, đơn thường, trả đồ đơn hủy, chuyển lưu giữ và giao lại từ lưu giữ.
- Không tính lịch sử đã hoàn tất/hủy/thay thế; không cộng trùng đơn và task.
- Hoàn tất chuyến tương ứng giải phóng chỗ, không phải chờ cả đơn giặt hoàn thành.
- Shipper được nhận thêm khi còn dưới giới hạn và đáp ứng các điều kiện tài khoản, cùng Merchant, tình trạng làm việc và trạng thái vận hành hiện hành.
- Phân công lại cùng chuyến cho cùng người hoặc replay cùng operation không chiếm thêm một chỗ. BE quyết định canAssign cho chuyến đang xét.
- Không tự đổi trạng thái vận hành khi số nhiệm vụ tăng. Sẵn sàng không đồng nghĩa với còn chỗ nhận.

## 4. BE cần triển khai

### Lưu cấu hình riêng và bảo vệ quyền chỉnh

- Bổ sung giới hạn riêng trên Shipper và hỗ trợ trong API tạo/chỉnh sửa của Merchant. Tên trường có thể giữ `maxActiveTasks` hoặc tên thống nhất trong hợp đồng bàn giao.
- Kiểm tra quyền sở hữu và giá trị phía server. Chặn việc chỉnh trường này qua endpoint Admin hoặc endpoint khác không được phép, không chỉ ẩn nút trên FE.
- Giữ kiểm tra phiên bản hồ sơ, cơ chế chống gửi trùng/tra cứu kết quả và audit phù hợp với API hiện hành.
- Di chuyển dữ liệu hiện có từ giới hạn chung sang giới hạn riêng mà không làm mất nhiệm vụ hoặc tự đổi tải công việc. Bàn giao rõ quy tắc khởi tạo cho Shipper mới.
- Khởi tạo dữ liệu cũ bằng giá trị lớn nhất giữa giới hạn chung hiện tại và số nhiệm vụ chưa hoàn tất của từng Shipper. Nếu kết quả ngoài 1–20, phải dừng và báo ngoại lệ để xử lý trước migration; không tự cắt giới hạn, nâng trần hoặc hủy nhiệm vụ.
- Ngừng dùng cấu hình dispatch chung làm nguồn quyết định giới hạn. Cập nhật hoặc ngừng cho phép ghi giới hạn chung tại endpoint cũ, tránh tồn tại hai nơi cùng điều khiển năng lực.

### Kiểm tra nguyên tử khi lưu và phân công

- Kiểm tra lại số nhiệm vụ ngay lúc lưu, không chỉ dùng số mà FE đã tải khi mở form.
- Đổi giới hạn và phân công nhiệm vụ phải dùng cùng cơ chế khóa/serialize theo Shipper trong transaction. Không để hai thao tác cùng thành công khiến số nhiệm vụ vượt giới hạn vừa lưu.
- Ví dụ Merchant mở form lúc có 2 nhiệm vụ, chọn giới hạn 2 nhưng trước khi lưu đã có nhiệm vụ thứ 3: BE phải chặn lưu giới hạn 2 và trả số liệu mới nhất.
- Khi bị chặn, không ghi giới hạn mới hoặc ghi một phần các thay đổi khác trong cùng yêu cầu.
- Đề xuất HTTP 409 với mã ổn định như `ShipperTaskLimitBelowActiveCount`, kèm `activeTaskCount`, `maxActiveTasks` hiện hành và giới hạn tối thiểu có thể lưu. Tên mã/trường cuối cùng cần được ghi trong hợp đồng BE.
- Giữ lỗi thiếu quyền, sai Merchant, phiên bản cũ và vượt năng lực phân công riêng biệt, không dùng chung một mã lỗi.

### Dữ liệu đọc

Danh sách/chi tiết quản lý Shipper, hồ sơ Shipper và các danh sách phân công cần trả thống nhất:

| Trường | Ý nghĩa |
| --- | --- |
| `activeTaskCount` | Số nhiệm vụ chưa hoàn tất |
| `maxActiveTasks` | Giới hạn riêng đang áp dụng cho Shipper |
| `remainingTaskCapacity` | Số chỗ còn nhận, không âm |
| `canAssign` | Có thể nhận chuyến đang xét hay không, trên API phân công |
| `blockedReasonCode` | Lý do không chọn được, trên API phân công |

Giữ các kiểm tra năng lực tại mọi endpoint phân công và trả lỗi khi năng lực thay đổi giữa lúc chọn và lúc xác nhận. FE không quyết định thay BE bằng phép đếm cục bộ.

## 5. FE dự kiến

- Gỡ nhóm Điều phối Shipper khỏi Cài đặt hệ thống Admin.
- Thêm trường Số nhiệm vụ tối đa trong form tạo/chỉnh sửa Shipper của Merchant.
- Hiển thị tải công việc gọn ở danh sách và chi tiết, ví dụ **Đang nhận: 2/5 nhiệm vụ**; Shipper xem được số liệu tương ứng trong hồ sơ.
- Kiểm tra sơ bộ theo số nhiệm vụ đã tải để báo lỗi sớm; BE vẫn kiểm tra lại khi lưu.
- Khi BE chặn giảm giới hạn, hiển thị số nhiệm vụ mới nhất và yêu cầu chỉnh giá trị phù hợp. Không tự sửa giá trị hoặc tự gửi lại.
- Giữ vô hiệu hóa người đã đầy cùng lý do ở màn phân công. Sau khi đổi giới hạn thành công, tải lại số liệu liên quan.
- Không gửi giới hạn qua API chưa hỗ trợ hoặc giả lập lưu bằng localStorage.

## 6. Kiểm thử cần có

- Đang có 3 nhiệm vụ: giảm xuống 2 bị chặn, đặt 3 được phép và không nhận thêm; đặt 4 được phép trong khoảng hợp lệ.
- Từ chối số âm, 0, trống, số thập phân và ngoài khoảng cho phép.
- Phân công và giảm giới hạn đồng thời không làm vượt giới hạn hoặc lưu dựa trên số nhiệm vụ cũ.
- Merchant không chỉnh được Shipper khác cửa hàng; Admin và Shipper không chỉnh được giới hạn qua API.
- Đếm đúng mọi loại nhiệm vụ, giải phóng chỗ đúng khi kết thúc/hủy/thay người; replay/phân công lại không cộng trùng.
- Phiên bản cũ và kết quả ghi chưa xác định không dẫn tới ghi đè hoặc tự gửi lại.
- Dữ liệu lịch sử được chuyển đổi phù hợp; giao diện không còn cấu hình giới hạn chung ở Admin.

BE đã triển khai theo kế hoạch. Trạng thái xác minh và tích hợp FE được theo dõi trong `MERCHANT_SHIPPER_LIMIT_IMPLEMENTATION_PLAN_20261123.md`.
