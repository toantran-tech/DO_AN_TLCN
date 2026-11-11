# Chia nhánh và tạo PR Shipper — 11/11/2026

Ba nhánh nối tiếp từ `develop` tại `584b13f`. Nhánh sau chứa commit của nhánh trước vì dùng chung dữ liệu nhiệm vụ, API đọc và guard. Không phải ba nhánh độc lập.

| Thứ tự | Nhánh | Nội dung thêm | Base PR ban đầu |
| --- | --- | --- | --- |
| 1 | `feat/toantran/shipper-delivery-workspace` | Nền DeliveryTask/snapshot, hai migration, nhiệm vụ, credentials, operation replay, lịch sử/summary/CSV, preview/media và smoke liên quan. | `develop` |
| 2 | `feat/toantran/shipper-dashboard` | Dashboard snapshot VN, KPI/coverage, chart, kỳ so sánh và route dashboard. | `feat/toantran/shipper-delivery-workspace` |
| 3 | `feat/toantran/shipper-profile-handoff` | Hồ sơ version/idempotency, liên hệ Merchant, nhật ký, guard ghi hồ sơ, smoke tổng hợp và tài liệu bàn giao. | `feat/toantran/shipper-dashboard` |

File handler dùng chung được tách nội dung trong index để mỗi commit chỉ tham chiếu interface đã có ở nhánh đó. Schema/model snapshot và migration của đợt được giữ chung tại nhánh 1.

## PR do thành viên tạo

Tạo/review theo thứ tự **1 → 2 → 3**. Có thể mở cả ba PR ngay với base ở bảng trên để mỗi PR chỉ hiện phần thay đổi của chặng đó. Sau khi nhánh trước đã merge vào develop, đổi base PR kế tiếp về develop rồi kiểm tra lại diff.

Nên dùng **Create a merge commit** cho các PR nền để giữ quan hệ commit của chuỗi. Nếu chọn squash/rebase merge, cần cập nhật lại các nhánh phụ thuộc trước khi đổi base, tránh review lại commit nền đã nhập.

Không merge trực tiếp lên main trong thao tác chia/push này. Các nhánh này chỉ chuẩn bị code để thành viên tạo PR; không tự tạo hoặc merge PR.

Hợp đồng cho FE: [SHIPPER_API_REVIEW.md](SHIPPER_API_REVIEW.md). Kết quả kiểm thử nghiệp vụ: [checklist](../IMPLEMENTATION_CHECKLIST.md), mục Shipper 11/11. Ngoài build toàn bộ đã chạy, các snapshot commit tách nhánh được kiểm tra build từ bản archive riêng, không dùng các file chưa commit ở workspace để che thiếu dependency.
