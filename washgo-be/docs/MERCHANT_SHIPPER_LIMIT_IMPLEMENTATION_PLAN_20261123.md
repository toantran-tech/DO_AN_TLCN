# Kế hoạch giới hạn nhiệm vụ riêng cho từng Shipper

Ngày rà soát và chốt tham số: 23/11/2026. Nguồn yêu cầu: `BACKEND_MERCHANT_SHIPPER_MANAGEMENT.md` trong cùng thư mục, bản thống nhất 22/11 và chốt bổ sung 23/11/2026. Baseline code: `develop`, commit `c46059c`.

Trạng thái triển khai: đã cập nhật BE, migration AddPerShipperTaskLimit, smoke test và tài liệu bàn giao API. Kết quả chạy smoke trên PostgreSQL và FE chờ xác nhận.

## 1. Kết quả đối chiếu source

| Nội dung | Hiện tại | Công việc còn lại |
| --- | --- | --- |
| Nguồn giới hạn | `SystemSetting.MaxActiveTasksPerShipper`, mặc định 3; `ShipperCapacityService.LimitAsync()` đọc chung | Chuyển sang trường riêng trên `Shipper` |
| Quyền chỉnh | Admin ghi qua `PUT /system-setting/dispatch` | Ngừng quyền ghi chung; chỉ Merchant sở hữu được ghi giới hạn riêng |
| Tạo/sửa Shipper | Command chưa có `maxActiveTasks`; Admin và Merchant dùng chung service | Thêm trường và kiểm tra quyền tại service, kể cả đường tạo Shipper từ quản lý tài khoản Admin |
| Sửa hồ sơ | Có khóa Shipper `FOR UPDATE` và `expectedRevision` tùy chọn | Thêm kiểm tra giới hạn sau khóa; cơ chế operation/replay/lookup cho sửa hồ sơ còn thiếu |
| Đếm nhiệm vụ | `DeliveryTaskRules.ActiveDeliveries` đã bao gồm lấy, trả, trả đơn hủy, chuyển kho và giao lại | Tái sử dụng một nguồn đếm; bỏ việc đếm riêng trong quản lý Shipper |
| Phân công | Đơn thường và kho đã khóa Shipper trước kiểm tra sức chứa | Giữ khóa; đọc giới hạn từ chính bản ghi đang khóa |
| Response | Có activeTaskCount/maxActiveTasks/remainingTaskCapacity trên các màn chính | Đồng bộ mọi response, gồm tạo/sửa/replay và lỗi revision; hiện ToDto đơn thuần chưa bổ sung capacity |
| Test/CI | Có `shipper-task-capacity.ps1`, đã được gọi trong `tests/ci/Run-ApiSmoke.ps1` | Sửa test theo quyền Merchant và giới hạn riêng, bổ sung race giảm giới hạn/phân công |
| Tài liệu FE | `SHIPPER_TASK_CAPACITY_API_REVIEW.md` còn mô tả Admin chỉnh chung và cho giảm dưới tải | Thay nội dung sau khi triển khai; không để FE theo hợp đồng cũ |

## 2. Tham số đã chốt ngày 23/11/2026

- Giới hạn hợp lệ **1–20**, số nguyên, bao gồm hai đầu mút; tạo mới mặc định **3**. Người dùng đã đồng ý phương án đề xuất.
- Tạo mới bởi Merchant: không gửi `maxActiveTasks` thì BE áp dụng 3; sửa không gửi thì giữ nguyên. Null tường minh không hợp lệ, không mang nghĩa không giới hạn.
- Admin vẫn có thể tạo hồ sơ Shipper theo chức năng hiện có, nhưng không được chọn giới hạn: BE khởi tạo mặc định; Admin gửi `maxActiveTasks` phải bị từ chối, không âm thầm bỏ qua. Admin sửa trường hồ sơ khác giữ quyền hiện hành.
- Dữ liệu cũ: lấy giá trị lớn nhất giữa giới hạn chung hiện tại và số nhiệm vụ đang hoạt động của từng Shipper. Không đổi, hủy hoặc chuyển nhiệm vụ.
- Nếu dữ liệu cũ có số nhiệm vụ lớn hơn trần đã chốt: phải báo preflight và xử lý rõ trước migration; không âm thầm cắt giới hạn hay nâng trần. Không chạy migration khi chưa giải quyết ngoại lệ.
- Word đang ghi mỗi Shipper chỉ có một chuyến; chưa có giới hạn riêng hay mặc định. Đợt này chốt MD trước, chưa chỉnh Word. Cần đồng bộ UC 2.4.1–2.4.4 khi cập nhật đặc tả sau.

## 3. Chặng 1 — Dữ liệu và quy tắc dùng chung

- [ ] Thêm `Shipper.MaxActiveTasks`, mapping cột `max_active_tasks`; quy tắc validation tập trung ở Domain.Shared, tránh lặp min/max/default.
- [ ] Tạo migration khởi tạo giới hạn riêng cho mọi Shipper, cả bản ghi xóa mềm; dùng cùng điều kiện nhiệm vụ hiệu lực như domain khi tính số nhiệm vụ cũ.
- [ ] Thêm ràng buộc DB theo khoảng đã chốt. Cập nhật snapshot và mọi đường khởi tạo/seed Shipper.
- [ ] Chỉ dùng cấu hình cũ để chuyển dữ liệu một lần, không còn dùng làm fallback runtime. Đề xuất giữ cột cũ ở DB giai đoạn đầu để giảm rủi ro triển khai, bỏ khỏi hợp đồng ghi/đọc hiện hành và đánh dấu deprecated trong tài liệu.
- [ ] Preflight trước triển khai: tải đang có, cấu hình chung, ngoại lệ vượt trần; backup DB trước áp migration. Dừng bản BE cũ ghi dữ liệu trong lúc chuyển đổi.

Hoàn thành khi mỗi Shipper có một giới hạn dương hợp lệ và dữ liệu nhiệm vụ không đổi. Chưa coi là hoàn thành trước khi migration được kiểm tra trên DB thử nghiệm.

## 4. Chặng 2 — Merchant tạo và sửa giới hạn an toàn

- [ ] Bổ sung `maxActiveTasks` vào create/update command. Create không gửi thì mặc định 3; update không gửi thì giữ giá trị cũ. Theo dõi sự hiện diện của trường để từ chối JSON null tường minh, 0, số thập phân và ngoài 1–20.
- [ ] Chỉ Merchant của Shipper được ghi trường này; lấy Merchant từ JWT. Admin/Shipper và endpoint khác không được ghi. Bảo vệ cả ở service, không chỉ authorization route.
- [ ] Khi sửa giới hạn, bắt buộc có revision hiện tại và mã operation; đọc lại bản ghi sau khóa `FOR UPDATE`, kiểm tra revision, rồi đếm tải mới nhất trước mọi mutation hồ sơ.
- [ ] Nếu giới hạn mới nhỏ hơn tải: trả 409 `ShipperTaskLimitBelowActiveCount`; kèm `activeTaskCount`, `maxActiveTasks` hiện tại, `minimumAllowedMaxActiveTasks`, lỗi trường `maxActiveTasks`. Ví dụ: “Shipper đang có 3 nhiệm vụ chưa hoàn tất. Số nhiệm vụ tối đa phải từ 3 trở lên.”
- [ ] Giới hạn bằng tải được lưu, còn chỗ bằng 0. Thất bại không lưu một phần tên/điện thoại/giấy tờ hay giới hạn.
- [ ] Dùng chung AdministrativeWriteGuard hiện có cho operation/replay/lookup của sửa hồ sơ; cùng khóa/cùng payload trả kết quả cũ, khác payload trả 409; kiểm tra quyền truy cập trước replay. Ghi kết quả và thay đổi hồ sơ cùng transaction.
- [ ] Tạo Shipper giữ clientRequestId/header Idempotency-Key. Mở rộng kiểm tra payload replay bao gồm giới hạn: key cũ không được tạo tiếp hoặc âm thầm chấp nhận giới hạn khác. Bàn giao quy tắc tương thích operation đã có trước phiên bản mới.
- [ ] Audit thay đổi giới hạn có actor, thời điểm, giá trị trước/sau; cập nhật revision và trả DTO có đủ số liệu tải.
- [ ] Ngừng ghi `/system-setting/dispatch`, ví dụ trả 410 `DispatchSettingsMovedToShipper` có hướng dẫn chuyển sang Merchant. Service cũ cũng phải chặn, không chỉ bỏ route. Các cấu hình SLA/commission/loyalty giữ chức năng hiện hành.

Hoàn thành khi quyền Merchant được bảo vệ trên server, không thể ghi qua Admin account creation hoặc endpoint cũ, và sửa giới hạn không vượt qua kiểm tra tải/revision.

## 5. Chặng 3 — Phân công và dữ liệu đọc

- [ ] Refactor ShipperCapacityService đọc giới hạn của từng Shipper, bỏ phụ thuộc SystemSettingProvider. Nhận bản ghi đã khóa cho thao tác ghi để không dùng entity đã track từ trước làm nguồn giới hạn cũ.
- [ ] Giữ rule ActiveDeliveries thống nhất; dùng batch query cho danh sách, không gọi query đếm riêng cho từng dòng.
- [ ] Áp dụng cho Collect, Return, trả đồ hủy, StorageTransfer, StorageReturn; mọi đường thêm nhiệm vụ phải khóa cùng hàng Shipper trong cùng transaction với ghi phân công.
- [ ] Không lấy khóa order sau khi đã khóa Shipper trong luồng sửa giới hạn: đếm bằng query để tránh tạo thứ tự khóa đảo ngược với phân công (order rồi Shipper). Kiểm tra không còn đường tạo nhiệm vụ vượt qua khóa này.
- [ ] Tải lại số liệu sau khi chờ khóa; hai phân công tranh chỗ cuối chỉ một thành công. Giảm giới hạn và phân công đồng thời phải tương đương một thứ tự tuần tự hợp lệ.
- [ ] Phân công lại cùng người/cùng chuyến và replay không chiếm thêm chỗ. Giữ điều kiện tài khoản, Merchant, Available và đang làm việc.
- [ ] Đồng bộ DTO quản lý Shipper, hồ sơ, danh sách phân công đơn/kho; mọi response tạo/sửa/replay/revision conflict có số liệu theo cùng quy tắc. remainingTaskCapacity luôn không âm.
- [ ] Giữ 409 `ShipperCapacityExceeded` riêng với lỗi giảm giới hạn, sai Merchant, thiếu quyền và revision cũ. Không tự đổi trạng thái vận hành.

## 6. Chặng 4 — Test và bàn giao

- [ ] Sửa smoke capacity hiện có thay vì tạo các suite trùng nội dung; kiểm tra nó tiếp tục được nối vào CI.
- [ ] Tải 3: lưu 2 bị chặn và toàn bộ hồ sơ giữ nguyên; lưu 3 thành công nhưng không nhận thêm; lưu 4 còn một chỗ.
- [ ] Validate số âm, 0, số thập phân, null/trống theo hợp đồng, ngoài khoảng; tạo mới có/không truyền giới hạn.
- [ ] Hai Shipper có giới hạn khác nhau được hiển thị/kiểm tra độc lập; cùng Merchant và khác Merchant.
- [ ] Merchant khác tiệm, Admin, Shipper tự sửa, Admin tạo qua /admin/accounts và endpoint dispatch cũ đều không ghi được giới hạn trái quyền.
- [ ] Race giảm giới hạn/phân công: nếu gán trước thì giảm dưới tải bị chặn; nếu giảm trước thì phân công vượt mức bị chặn. Hai Merchant cập nhật cùng revision: chỉ một thay đổi được chấp nhận.
- [ ] Replay, timeout/lookup kết quả, operationId dùng lại khác payload, revision cũ, audit không lặp.
- [ ] Đếm đủ năm loại chuyến, hoàn tất/hủy/thay người giải phóng đúng chỗ; không đếm trùng task và order.
- [ ] Migration chuyển đúng giới hạn, không mất lịch sử/nhiệm vụ, bắt được ngoại lệ dữ liệu vượt trần.
- [ ] Cập nhật `SHIPPER_TASK_CAPACITY_API_REVIEW.md`, summary Swagger, `IMPLEMENTATION_CHECKLIST.md` và `NEXT_IMPLEMENTATION_PLAN.md`; ghi rõ test nào mới viết và test nào thực sự đã PASS.
- [ ] FE nhận hợp đồng mới: Merchant form giới hạn, tải x/y, xử lý 409, bỏ cấu hình dispatch ở Admin. Phần sửa FE nằm ngoài phạm vi BE của kế hoạch này.

Theo cách làm đã thống nhất: người dùng chạy test, build Docker và áp migration; agent chuẩn bị source/test/docs cho đợt này. Chia bàn giao theo bốn chặng nhưng chỉ deploy bản tích hợp đủ migration + nguồn giới hạn mới + phân quyền mới; không đưa riêng migration lên môi trường đang có BE cũ ghi dữ liệu.

## 7. Phạm vi không thay đổi

Không thêm lập tuyến, tối ưu lộ trình, SLA mới, giá/phí mới, tự chuyển nhiệm vụ hay tự đổi Available/Busy. Không sửa file tài liệu yêu cầu gốc để biến đề xuất chưa chốt thành yêu cầu đã duyệt. Không khôi phục file `docs/render-staging.env.example` đang bị người dùng xóa trong working tree.
