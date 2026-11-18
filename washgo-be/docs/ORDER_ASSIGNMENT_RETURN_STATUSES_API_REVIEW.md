# Bàn giao FE — trạng thái phân công, giặt tuần tự và trả đồ sau thanh toán

Ngày 18/11/2026. Thực hiện `Docs/cần làm/BACKEND_ORDER_REMAINING.md`. Các số trạng thái 1–15/99 giữ nguyên ý nghĩa; thêm bốn giá trị mới.

## Enum `WashOrderStatus` mới

| Số | Tên | Nhãn VI đề xuất | Khi nào |
| --- | --- | --- | --- |
| 16 | MerchantAssigned | Đã phân công tiệm giặt | Admin phân công thủ công/tự động thành công (từ Deposited) |
| 17 | CollectionShipperAssigned | Đã phân công Shipper lấy đồ | Merchant/Admin phân công chuyến Collect (từ MerchantAssigned) |
| 18 | ReadyForReturnDispatch | Sẵn sàng trả đồ | Merchant xác nhận đồ đã đóng gói xong tại tiệm (từ Paid) |
| 19 | ReturnShipperAssigned | Đã phân công Shipper trả đồ | Merchant/Admin phân công chuyến Return (từ ReadyForReturnDispatch) |

ReadyToReturn (10) giữ nghĩa cũ (Shipper đã đặt đồ vào ô locker trả, PickupDeadline bắt đầu từ đây); FE đổi nhãn thành **"Chờ khách nhận đồ"**.

Thứ tự nghiệp vụ (không sắp theo số):
`1 → 2 → 16 → 17 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 18 → 19 → 10 → (14 → 13 → 15 khi lưu giữ) → 11 → 12`; 99 là nhánh hủy. FE không dùng `status <= Completed` hay `index + 1`.

Mô tả Customer: 16 "WashGo đã phân công tiệm giặt phụ trách đơn hàng của bạn."; 17 "Shipper đã được phân công đến locker lấy đồ và chuyển về tiệm giặt."; 18 "Tiệm giặt đã chuẩn bị đồ xong, đang chờ phân công Shipper trả về locker."; 19 "Shipper đã được phân công mang đồ về locker để bạn nhận lại."; 10 "Đồ đã có tại locker. Bạn có thể đến nhận theo hướng dẫn."

## Chuyển trạng thái

- Giặt tuần tự: chỉ Collected→Washing→Drying→Ironing→Folding→AwaitingPayment, mỗi lần đúng một bước (PUT `/wash-order/status`). Bỏ bước, lùi bước hoặc gửi lại cùng bước → 400 `InvalidTransition`. `allowedActions.allowedNextStatuses` chỉ có đúng bước kế tiếp; điều kiện cân thực tế giữ nguyên. PUT `/status` không nhận 16–19.
- Phân công tiệm (`/order-assignment/...`): Deposited (chưa có tiệm) → MerchantAssigned, cùng transaction với snapshot hoa hồng; một event `MerchantAssigned` có fromStatus 2, status 16. Kết quả thao tác (`resultStatus`) là 16.
- Phân công Shipper (PUT `/wash-order/assign-shipper`, giữ payload cũ): MerchantAssigned → CollectionShipperAssigned (Collect) hoặc ReadyForReturnDispatch → ReturnShipperAssigned (Return); một event `ShipperAssigned` có fromStatus/status mới và taskType. Đổi Shipper giữ nguyên 17/19, ghi `ShipperReassigned` (previousShipperId), task cũ chuyển Reassigned nên mã/credential cũ hết hiệu lực; mốc phân công đầu tiên trong workflow không đổi. Chuyến trả của đơn hủy (Cancelled + AtMerchant) và StorageTransfer/StorageReturn giữ luồng cũ.
- Paid không phân công trả trực tiếp: `assignShipperReasonCode = ReturnNotReady`, gọi assign → 400.
- Shipper lấy đồ chỉ khi đơn 17 và đúng task Collect; trả đồ chỉ khi đơn 19 và đúng task Return (hoặc đơn hủy có đồ tại tiệm). Trả đồ thành công → 10 và đặt PickupDeadline; thất bại (hết ô...) giữ 19 và task chưa hoàn thành.
- Hủy đơn của Customer: Deposited/16/17 tính như Deposited (đồ còn ở locker gửi, CancelledGoodsStatus = AtDepositLocker, task Collect bị hủy).

## Endpoint mới

Cả hai nhận `expectedRevision` (hoặc If-Match), `expectedStatus`, `operationId` (hoặc Idempotency-Key) như các thao tác đơn khác: lệch → 409 kèm đơn hiện tại; gửi lại cùng operationId → 200 `meta.replayed=true`, không ghi thêm event/thông báo. Tra kết quả qua GET `/wash-order/{id}/operations/{operationId}`. Response: `BaseResponse<WashOrderDetailDto>` có `allowedActions` mới.

`POST /wash-order/{id}/confirm-return-ready` — Merchant phụ trách đơn (policy MerchantOnly; Admin 403).

```json
{ "expectedRevision": 12, "expectedStatus": 9, "operationId": "…", "note": "Đã đóng gói 2 túi" }
```

Paid → ReadyForReturnDispatch, event `ReturnReadyConfirmed` (fromStatus 9, status 18, actor, timestamp thật). Chỉ khi thanh toán đã được BE xác nhận (`paymentStatus = Paid`). `note` tùy chọn ≤ 500. Lặp lại khi đã 18 → 400 `WrongStage`.

`POST /wash-order/{id}/unassign-shipper` — Merchant hoặc Admin (MerchantOrAdmin).

```json
{ "expectedRevision": 15, "expectedStatus": 17, "operationId": "…", "reason": "Shipper báo bận" }
```

CollectionShipperAssigned → MerchantAssigned hoặc ReturnShipperAssigned → ReadyForReturnDispatch (không về Paid), event `ShipperUnassigned` (taskType, previousShipperId). Đóng task (Cancelled), xóa Shipper/mã nhiệm vụ của đơn trong cùng transaction; Shipper cũ nhận thông báo. Không có chuyến đang chờ → 400 `NoActiveAssignment`. BE không hỗ trợ đổi/hủy tiệm sau phân công.

## `allowedActions` bổ sung

| Trường | Ý nghĩa |
| --- | --- |
| `canAssignShipper` / `assignTaskType` | true ở 16/17 (Collect), 18/19 (Return), Cancelled+AtMerchant (Return). Reason code thêm `ReturnNotReady` ở Paid |
| `hasAssignedShipper` | đang có Shipper cho chuyến này; phân công nữa là đổi người |
| `canUnassignShipper`, `unassignShipperReasonCode` | hủy chuyến ở 17/19 (`RoleNotAllowed`, `NoActiveAssignment`) |
| `canConfirmReturnReady`, `confirmReturnReadyReasonCode`, `confirmReturnReadyReason` | Merchant ở Paid (`RoleNotAllowed`, `WrongStage`, `NotPaid`) |

`isNew` của Merchant: đơn 16 (chờ phân công Shipper lấy đồ).

## Tiến độ, đếm và thông báo

- `workflowSteps` có 19 bước theo thứ tự nghiệp vụ ở trên (thêm 16, 17, 18, 19 và Returned 11); đơn mới có 16 bước áp dụng. Mỗi bước thêm `basis`: `StatusChange` (thời điểm chuyển trạng thái thật), `AssignmentEvent` (đơn cũ: thời điểm của event MerchantAssigned/ShipperAssigned ghi trước khi có trạng thái riêng), `NotRecorded` (đơn cũ đã qua mốc nhưng không có thời điểm, `timestamp = null`), null khi chưa qua. Bước giặt mà đơn cũ đã bỏ qua giữ quy tắc hiển thị cũ. progressPercent/ETA chỉ đổi khi chuyển trạng thái thật; đổi Shipper không dịch mốc.
- `statusCounts` (summary đơn, dashboard Merchant) thêm `merchantAssigned`, `collectionShipperAssigned`, `readyForReturnDispatch`, `returnShipperAssigned`; `byStatus` có đủ mọi trạng thái (kể cả 14, 15) và `byGroup` không trùng nhau: Pending, Incoming (2+16+17), Processing (3–7), AwaitingPayment, ReturnPreparation (9), ReturnDispatch (18+19), AwaitingPickup (10), Storage (13+14+15), Finished (11+12), Cancelled. Tổng `byStatus` = tổng `byGroup` = `total`.
- Dashboard Admin thêm `waitingCollectAssignment` (16), `waitingCollection` (17), `waitingReturnPreparation` (9), `waitingReturnAssignment` (18), `waitingReturnDelivery` (19), `awaitingCustomerPickup` (10); `waitingAssignment` vẫn là Deposited chưa có tiệm; `inProgress` gồm thêm 18, 19. `currentByStatus` có các số mới.
- SLA lấy đồ vẫn tính từ DepositedAt qua 2/16/17; SLA trả đồ vẫn tính từ PaidAt qua 9/18/19 (không reset khi xác nhận/phân công/đổi người). Nhắc Shipper lấy đồ ở 17.
- Thông báo `messageKey` mới: `merchantAssigned.merchant`, `merchantAssigned.customer`, `shipperAssigned.collect.customer`, `shipperAssigned.return.customer`, `shipperReassigned.collect.previousShipper`, `shipperReassigned.return.previousShipper`, `shipperUnassigned.collect.shipper`, `shipperUnassigned.return.shipper`, `returnReadyConfirmed.customer` (type 50 OrderEvent). Admin nhận `orderEvent.{eventType}` cho mọi event (gồm `ReturnReadyConfirmed`, `ShipperUnassigned`). Customer chỉ nhận lời mời đến nhận (`returnedToLocker.customer`) sau khi đồ đã vào locker. Nội dung Customer không có tên/ID nhân sự.

## Dữ liệu cũ và phối hợp phiên bản

Migration `OrderAssignmentAndReturnStatuses` (không sửa timeline, không tạo task/event):

- Deposited có tiệm và phân công Collect hợp lệ (Shipper cùng tiệm, không mâu thuẫn với task đang mở) → 17; Deposited có tiệm còn lại → 16.
- Paid đã thanh toán có phân công Return hợp lệ → 19; Paid chưa có chuyến trả giữ 9 (Merchant xác nhận sẵn sàng theo luồng mới). Đơn đã 10/11/12, đơn hủy và đơn lưu giữ không đổi.

FE cũ chưa có số 16–19 sẽ hiển thị trạng thái lạ: cần phát hành FE cùng lúc với BE (hoặc FE thêm enum trước, BE deploy sau). Không suy trạng thái từ merchantId/shipperId.
