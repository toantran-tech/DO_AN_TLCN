# Plan: Danh mục Admin và điểm tích lũy

Ngày rà soát: 15/11/2026. Trạng thái: **phần điểm đã được duyệt và triển khai source; chờ CI/migration/deploy. API danh mục Admin vẫn tạm hoãn**. Hợp đồng bàn giao và quy tắc hiện hành: [LOYALTY_POINTS_API_REVIEW.md](LOYALTY_POINTS_API_REVIEW.md).

## 1. Phạm vi cần xác nhận

Người dùng xác nhận chưa chốt phạm vi “danh mục Admin”, yêu cầu **chưa làm API này**. Phần thiết kế bên dưới chỉ giữ làm tham khảo, không phải nhiệm vụ triển khai hiện tại.

Nếu người dùng muốn menu hoặc quản lý danh mục dịch vụ, cần điều chỉnh phần API dưới đây trước khi code. Không tạo thêm bảng danh mục chỉ để trả enum.

## 2. Hiện trạng danh mục

- Có `GET /admin/lookups/merchants` và `GET /admin/lookups/lockers`: dữ liệu động, có tìm kiếm/phân trang.
- Có CRUD/filter dịch vụ tại `/wash-service` dành cho Admin.
- Chưa thấy endpoint tổng hợp metadata dropdown Admin.
- Dịch vụ thực tế dùng `WashServiceType`: StandardWash, DryClean, IronOnly, BlanketWash, ShoeCare. Không dùng enum `ServiceCategory` thay thế chỉ vì tên giống “danh mục”.
- Giá trị enum tồn tại chưa có nghĩa tính năng tương ứng được hỗ trợ. Ví dụ không đưa Cash/ZaloPay thành phương thức thanh toán có thể chọn nếu luồng hiện chỉ hỗ trợ VNPay/MoMo.

## 3. API đề xuất nếu chốt phạm vi dropdown/bộ lọc

**GET `/admin/catalogs`**, policy `AdminOnly`, dùng envelope response hiện có.

Trả các nhóm hữu hạn, không cần phân trang. Mỗi lựa chọn gồm:

```json
{
  "value": 1,
  "code": "StandardWash",
  "label": "Giặt thông thường",
  "sortOrder": 1
}
```

Response data là object chứa các nhóm đã thống nhất, ví dụ `serviceTypes`, `orderStatuses`, `merchantStatuses`, `lockerStatuses`, `paymentMethods`. Chỉ bổ sung các nhóm thật sự được màn hình Admin sử dụng sau khi đối chiếu bộ lọc/command hiện hành.

Quy tắc:

1. Dùng danh sách nhóm cho phép rõ ràng; không tự xuất mọi enum trong assembly.
2. `value` giữ nguyên giá trị enum hiện hành, `code` ổn định, nhãn tiếng Việt và thứ tự hiển thị được định nghĩa tập trung.
3. Phân biệt lựa chọn lọc dữ liệu lịch sử với lựa chọn cho phép ghi. Nếu cùng nhóm cần cả hai, trả tách `filterOptions` và `inputOptions` có tài liệu rõ ràng.
4. Metadata không cấp quyền chuyển trạng thái. Các API thao tác và `allowedActions` của từng đơn vẫn quyết định hành động hợp lệ.
5. Merchant/locker động tiếp tục dùng hai API lookup có phân trang, không nhét toàn bộ bản ghi vào response này.
6. Không trả thông tin cá nhân, mã PIN/QR hay secrets.

### Thứ tự triển khai

- [ ] Chốt nghĩa “danh mục” và danh sách nhóm phục vụ các màn hình Admin.
- [ ] Tạo DTO trong `WashGo.Application.Contracts/Features/.../Results`, interface riêng trong `Services`; tránh gom interface vào file DTO.
- [ ] Tạo service Application ánh xạ các enum được phép sang lựa chọn; dùng helper chung để tránh lặp cấu trúc, giữ nhãn/ngữ cảnh rõ ràng.
- [ ] Map endpoint trong nhóm Admin, bổ sung Summary và hợp đồng response Swagger.
- [ ] Viết contract test cho quyền truy cập, giá trị enum/nhãn/thứ tự và các phương thức thanh toán thực sự được hỗ trợ; nối vào CI.
- [ ] Cập nhật tài liệu bàn giao FE và tiến độ theo kết quả thực tế.

Không cần migration nếu giữ phạm vi enum metadata. Không thêm cache trước khi có nhu cầu đo được.

## 4. Hiện trạng điểm trước đợt triển khai này (giữ làm căn cứ rà soát)

Đã có:

- Entity `Customer.LoyaltyPoints`, lưu tại `washgo.customers.loyalty_points`.
- Đăng ký thường, Google tạo Customer và dữ liệu seed đều khởi tạo điểm bằng 0.
- `CustomerAppService` đưa giá trị đã lưu vào `CustomerProfileDto.LoyaltyPoints`, trả qua `GET /customer/me`.
- API cập nhật hồ sơ không nhận trường điểm từ client.

**Chưa thấy nghiệp vụ cộng điểm, trừ/đổi điểm, lịch sử điểm, hết hạn hoặc cấu hình quy đổi.** Thanh toán và hoàn tất đơn hiện không tự làm tăng điểm. Số đang hiển thị là giá trị được lưu trong DB.

`TotalOrders` và `TotalSpent` trong hồ sơ được tính riêng từ đơn hàng, không phải công thức tích điểm và không tự ghi lại LoyaltyPoints. Không lấy tổng chi tiêu này làm sổ giao dịch điểm vì cần xét hoàn tiền và các khoản thu dư.

Đối chiếu `DO_AN/Đặc tả usecase.docx`: phần hồ sơ có yêu cầu hiển thị điểm tích lũy; chưa tìm thấy quy tắc cộng/đổi điểm khi rà các từ khóa điểm tích lũy, điểm thưởng, tích điểm, đổi điểm và loyalty. Vì vậy không tự đặt tỷ lệ tiền/điểm.

Nguồn chính:

- `src/WashGo.Domain/Entities/Partner/Customer.cs`
- `src/WashGo.Application/Features/AuthApplication/Services/AuthAppService.cs`
- `src/WashGo.Application/Features/CustomerApplication/Services/CustomerAppService.cs`
- `src/WashGo.Application.Contracts/Features/CustomerContracts/Results/CustomerProfileDto.cs`

## 5. Plan tích và sử dụng điểm như coin

**Hướng người dùng yêu cầu:** cộng điểm theo phần trăm sau mỗi đơn thanh toán thành công; điểm dùng giảm tiền cho lần sau. Không chờ `Completed` để cộng điểm như đề xuất cũ.

Người dùng đã duyệt triển khai phương án dưới đây, đồng thời xác nhận riêng: **WashGo chịu chi phí giảm điểm; hoàn tiền/điểm theo tỷ lệ sau khi trừ phí hủy**. Các mô tả “đề xuất” bên dưới là lịch sử của plan; trạng thái đã chốt xem mục 5.8.

### 5.1. Bộ thông số đề xuất

| Nội dung | Đề xuất |
| --- | --- |
| Giá trị sử dụng | 1 điểm = 1 VNĐ |
| Tỷ lệ tích | 1% tiền giặt thực trả qua VNPay/MoMo, làm tròn xuống điểm nguyên |
| Giới hạn dùng | Tối đa 20% tiền giặt sau voucher và không vượt điểm khả dụng |
| Thứ tự giảm giá | Voucher → điểm → thanh toán số tiền còn lại |
| Thời hạn điểm | Chưa hết hạn trong bản đầu |
| Đơn cũ | Không tự cộng bù; bắt đầu từ lúc bật tính năng |
| Phạm vi | Customer; giảm tiền giặt; không chuyển điểm giữa tài khoản hoặc rút tiền mặt |

Tỷ lệ tích 1% và giới hạn dùng 20% dự kiến đưa vào cấu hình Admin. Lưu chính sách áp dụng theo giao dịch; thay đổi cấu hình không tính lại điểm cũ.

### 5.2. Cộng điểm khi thanh toán thành công

- Chỉ cộng khi BE xác nhận callback/IPN hợp lệ, đúng giao dịch, số tiền và mục đích thanh toán. Không tin kết quả chuyển trang của FE.
- Cơ sở tích điểm là tiền giặt thực trả qua cổng sau voucher và điểm đã dùng.
- Loại trừ phần trả bằng điểm, phí hủy, phí quá hạn/locker/kho, bồi thường và khoản thu dư.
- Callback lặp hoặc thanh toán dư cho cùng nghĩa vụ không cộng thưởng lần hai.
- Điểm thưởng của giao dịch hiện tại không dùng để thanh toán chính giao dịch đó.

Ví dụ: giá chốt 200.000đ, voucher 20.000đ, dùng 10.000 điểm → trả cổng 170.000đ → nhận 1.700 điểm.

### 5.3. Báo giá và dùng điểm

- Khách chọn số điểm tại màn hình thanh toán sau khi Merchant chốt giá và giặt xong.
- BE tính lại tiền giặt sau voucher, giới hạn điểm và số tiền phải trả; client chỉ gửi số điểm mong muốn, không gửi số dư hoặc quyết định số tiền cuối.
- Điểm dùng là số nguyên không âm, phải đáp ứng giới hạn và số dư khả dụng.
- Ví dụ: tiền sau voucher 180.000đ, có 50.000 điểm → dùng tối đa 36.000 điểm → thanh toán 144.000đ.
- Phần giảm bằng điểm phải được tách riêng trong báo giá/đơn/giao dịch; không ghi giả thành tiền nhận từ VNPay/MoMo.

### 5.4. Giữ điểm theo phiên thanh toán

- Phân biệt tổng điểm, điểm đang giữ và điểm khả dụng. Khi không có nợ: điểm khả dụng = tổng điểm − điểm đang giữ.
- Tạo phiên thanh toán thì giữ điểm; thanh toán thành công mới trừ chính thức và cộng điểm thưởng.
- Phiên thất bại/hết hạn đã được xác nhận an toàn thì giải phóng điểm. Không tự nhả chỉ vì FE đóng trang hoặc hết giờ local khi cổng còn có thể trả thành công.
- Giao dịch chưa rõ kết quả tiếp tục giữ điểm, phối hợp luồng đối soát hiện có. Kết quả đến muộn sau khi giải phóng phải được đối soát, không trừ hai lần hoặc tự gây âm điểm ngoài chính sách.
- Retry cùng phiên tái sử dụng lần giữ điểm; hai đơn đồng thời không được giữ vượt số dư.

### 5.5. Hủy đơn và hoàn tiền

- Phần thanh toán bằng tiền hoàn qua nghiệp vụ tiền hiện có; phần thanh toán bằng điểm trả lại bằng điểm, không đổi thành tiền mặt.
- Thu hồi điểm thưởng tương ứng phần tiền giặt thực trả được hoàn; tham chiếu giao dịch thưởng gốc.
- Hoàn nhiều lần/hoàn một phần phải tính theo số lũy kế, giới hạn bởi tiền và điểm gốc, tránh sai số làm tròn hoặc hoàn vượt.
- Với phí hủy, cần chốt cách phân bổ phần khách chịu giữa tiền và điểm trước khi code; không mặc định hoàn hết điểm rồi tính toàn bộ phí vào tiền.
- Nếu điểm thưởng đã dùng hết: đề xuất ghi nhận điểm nợ, chặn đổi điểm khi còn nợ và bù từ điểm kiếm được sau này; vẫn cho mua/thanh toán đơn mới. Đây là chính sách cần người dùng duyệt.
- Thất bại hoàn tiền không được ghi nhận đã hoàn thành điểm theo cách khiến retry tạo thêm giao dịch; cần trạng thái xử lý và khóa sự kiện rõ ràng.

### 5.6. Thiết kế kỹ thuật dự kiến

- Tạo sổ giao dịch điểm và bản ghi giữ điểm, liên kết Customer/đơn/phiên thanh toán/hoàn tiền, có khóa sự kiện duy nhất chống ghi trùng.
- Sổ ghi cộng, trừ và đảo giao dịch; số dư là dữ liệu tổng hợp được cập nhật đồng bộ, có khả năng đối chiếu lại từ sổ.
- Cùng một transaction cho thay đổi số dư, giữ/trừ điểm và ghi nhận kết quả thanh toán nội bộ; dùng thứ tự khóa nhất quán với order/payment hiện có. Không giữ transaction DB trong lúc gọi cổng bên ngoài.
- Không thay đổi ý nghĩa `Customer.LoyaltyPoints` sang số dư âm một cách ngầm định; nếu chốt nợ điểm thì thiết kế trường/sổ nợ và công thức khả dụng rõ ràng.
- Phân lớp Domain, Contracts, Application, EF và HttpApi theo cấu trúc hiện hành; tách interface/DTO; tái sử dụng service tính điểm chung cho VNPay/MoMo để tránh lặp.
- API dự kiến: số dư điểm, lịch sử có phân trang, báo giá sử dụng điểm theo đơn; tích hợp số điểm mong muốn vào tạo phiên thanh toán. Tên route/DTO sẽ chốt sau rà hợp đồng hiện có.
- Kiểm tra JWT, đúng chủ đơn, giới hạn input; không mở API cho Customer tự cộng/trừ điểm.
- Có migration riêng và kiểm tra dữ liệu điểm cũ trước khi lập số dư đầu kỳ; không tự xóa hay ghi đè điểm có sẵn.

### 5.7. Chia chặng sau khi duyệt nghiệp vụ

1. **Sổ điểm và cộng thưởng:** migration, cấu hình/snapshot, số dư/lịch sử; tích điểm từ thanh toán tiền giặt thành công; chống callback trùng.
2. **Dùng điểm:** báo giá server, giữ/trừ/giải phóng điểm theo phiên, giới hạn sử dụng, xử lý giao dịch chưa rõ kết quả và tranh chấp số dư.
3. **Hoàn tiền và điểm:** hoàn điểm, thu hồi thưởng, hoàn từng phần, phí hủy, điểm nợ nếu được duyệt; báo cáo tài chính phân biệt tiền thật và giảm bằng điểm.
4. **Nghiệm thu và bàn giao:** test nghiệp vụ/concurrency nối CI; cập nhật đặc tả use case, tiến độ, checklist và tài liệu FE theo kết quả thực tế.

Không công bố tích điểm sẵn sàng sử dụng trước khi xử lý hoàn tiền/thu hồi thưởng, vì khách có thể nhận thưởng rồi yêu cầu hoàn tiền.

### 5.8. Các quyết định đã chốt và tiến độ

- [x] Tích 1%, 1 điểm = 1đ, dùng tối đa 20%; có cấu hình Admin cho tỷ lệ phiên mới.
- [x] Kết hợp voucher trước điểm, chưa hết hạn, không tự cộng bù giao dịch cũ; giữ số dư legacy.
- [x] Nợ điểm sau thu hồi thưởng được bù bằng điểm kiếm sau này; không chặn mua đơn bằng tiền.
- [x] WashGo chịu giảm bằng điểm; Merchant đối soát tiền + tài trợ điểm, cổng chỉ ghi tiền thật. Hoàn điểm giảm phần tài trợ tương ứng.
- [x] Hoàn tỷ lệ tiền/điểm sau phí hủy; làm tròn điểm nguyên, phần lẻ vào tiền nhưng không vượt tiền/điểm gốc. Hoàn từng phần tính lũy kế.
- [x] Source sổ điểm, giữ/tiêu/thưởng/hoàn/đảo, snapshot, API và cấu hình.
- [x] Source migration + Designer/snapshot; test công thức/HTTP đã viết và nối CI; tài liệu FE/tiến độ/checklist đã cập nhật.
- [ ] CI chạy test thành công, áp migration, build Docker/deploy và nghiệm thu sandbox. Không đánh dấu PASS khi mới chỉ kiểm tra biên dịch.

## 6. Điều kiện nghiệm thu

API danh mục: đúng Admin mới truy cập; numeric values khớp enum; nhãn/thứ tự xác định; lựa chọn ghi không quảng bá tính năng chưa hỗ trợ; không trả danh sách DB không giới hạn hoặc dữ liệu nhạy cảm. CI kiểm tra hợp đồng và hồi quy API hiện có.

Điểm tích lũy: source đã triển khai; chỉ đánh dấu nghiệm thu sau khi kiểm thử lifecycle/hoàn tiền/retry/concurrency. Bộ ca nghiệm thu: tính điểm sau voucher/đổi điểm, loại các khoản không hợp lệ, callback lặp, hai đơn dùng cùng số dư, retry tạo phiên, kết quả cổng đến muộn, hoàn một phần/nhiều lần, thưởng đã tiêu trước hoàn tiền, bảo vệ quyền sở hữu và hồi quy đối soát.

## 7. Thứ tự ưu tiên đề xuất

1. API danh mục Admin tạm hoãn theo yêu cầu người dùng, chưa triển khai.
2. Phần điểm đã duyệt và có source theo bốn chặng; thực hiện CI, kiểm tra migration và deploy theo tài liệu bàn giao.
3. Chỉ xử lý API danh mục khi người dùng chốt phạm vi sau này.

Đã sửa business code và kiểm tra biên dịch; chưa chạy smoke/unit tests, build Docker, áp migration hoặc deploy trong đợt này. Không sửa file Word ngoài repo trong đợt code; quy tắc bổ sung được ghi tại tài liệu bàn giao ở đầu file.
