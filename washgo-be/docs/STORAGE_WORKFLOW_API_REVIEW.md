# Bàn giao BE: trạng thái Shipper và lưu kho tại tiệm

Ngày 14/11/2026. Kho là khu lưu đồ của Merchant xử lý đơn, dùng địa chỉ tiệm snapshot; không có registry/CRUD kho. API chỉ mô phỏng giao nhận, không xác nhận cảm biến hay cửa tủ thật.

## Hợp đồng chung

- JWT đúng role và đúng chủ đơn/tiệm/Shipper được gán. Admin điều phối cần `reason`; Admin không gọi thay bốn bước Shipper.
- Các POST ghi kho cần `operationId` UUID và `expectedRevision` lấy từ `WashOrder.revision` hoặc `StorageCycleDto.orderRevision`; có thể gửi `expectedStatus`. Cùng operation/payload trả kết quả đã lưu, khác payload trả 409. Request mới sau lỗi cần tải lại revision. Lưu operationId khi timeout, tra GET `/storage/{orderId}/operations/{operationId}` trước khi gửi thao tác khác.
- 400: dữ liệu/mã không hợp lệ; 403/404: sai quyền hoặc tài nguyên; 409: revision/phân công/bước/quote thay đổi hoặc thiếu tiền/ô. Đọc `meta.code`. Mã sai trả thêm revision, remainingAttempts, lockoutUntil; 5 lần khóa 5 phút, không mất bộ đếm do rollback.
- Chỉ gửi **một** `pinCode` hoặc `qrCode`. Mã nhiệm vụ lấy ở GET `/shipper-tasks/{taskId}/credentials`, không dùng mã Customer. Phân công lại đổi task/mã và reset lượt sai, giữ SLA của chuyến.
- Customer không nhận địa chỉ tiệm, biên nhận nội bộ, nhân sự hoặc task credentials. Không hiển thị nội dung exception nội bộ cho khách.
- `can*` là gợi ý tại thời điểm đọc; server kiểm tra lại dưới khóa. `canDepart`/`departBlockedReason` còn xét báo phí và thanh toán. Không dùng capability cũ để bỏ kiểm tra 409.

## Enum bổ sung

| Enum | Giá trị |
|---|---|
| WashOrderStatus | 14 TransferringToStorage; 15 ReturningFromStorage. Giữ 10 ReadyToReturn, 13 MovedToStorage và enum cũ |
| DeliveryTaskType | 3 StorageTransfer; 4 StorageReturn; 1/2 vẫn Collect/Return |
| StorageCycleState | 1 AwaitingCollection; 2 InboundTransit; 3 Stored; 4 OutboundTransit; 5 Closed; 6 Cancelled |
| StorageReturnState | 1 Requested; 2 Assigned; 3 InTransit; 4 Delivered; 5 Cancelled |
| AdminAlertType | 12 StorageTransferOverdue; 13 StorageReturnOverdue |

FE cần bổ sung nhãn/filter/count cho enum mới, không coi mọi task khác Collect là Return. Dashboard/task DTO đã có số liệu và capability riêng cho nhiệm vụ kho.

## API đọc và phân công

| API | Quyền / sử dụng |
|---|---|
| POST `/storage/filter` | Admin/Merchant; body keyword mã đơn, state, fromDate/toDate Unix, page từ 1, take 1–100. Mặc định tạo mới trước, ID phụ để phân trang ổn định; không hỗ trợ sort/column filter tùy ý |
| GET `/storage/summary` | Admin/Merchant; đếm theo trạng thái, đã scope tiệm |
| GET `/storage/{orderId}/cycles?page=1&take=20` | Lịch sử phân trang theo quyền. `frozenLockerFee`/`frozenWarehouseFee` riêng vòng; `feeTotal`/`feePaid`/`outstanding` là tổng nghĩa vụ hiện tại của đơn, không cộng lại từng dòng lịch sử |
| GET `/storage/{orderId}/shippers` | Admin/Merchant; ứng viên active, Available, thuộc tiệm và không có chuyến khác |
| POST `/storage/{orderId}/assign` | Admin/Merchant; guard + shipperId. Chuyển kho khi locker quá 72h tính từ pickupDeadline; giao lại khi đã có yêu cầu Customer |
| POST `/storage/{orderId}/cancel-assignment` | Admin/Merchant; guard, chỉ trước lấy/xuất kho; không đổi phí hay giải phóng ô |
| POST `/storage/{orderId}/exception` | Admin/Merchant; guard + reason 1–500 ký tự. Lưu sự cố, báo Admin, giữ nguyên người giữ hàng và trạng thái để phục hồi có kiểm soát |

## Locker → tiệm

1. Merchant phân công: ô vẫn Occupied, order vẫn 10. SLA 4 giờ từ lần phân công đầu.
2. POST `/storage/{orderId}/collect`: Shipper gửi guard + taskId + PIN/QR. Giải phóng đúng ô một lần, chốt phí locker, cycle 2/order 14. Có phiên Customer đang mở ô thì trả `PickupInProgress` để hai nhánh không cùng giải phóng.
3. POST `/storage/{orderId}/handover`: cùng guard/task/mã, thêm `receiptReference` 1–120 ký tự. Bàn giao tại tiệm snapshot; lưu actor/giờ/biên nhận, cycle 3/order 13 và hoàn tất task. Lúc này mới thông báo đã lưu kho và bắt đầu phí kho.

Sau collect không hủy/gán người khác bằng thao tác thường. Dùng exception để báo điều phối; không tự giả lập hàng đã về tiệm. Nếu Customer kịp nhận trước collect, xác nhận nhận đồ sẽ hủy cycle/task đang chờ trong cùng transaction.

## Tiệm → locker theo yêu cầu Customer

1. GET `/storage/{orderId}/quote?lockerId=...`: Customer chọn trạm đang hoạt động được tiệm phục vụ; server trả `quoteToken`, fee, expiresAt. Token ký server, gắn owner/order/cycle/locker, hạn 5 phút. Chưa giữ ô, chưa thu tiền.
2. POST `/storage/{orderId}/return-request`: guard + quoteToken. Một yêu cầu hiệu lực/vòng; SLA 24 giờ từ yêu cầu hợp lệ, bao gồm chờ phân công. Gửi lại báo phí cho cùng trạm không reset SLA. Đổi trạm trước xuất kho: hủy yêu cầu cũ rồi lấy quote/tạo yêu cầu mới.
3. Thanh toán tiền giặt và phí còn thiếu qua API payment hiện có. Không có phí giao lại bổ sung. Nếu phí tăng đến lúc xuất kho, lấy quote mới và xác nhận lại bằng return-request, thanh toán phần thiếu; không thu ngầm.
4. Merchant assign Shipper chuyến giao lại. POST `/storage/{orderId}/depart`: Shipper guard + taskId + PIN/QR. Chỉ khi đủ tiền và báo phí đã được chấp thuận; chốt/dừng phí kho, cycle 4/order 15. Sau xuất kho không hủy yêu cầu.
5. POST `/storage/{orderId}/place`: Shipper guard + taskId + PIN/QR. Trong một transaction, khóa trạm, chọn ô Available cùng/lớn hơn ô gửi, giữ/chiếm ô và cập nhật đơn. Hết ô trả `NoSuitableSlot` 409; order vẫn 15, Shipper vẫn giữ đồ, không tự đổi trạm. Thử lại hoặc báo exception.
6. Đặt thành công: cycle Closed, request Delivered, order 10; mã nhận mới, session cũ bị hủy, hạn miễn phí mới 24 giờ từ lúc đặt đồ; lúc này mới thông báo khách đến locker.
7. Customer dùng `/wash-order/{id}/open-pickup-box` → nhận session → `/confirm-pickup` như trước. Chưa xác nhận thì ô còn Occupied; xác nhận mới giải phóng ô, ghi Returned rồi Completed.

Ví dụ body Shipper (thay các placeholder bằng giá trị thật):

```json
{
  "operationId": "<UUID mới>",
  "expectedRevision": 42,
  "taskId": "<task UUID>",
  "pinCode": "<mã nhiệm vụ>",
  "receiptReference": "<chỉ bắt buộc khi handover>"
}
```

## Phí, SLA và các vòng lặp

- Dừng phí locker ở collect; bắt đầu phí kho ở handover; dừng phí kho ở depart. Không tính giờ vận chuyển là giờ lưu kho.
- Dùng chính sách đã snapshot trên đơn: mặc định phí locker ngày 1/2/3 là 10k/20k/50k, sau 72h có mức 100k/ngày hiện hành; phí kho 100k cho mỗi chu kỳ 24 giờ đã bắt đầu. Ngay đúng mốc intake, phần phí kho = 0; sau đó thời gian dương bắt đầu ngày đầu.
- Tổng phải trả = phí các chặng đã chốt + phí chặng hiện tại. Không mất phí các vòng trước khi đặt lại vào locker; tiền đã trả được khấu trừ qua `storageFeeTotal`.
- SLA 4h/24h chỉ cảnh báo/timeline, không chặn giao nhận, không tự phạt tiền. Phân công lại giữ hạn ban đầu. `/admin/alerts` và reminder scan có hai loại cảnh báo kho.

## Shipper tự Available/Offline

PUT `/shipper-profile/work-status` (ShipperOnly):

```json
{"status":3,"expectedRevision":"<revision string từ GET /shipper-profile/>","operationId":"<UUID mới>"}
```

Available=1, Offline=3; không nhận Busy=2. Đang có Collect/Return/StorageTransfer/StorageReturn thì không Offline; không tự ghi đè trạng thái nhân sự Busy/inactive. Dùng `canChangeWorkStatus`, `canGoOffline`, `workStatusBlockedReason`, `goOfflineBlockedReason`. Assignment và đổi status dùng cùng khóa Shipper. Operation lookup dùng cơ chế self-profile hiện có.

## Migration và đơn cũ

- Migration `AddStorageCyclesAndReturnWorkflow` thêm hai bảng và index, giữ đơn đang tồn tại `storageWorkflowVersion=0`; đơn mới được tạo bằng code là 1. Không backfill ngày/biên nhận/vị trí giả hoặc đổi giá hàng loạt.
- V1 chặn `/wash-order/move-to-storage` và `/wash-order/{id}/pickup-storage`; FE chuyển sang storage APIs. V0 còn tương thích route cũ cho đến khi đối chiếu.
- Đơn v0 thực sự đang ở tiệm: Admin đối chiếu bên ngoài rồi POST `/storage/{id}/verify-legacy` với guard, reason, `confirmedAtCurrentShop=true`, `verificationReference`. Giữ mốc vào kho/tiền cũ, basis `LegacyAdminVerifiedCurrentShop`; không tạo handover Shipper giả. Thiếu mốc/địa chỉ thì từ chối, không suy đoán. Sau đối chiếu mới mở quote/giao lại v1.
- Deploy DB migration trước app mới; kiểm tra legacy, backup DB, rồi cập nhật FE hỗ trợ enum trước khi bật UI. Hiện chưa áp migration này lên DB chính/chưa rebuild Docker chính trong cụm này.

## Kiểm chứng và giới hạn

`tests/smoke/Storage` kiểm tra phí/cycle/transit/HMAC/predicate; `storage-lifecycle.ps1` chạy HTTP trên DB riêng, Mock payment, không SMTP ngoài. Suite mới được gắn vào CI. Không dùng smoke có SQL fixture trên production. Bộ hồi quy rộng chờ CI sau push; FE/UAT, Gemini thật theo nguồn mới và sự kiện phần cứng chưa là kết quả đã nghiệm thu.
