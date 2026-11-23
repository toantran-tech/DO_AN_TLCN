# Bàn giao FE — Bán kính phục vụ khi gán Merchant cho locker

Ngày 23/11/2026. Thực hiện `Docs/cần làm/BACKEND_ADMIN.md`.

## Quy tắc

- Merchant chỉ được phụ trách locker khi khoảng cách đường chim bay (Haversine) **từ tọa độ cửa hàng Merchant đến tọa độ locker** ≤ `serviceRadiusKm` của chính Merchant đó. Bằng bán kính vẫn hợp lệ.
- So sánh trên khoảng cách chưa làm tròn. `distanceKm` trả về làm tròn 2 chữ số chỉ để hiển thị (5,004 km hiển thị 5.00 nhưng **không** thuộc bán kính 5 km).
- Merchant thiếu tọa độ (0,0) không bao giờ được coi là khoảng cách 0.
- Không liên quan vị trí khách hàng và bán kính tìm kiếm locker của Customer.
- Vùng phục vụ (`serviceAreaIds`) vẫn chỉ là phân loại/lọc. Quyền phụ trách locker do liên kết Merchant–locker và bán kính quyết định; tên vùng/cùng tỉnh không cấp quyền.

## API mới

### `GET /locker/eligible-merchants` (Admin)

| Query | Ý nghĩa |
| --- | --- |
| `latitude`, `longitude` | Tọa độ **locker** đang tạo/sửa. Có thể bỏ trống khi gửi `lockerId`, khi đó BE dùng tọa độ đã lưu. |
| `lockerId` | Khi sửa locker: luôn trả cả Merchant đang gán (`isLinked`, `linkPriority`) dù không còn hợp lệ. |
| `keyword` | Lọc tên/địa chỉ cửa hàng (≤ 200 ký tự). Merchant đang gán vẫn được trả. |
| `includeIneligible` | `true`: trả thêm Merchant đang hoạt động nhưng ngoài bán kính/thiếu tọa độ/khóa tài khoản/hết hợp đồng. |
| `page`, `take` | Phân trang ở BE (take 1–100, mặc định 20). |

Mỗi dòng `EligibleMerchantDto`: `merchantId`, `businessName`, `address`, `city`, `status`, `serviceRadiusKm`, `distanceKm` (null khi thiếu tọa độ), `withinServiceRadius`, `eligible`, `blockedReasonCode`, `isLinked`, `linkPriority`.

- `blockedReasonCode` có thể là `MerchantOutsideServiceRadius`, `MerchantLocationUnavailable`, `LockerLocationRequired`, `MerchantInactive`, `AccountInactive`, `ContractExpired`.
- Thứ tự sắp xếp: hợp lệ trước, gần trước (theo khoảng cách chưa làm tròn), rồi tên, rồi id.
- `meta`: `{ latitude, longitude, lockerId, eligibleCount, code, distanceUnit: "km", distanceDecimals: 2 }`. `code = NoEligibleMerchantForLocker` khi không có Merchant hợp lệ (HTTP 200, list có thể rỗng).
- Lỗi 400 `meta.code`: `LockerLocationRequired` (thiếu tọa độ hoặc 0,0), `InvalidLocation` (chỉ gửi một tọa độ), `InvalidFilter`. Gửi `lockerId` không tồn tại trả 404.
- Đây chỉ là dữ liệu gợi ý. BE kiểm tra lại mọi điều kiện lúc lưu.

### `GET /locker/coverage-issues` (Admin)

Danh sách liên kết cũ không đạt quy tắc (dữ liệu tạo trước khi có quy tắc). Mỗi dòng gồm:

- `lockerId`, `lockerCode`, `lockerName`, `lockerStatus`.
- `merchantId`, `businessName`, `priority`, `distanceKm`, `serviceRadiusKm`.
- `reasonCode`: `MerchantOutsideServiceRadius`, `MerchantLocationUnavailable` hoặc `LockerLocationRequired`.
- `lockerStillServed`: còn Merchant khác phục vụ locker hay không.
- `suggestedServiceRadiusKm`: bán kính nguyên nhỏ nhất bao được locker (null nếu > 100 km hoặc thiếu tọa độ).

Thứ tự: locker không còn ai phục vụ lên trước.

## Kiểm tra khi lưu

Áp dụng cho `POST /locker/` và `PUT /locker/`. Đây là các endpoint duy nhất ghi liên kết Merchant–locker.

- Tạo: mọi Merchant trong `merchantIds`/`merchantId` phải hợp lệ:
  - đang hoạt động, trong bán kính, có tọa độ;
  - Merchant mới thêm phải có tài khoản hoạt động và hợp đồng còn hiệu lực.
- Sửa: kiểm tra lại khi gửi `merchantIds` **hoặc** đổi tọa độ.
  - `merchantIds = null` nghĩa là giữ danh sách hiện có, và vẫn kiểm tra lại với tọa độ mới.
  - Merchant đang gán sẵn chỉ cần đang hoạt động và trong bán kính.
  - Sửa không đổi tọa độ và không gửi danh sách thì không chặn vì liên kết cũ.
- Một Merchant không hợp lệ thì từ chối **toàn bộ** yêu cầu. Không ghi một phần địa chỉ, danh sách hay thứ tự.
- Khóa đồng thời:
  - Lúc lưu locker, BE khóa từng Merchant theo cùng khóa mà luồng sửa Merchant và phân công đơn dùng.
  - Sau khi khóa mới đọc tọa độ và bán kính.
  - Vì vậy sửa locker và đổi bán kính Merchant không thể cùng thành công tạo ra liên kết ngoài vùng.
- `expectedVersion`, `operationId` (replay), tra cứu và audit giữ như cũ.

Lỗi 400 (có `errors.merchantIds` hoặc `errors.latitude/longitude`):

| meta.code | Dữ liệu kèm |
| --- | --- |
| `MerchantOutsideServiceRadius` | `lockerId` (khi sửa), `latitude`, `longitude`, `merchants[]: { merchantId, businessName, distanceKm, serviceRadiusKm, blockedReasonCode }` |
| `MerchantLocationUnavailable` | như trên, `blockedReasonCode = MerchantLocationUnavailable` |
| `LockerLocationRequired` | `fields: [latitude, longitude]` |
| `NoEligibleMerchantForLocker` | `reasons: ["NoActiveMerchantWithinServiceRadius"]`; tạo locker không chọn Merchant và không có Merchant nào hợp lệ |
| `MerchantNotEligible` | `merchants[]` với `MerchantInactive`/`AccountInactive`/`ContractExpired`/`MerchantNotFound` |

## Sửa vị trí hoặc bán kính Merchant

Áp dụng cho `PUT /merchant/` (Admin sửa hồ sơ) và `POST /merchant-profile-request/{id}/approve` (duyệt yêu cầu đổi vị trí cửa hàng).

- Nếu thay đổi làm một locker đang được Merchant phục vụ trong bán kính rơi ra ngoài: BE trả **409** `MerchantCoverageWouldExcludeLockers` và không lưu gì.
- Hồ sơ, tọa độ, bán kính, liên kết và trạng thái yêu cầu (vẫn Pending) giữ nguyên.
- `meta`: `{ code, merchantId, serviceRadiusKm, suggestedServiceRadiusKm, lockers[]: { lockerId, code, name, distanceKm, serviceRadiusKm } }`.
  - `serviceRadiusKm` là bán kính mới.
  - `distanceKm` tính theo vị trí mới.
  - `suggestedServiceRadiusKm` là bán kính nguyên nhỏ nhất (≤ 100) giữ được mọi locker; null nếu không có.
- BE không tự gỡ liên kết, không chuyển hoặc hủy đơn, không ngừng locker. Admin chuyển hoặc gỡ phân công qua `PUT /locker/` trước rồi sửa Merchant lại.

## Dữ liệu cũ và luồng đọc

- Liên kết cũ ngoài bán kính hoặc thiếu tọa độ **được giữ nguyên**, không tự xóa, không tự tăng bán kính. Đơn đang xử lý vẫn đi hết luồng cũ: trả đồ, kho, đối soát dùng liên kết đã lưu.
- Chính sách tiếp nhận đơn mới: liên kết không đạt quy tắc **không được tính là Merchant phục vụ**. Áp dụng thống nhất cho:
  - catalog Customer (danh sách và chi tiết locker);
  - kiểm tra tạo đơn và báo giá;
  - ứng viên phân công Merchant (`MerchantNotEligible`/`NoMerchantInArea`).
- Locker không còn Merchant nào trong bán kính sẽ ẩn khỏi catalog Customer cho tới khi Admin xử lý theo `GET /locker/coverage-issues`.
- Giữa các Merchant hợp lệ, điểm gợi ý phân công vẫn như cũ: ưu tiên, tải lượng, đánh giá, khoảng cách. Điểm cao không vượt được điều kiện bán kính.
- Đã rà dữ liệu `washgo_main` trên máy dev: 2 liên kết, cả hai trong bán kính (0,00 km và 0,38 km / 5 km). Không cần xử lý thêm.

## Kiểm thử

Smoke `tests/smoke/merchant-service-radius.ps1` (đã thêm vào CI), gồm:

- trong, ngoài bán kính và ranh giới trước làm tròn (4,996 / 5,004 km);
- hai Merchant với bán kính khác nhau, thứ tự ưu tiên, không có Merchant hợp lệ;
- Merchant thiếu tọa độ, chỉ Admin được gọi;
- tạo bị từ chối không ghi locker;
- dời locker với `merchantIds = null`;
- giảm bán kính Merchant và duyệt yêu cầu đổi vị trí bị 409 mà không ghi;
- liên kết cũ được giữ, có trong báo cáo, bị ẩn khỏi catalog và không tạo được đơn.

Các smoke có locker cũ được dời vào trong bán kính 5 km của Merchant mẫu.
