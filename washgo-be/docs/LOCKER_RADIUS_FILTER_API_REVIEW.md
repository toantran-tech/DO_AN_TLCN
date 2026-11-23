# Bàn giao FE — Lọc locker theo bán kính tìm kiếm

Ngày 23/11/2026. Thực hiện `Docs/cần làm/BACKEND_CUSTOMER.md`.

## Hợp đồng

`GET /customer/lockers` (public, không cần đăng nhập, rate limit `public-catalog`).

| Query | Ý nghĩa |
| --- | --- |
| `keyword`, `city`, `district` | Như cũ; lọc ở BE trước bán kính. |
| `latitude`, `longitude` | Vị trí khách tự cung cấp; phải đi cùng nhau. Có vị trí thì có `distanceKm` và sắp gần nhất trước. |
| `radiusKm` | Mới. Số thực `0 < radiusKm ≤ 50`, bắt buộc kèm cặp tọa độ. FE gửi 3, 5 hoặc 10. |
| `page`, `take` | Như cũ (take tối đa theo quy tắc hiện hành). |

- Có `radiusKm`: BE giữ locker có khoảng cách Haversine **chưa làm tròn** ≤ `radiusKm` trên toàn bộ tập locker khả dụng, rồi mới đếm `total` và phân trang. `total`, `totalPages`, `currentPage`, `hasNextPage` là của tập đã lọc bán kính.
- Sắp xếp khi có vị trí: khoảng cách tăng dần, khóa phụ ổn định là id locker. Không có vị trí: theo tên rồi id.
- `distanceKm` làm tròn 2 chữ số chỉ để hiển thị. Ví dụ locker cách 3,002 km hiển thị 3.00 nhưng **không** có trong kết quả `radiusKm=3`. FE không cần tự kiểm tra lại khoảng cách.
- Không có kết quả: HTTP 200, `list: []`, `total: 0`, message "Không có locker khả dụng trong bán kính X km." BE không tự mở rộng bán kính.
- Không gửi `radiusKm`: hành vi cũ giữ nguyên (Home, Tất cả locker).
- `meta` mới trên response thành công: `{ sortBy: "Distance" | "Name", radiusKm: number | null, distanceUnit: "km", distanceDecimals: 2 }`.
- Bán kính tìm kiếm chỉ để khám phá; không ảnh hưởng tạo đơn và không phải bán kính phục vụ của Merchant.

## Mã lỗi (HTTP 400, `meta.code`, `meta.fields`)

| meta.code | Khi nào |
| --- | --- |
| `LocationRequiredForRadius` | Có `radiusKm` nhưng thiếu cặp `latitude/longitude`. |
| `InvalidRadius` | `radiusKm` ≤ 0, > 50 hoặc không phải số hữu hạn. |
| `InvalidLocation` | Chỉ gửi một trong hai tọa độ, hoặc tọa độ ngoài phạm vi/không hữu hạn. |
| `InvalidLockerFilter` | keyword > 200 ký tự, city/district > 100 ký tự. |

`PaginatedResult` có thêm trường `meta` (null với các API khác); không ảnh hưởng client cũ.

## Kiểm thử

Smoke `tests/smoke/locker-radius-filter.ps1` (đã thêm vào CI): không bán kính giữ nguyên, lọc trước phân trang qua nhiều trang, ranh giới làm tròn (1,112 km ngoài 1,11 km; 3,002 km ngoài 3 km), rỗng không giãn, khách chưa đăng nhập, các mã lỗi.
