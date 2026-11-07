# KẾ HOẠCH TRIỂN KHAI BACKEND — MERCHANT (PLAN.MD)

Tài liệu này vạch ra lộ trình chi tiết từng bước để triển khai toàn bộ các tính năng Backend còn thiếu cho đối tác **Merchant (Tiệm giặt)** dựa trên các rà soát tại `BACKEND_MERCHANT.md` và tài liệu use case chuẩn `Đặc tả usecase.docx`.

---

## 1. MỤC TIÊU & NGUYÊN TẮC THIẾT KẾ

### 1.1. Mục tiêu
1. Cung cấp đầy đủ các API nghiệp vụ để Frontend Merchant hoạt động trơn tru:
   - **Dashboard tổng quan snapshot đồng bộ có bộ lọc thời gian linh hoạt** (`GET /merchant/dashboard`): Hỗ trợ lọc theo **Ngày (Hôm nay)**, **Tuần này (7 ngày gần nhất)**, **Tháng này**, hoặc **Tùy chọn khoảng ngày (Từ ngày - Đến ngày)**.
   - Báo cáo doanh thu & tăng trưởng chuyên biệt cho Merchant (`GET /merchant/revenue-report`).
   - Cảnh báo vi phạm SLA theo thời gian thực ("Overdue Attention") (`GET /merchant/sla-alerts`).
2. Nâng cấp hồ sơ cửa hàng:
   - Hỗ trợ trường `City` bắt buộc (1–100 ký tự), cập nhật tọa độ `Latitude/Longitude` khi chọn địa chỉ bản đồ.
   - Bổ sung ngày tham gia (`joinedAt`) và số lượt đánh giá công khai (`publicRatingCount`).
3. Chuẩn hóa cơ chế tài chính & hoa hồng:
   - Lưu snapshot tỷ lệ hoa hồng `CommissionRateApplied` theo từng đơn hàng ngay khi gán Merchant.
   - Tính hoa hồng đối soát (`Settlements`) chính xác theo từng đơn hàng, chống sai lệch khi cấu hình Merchant thay đổi hồi tố.
   - Trả đầy đủ thông tin tài chính theo đơn: `CommissionBaseAmount`, `CommissionRateApplied`, `CommissionAmount`, `MerchantNetAmount`.

### 1.2. Nguyên tắc triển khai
- **Phân quyền nghiêm ngặt**: Tất cả các API dành cho Merchant phải lấy định danh `MerchantId` từ `CurrentUser.Id` (Claims trong JWT Token), không tin tưởng `merchantId` do client gửi lên.
- **Tính nhất quán dữ liệu (Data Snapshot)**: Dashboard và Báo cáo trả về dữ liệu trong cùng một transaction/snapshot, kèm `generatedAt` để client hiển thị minh bạch.
- **Xử lý múi giờ chuẩn xác**: Mọi bộ lọc Ngày/Tuần/Tháng/Tùy chọn được quy đổi thống nhất theo múi giờ Việt Nam (`Asia/Ho_Chi_Minh`, UTC+7).
- **Không phá vỡ cấu trúc hiện tại**: Tận dụng tối đa kiến trúc ABP Framework, Entity Framework Core, Unit of Work, và Realtime SignalR hiện có.

---

## 2. LỘ TRÌNH TRIỂN KHAI CHI TIẾT (6 GIAI ĐOẠN)

### Phase 1: Cải tiến Hồ sơ Merchant (Profile & Update Request Enhancements) - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Hoàn thiện dữ liệu hồ sơ tiệm giặt, hỗ trợ chọn địa chỉ bản đồ + tỉnh/thành phố và duyệt tọa độ.*

- [x] **1.1. Cập nhật Application.Contracts**:
  - `MerchantProfileUpdateCommand`: Thêm `City` (string, required, 1-100 ký tự), `Latitude` (decimal?), `Longitude` (decimal?).
  - `MerchantProfileDataDto`: Thêm `City`, `Latitude`, `Longitude` để lưu thông tin đề xuất (`Proposed`) và hiện tại (`Current`).
  - `MerchantProfileDto`: Thêm `JoinedAt` (DateTime), `PublicRatingCount` (int).
- [x] **1.2. Cập nhật Application Service (`MerchantProfileAppService`)**:
  - Trong `RequestUpdateAsync`: Validate `City` bắt buộc (trim 1–100 ký tự), validate tọa độ hợp lệ (nếu có).
  - Trong `Changes()`: Bổ sung so sánh `city`, `latitude`, `longitude`.
  - Trong `ApproveAsync()`: Cập nhật đồng thời `merchant.City = data.City`, `merchant.Latitude = data.Latitude`, `merchant.Longitude = data.Longitude`.
  - Trong `GetMyProfileAsync()`: Gán `JoinedAt = merchant.CreatedAt` và tính `PublicRatingCount` từ repository đánh giá (`_ratingRepo`).
- [x] **1.3. Kiểm thử Phase 1**:
  - Đã cập nhật và chạy PASS 100% `tests/smoke/merchant-profile.ps1` (đầy đủ 7 bước kiểm thử: xem hồ sơ, validate city/tọa độ, gửi yêu cầu, admin duyệt, đối chiếu dữ liệu cập nhật).

---

### Phase 2: Hoa hồng cố định theo đơn (`CommissionRateApplied`) - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Đảm bảo hoa hồng được chốt cố định theo từng đơn hàng, đối soát minh bạch và không bị sai lệch số liệu khi đổi cấu hình tiệm.*

- [x] **2.1. Cập nhật Domain & EF Core**:
  - `WashOrder` (Entity): Thêm thuộc tính `public decimal CommissionRateApplied { get; set; }`.
  - `WashOrderEntityTypeConfiguration`: Cấu hình precision cho `CommissionRateApplied` (`HasPrecision(5, 2)`).
  - Tạo EF Core Migration: `AddOrderCommissionRateApplied` (đã migrate database PostgreSQL thành công).
- [x] **2.2. Snapshot hoa hồng khi gán đơn**:
  - Trong `OrderAssignmentAppService.AssignCoreAsync`:
    - Khi gán `order.MerchantId = target.MerchantId`, gán snapshot `order.CommissionRateApplied = target.CommissionRate`.
- [x] **2.3. Cập nhật DTO đơn hàng**:
  - Trong `WashOrderResultDto` (dùng chung cho cả Detail và List):
    - Thêm `CommissionRateApplied` (decimal).
    - Thêm `CommissionBaseAmount` (decimal = `FinalAmount`).
    - Thêm `CommissionAmount` (decimal = `Round(FinalAmount * CommissionRateApplied / 100)`).
    - Thêm `MerchantNetAmount` (decimal = `CommissionBaseAmount - CommissionAmount`).
- [x] **2.4. Cải tiến đối soát (`FinanceAppService`)**:
  - Sửa `BuildCommissionsAsync`: Tính hoa hồng từng đơn bằng `order.CommissionRateApplied` (fallback về `merchant.CommissionRate` cho dữ liệu lịch sử chưa có snapshot).
  - Kiểm tra đối soát tài chính và trả kết quả chính xác theo snapshot hoa hồng.
- [x] **2.5. Kiểm thử Phase 2**:
  - Đã tích hợp trong `tests/smoke/merchant-dashboard-commission.ps1`, xác nhận 100% các đơn có hoa hồng snapshot và doanh thu thực nhận chính xác.

---

### Phase 3: Dashboard tổng hợp Merchant có bộ lọc thời gian (`GET /merchant/dashboard`) - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Cung cấp 1 snapshot đồng bộ duy nhất, hỗ trợ lọc linh hoạt theo Ngày / Tuần / Tháng / Tùy chọn.*

- [x] **3.1. Thiết kế Query & DTO (`MerchantDashboardQuery` & `MerchantDashboardDto`)**:
  - `MerchantDashboardQuery`: Hỗ trợ `Period` (`all`, `today`, `week`, `month`, `custom`), `FromDate`, `ToDate`.
  - `MerchantDashboardDto`: Trả về `Period`, `FromDate`, `ToDate`, `TotalOrders`, `StatusCounts` (14 trạng thái), `LatestOrders` (5 đơn mới nhất đã ẩn PII khách hàng), `RecentSettlements` (4 kỳ đối soát gần nhất), `GeneratedAt`.
- [x] **3.2. Triển khai Service & Controller**:
  - Xử lý múi giờ chuẩn Việt Nam (`Asia/Ho_Chi_Minh` UTC+7) cho `today`, `week`, `month`, `custom`.
  - Truy vấn tối ưu bằng LINQ GroupBy trên bảng `WashOrders` theo `MerchantId == CurrentUser.Id` và mốc thời gian lọc.
  - Phân quyền nghiêm ngặt `RequireAuthorization("MerchantOnly")`.
- [x] **3.3. Kiểm thử Phase 3**:
  - Đã viết và chạy test tự động `tests/smoke/merchant-dashboard-commission.ps1` PASS 100% cho mọi bộ lọc (`all`, `today`, `week`, `month`, `custom`, và chặn lỗi mốc ngày không hợp lệ).

---

### Phase 4: Báo cáo Doanh thu & Tăng trưởng Merchant (`GET /merchant/revenue-report`) - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Cung cấp API báo cáo tài chính riêng cho Merchant (thay vì dùng API AdminOnly).*

- [x] **4.1. Thiết kế Query & DTO (`MerchantRevenueReportQuery` & `MerchantRevenueReportDto`)**:
  - `MerchantRevenueReportQuery`: Hỗ trợ `Period` (`today`, `week`, `month`, `custom`, `all`), `FromDate`, `ToDate`.
  - `MerchantRevenueReportDto`: Trả về `TotalGrossRevenue`, `TotalCommission`, `TotalPayoutAmount`, `RefundedAmount`, `PaidOrderCount`, `CancelledOrderCount`, `GrowthRate`, `PreviousPayoutAmount`, `DailySeries` (`Date`, `GrossRevenue`, `Commission`, `Payout`, `OrderCount`), `GeneratedAt`.
- [x] **4.2. Triển khai Logic Báo cáo (`MerchantAppService`)**:
  - Phương thức `GetRevenueReportAsync`:
    - Định danh tiệm bằng `CurrentUser.Id`.
    - Tính toán tăng trưởng (`GrowthRate`) dựa trên kỳ trước có cùng độ dài ngày (`PreviousPayoutAmount`).
    - Quy đổi chuẩn theo múi giờ Việt Nam (`Asia/Ho_Chi_Minh` UTC+7).
  - Expose API: `GET /merchant/revenue-report` phân quyền `RequireAuthorization("MerchantOnly")`.
- [x] **4.3. Kiểm thử Phase 4**:
  - Đã tích hợp trong `tests/smoke/merchant-features.ps1` kiểm tra tính toán doanh thu, hoa hồng, thực nhận, tăng trưởng và lọc theo các kỳ PASS 100%.

---

### Phase 5: Cảnh báo vi phạm SLA Merchant ("Overdue Attention") (`GET /merchant/sla-alerts`) - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Hỗ trợ panel Overdue Attention giúp tiệm giặt không bỏ sót đơn hàng trễ hạn.*

- [x] **5.1. Thiết kế DTO (`MerchantSlaAlertsDto` & `MerchantPriorityOrderDto`)**:
  - `TotalAlerts`: Tổng số đơn cần chú ý gấp.
  - `CollectionAlertsCount`: Số đơn quá hạn lấy đồ (đơn `Deposited` > `MerchantCollectSlaHours` giờ).
  - `ProcessingAlertsCount`: Số đơn quá hạn giặt sấy (đơn đang xử lý quá `EstimatedFinishAt`).
  - `ReturnAlertsCount`: Số đơn quá hạn trả đồ (đơn `Paid` > `MerchantReturnSlaHours` giờ).
  - `PriorityOrders`: Danh sách đơn ưu tiên sắp xếp theo `OverdueMinutes` giảm dần, kèm `SuggestedAction` cụ thể.
- [x] **5.2. Triển khai Service & Controller (`MerchantAppService`)**:
  - Lấy cấu hình SLA động từ `SystemSettingProvider` (`MerchantCollectSlaHours`, `MerchantReturnSlaHours`).
  - Phương thức `GetSlaAlertsAsync` lọc và tính toán số phút trễ hạn theo giờ server chuẩn VN.
  - Expose API: `GET /merchant/sla-alerts` phân quyền `RequireAuthorization("MerchantOnly")`.
- [x] **5.3. Kiểm thử Phase 5**:
  - Đã tích hợp trong `tests/smoke/merchant-features.ps1` kiểm tra phân loại cảnh báo quá hạn và gợi ý hành động PASS 100%.

---

### Phase 6: Tích hợp, Kiểm thử toàn diện & Docker Build - [ĐÃ HOÀN THÀNH]
*Mục tiêu: Đảm bảo toàn bộ hệ thống hoạt động ổn định, không lỗi hồi quy.*

- [x] **6.1. Viết Smoke Test Script**:
  - Tạo script toàn diện `tests/smoke/merchant-features.ps1` kiểm tra tự động toàn bộ 6 bước:
    1. Đăng nhập và xác thực các vai trò.
    2. Hồ sơ tiệm giặt (`GET /merchant-profile/` với `City`, tọa độ, `JoinedAt`, `PublicRatingCount`).
    3. Snapshot hoa hồng (`CommissionRateApplied`, `CommissionBaseAmount`, `CommissionAmount`, `MerchantNetAmount`).
    4. Dashboard tổng hợp (`GET /merchant/dashboard` đủ 14 trạng thái, ẩn danh PII khách hàng, bộ lọc Hôm nay / Tuần này / Tháng này / Tùy chọn).
    5. Báo cáo doanh thu & tăng trưởng (`GET /merchant/revenue-report` chuỗi ngày và tỷ lệ tăng trưởng).
    6. Cảnh báo quá hạn SLA (`GET /merchant/sla-alerts` Overdue Attention).
- [x] **6.2. Kiểm tra Swagger Documentation**:
  - Toàn bộ endpoint xuất hiện đầy đủ trên Swagger OpenAPI với mô tả chi tiết, schema chuẩn.
- [x] **6.3. Docker & Release Build**:
  - Build Release Docker container `washgo-be` hoàn tất không lỗi, container hoạt động ổn định.

---

## 3. THỨ TỰ THỰC HIỆN

Chúng ta sẽ tiến hành lần lượt:
1. **Bước 1**: Triển khai **Phase 1** (Cải tiến Hồ sơ Merchant: City, Lat/Long, JoinedAt, PublicRatingCount).
2. **Bước 2**: Triển khai **Phase 2** (Snapshot hoa hồng theo đơn `CommissionRateApplied` & EF Core).
3. **Bước 3**: Triển khai **Phase 3** (Dashboard tổng hợp có bộ lọc thời gian Ngày/Tuần/Tháng/Custom `GET /merchant/dashboard`).
4. **Bước 4**: Triển khai **Phase 4** (Báo cáo doanh thu & tăng trưởng `GET /merchant/revenue-report`).
5. **Bước 5**: Triển khai **Phase 5** (Cảnh báo quá hạn SLA `GET /merchant/sla-alerts`).
6. **Bước 6**: Chạy smoke test toàn diện và build docker.
