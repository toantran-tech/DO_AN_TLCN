using System.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Luật thao tác của Merchant/Admin trên đơn: dùng chung cho kiểm tra khi ghi (status, actual-weight, assign-shipper)
    /// và cho metadata allowedActions trả về FE, để hai nơi không lệch nhau khi chính sách đổi.
    /// </summary>
    public static class OrderActionRules
    {
        public readonly record struct Block(string Code, string Message);

        /// <summary>Các bước xử lý giặt Merchant được chuyển qua PUT /wash-order/status.</summary>
        public static readonly WashOrderStatus[] ProcessingStatuses =
        [
            WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Ironing,
            WashOrderStatus.Folding, WashOrderStatus.AwaitingPayment
        ];

        /// <summary>Chỉ đúng bước kế tiếp: Collected → Washing → Drying → Ironing → Folding → AwaitingPayment.</summary>
        public static bool IsValidTransition(WashOrderStatus current, WashOrderStatus next) => current switch
        {
            WashOrderStatus.Collected => next == WashOrderStatus.Washing,
            WashOrderStatus.Washing => next == WashOrderStatus.Drying,
            WashOrderStatus.Drying => next == WashOrderStatus.Ironing,
            WashOrderStatus.Ironing => next == WashOrderStatus.Folding,
            WashOrderStatus.Folding => next == WashOrderStatus.AwaitingPayment,
            _ => false
        };

        /// <summary>
        /// Chuyến được phân công ở trạng thái hiện tại: lấy đồ sau khi Admin phân công tiệm, trả đồ sau khi Merchant
        /// xác nhận sẵn sàng; đơn hủy có đồ ở tiệm vẫn dùng chuyến trả riêng và giữ Cancelled.
        /// </summary>
        public static DeliveryTaskType? AssignableTask(WashOrder order) => order.Status switch
        {
            WashOrderStatus.MerchantAssigned or WashOrderStatus.CollectionShipperAssigned => DeliveryTaskType.Collect,
            WashOrderStatus.ReadyForReturnDispatch or WashOrderStatus.ReturnShipperAssigned
                when order.PaymentStatus == PaymentStatus.Paid => DeliveryTaskType.Return,
            WashOrderStatus.Cancelled when order.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant => DeliveryTaskType.Return,
            _ => null
        };

        /// <summary>Trạng thái sau khi phân công thành công; đổi Shipper hoặc chuyến của đơn hủy giữ nguyên trạng thái.</summary>
        public static WashOrderStatus StatusAfterAssign(WashOrderStatus status) => status switch
        {
            WashOrderStatus.MerchantAssigned => WashOrderStatus.CollectionShipperAssigned,
            WashOrderStatus.ReadyForReturnDispatch => WashOrderStatus.ReturnShipperAssigned,
            _ => status
        };

        /// <summary>Trạng thái khi hủy phân công chuyến chưa hoàn thành; null = không hỗ trợ hủy ở trạng thái này.</summary>
        public static WashOrderStatus? StatusAfterUnassign(WashOrder order) => order.Status switch
        {
            WashOrderStatus.CollectionShipperAssigned when order.AssignedTaskType == DeliveryTaskType.Collect => WashOrderStatus.MerchantAssigned,
            WashOrderStatus.ReturnShipperAssigned when order.AssignedTaskType == DeliveryTaskType.Return => WashOrderStatus.ReadyForReturnDispatch,
            _ => null
        };

        /// <summary>Lý do không được chuyển đơn sang <paramref name="next"/>; null = được phép.</summary>
        public static Block? StatusChange(WashOrder order, string role, WashOrderStatus next)
        {
            if (role is not ("Merchant" or "Admin"))
                return new("RoleNotAllowed", "Chỉ Merchant hoặc Admin được cập nhật các bước xử lý giặt.");
            if (role == "Merchant" && !ProcessingStatuses.Contains(next))
                return new("RoleNotAllowed", "Vai trò hiện tại không được thực hiện bước này.");
            if (!IsValidTransition(order.Status, next))
                return new("InvalidTransition", $"Không thể chuyển trạng thái từ {order.Status} sang {next}.");
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue &&
                next is WashOrderStatus.Washing or WashOrderStatus.AwaitingPayment)
                return new("WeightRequired", "Merchant cần cập nhật số kg thực tế và chốt giá trước khi giặt hoặc yêu cầu thanh toán.");
            return null;
        }

        /// <summary>Lý do không được cân/chốt giá; null = được phép. Cần đơn đã nạp Items.</summary>
        public static Block? Weigh(WashOrder order, string role)
        {
            if (role != "Merchant")
                return new("RoleNotAllowed", "Chỉ Merchant của đơn được cân và chốt giá.");
            if (order.Status != WashOrderStatus.Collected || order.PaymentStatus != PaymentStatus.Unpaid)
                return new("WrongStage", "Chỉ cân và chốt giá khi đơn đã lấy về tiệm, chưa bắt đầu giặt và chưa thanh toán.");
            if (order.IncludedWeightKg is null || order.BaseProcessingHours is null)
                return new("LegacyPricing", "Đơn cũ chưa có thông tin giá gói; không tự áp dụng quy tắc mới để đổi giá đơn này.");
            if (order.IncludedWeightKg <= 0 || !order.Items.Any(x => x.IsWeightBased))
                return new("NoWeightService", "Đơn chỉ có dịch vụ theo chiếc/đôi, không áp dụng chốt giá theo kg.");
            return null;
        }

        /// <summary>Lý do không được phân công Shipper; null = được phép cho nhiệm vụ <paramref name="task"/>.</summary>
        public static Block? Assign(WashOrder order, string role, out DeliveryTaskType? task)
        {
            task = AssignableTask(order);
            if (role is not ("Merchant" or "Admin"))
                return new("RoleNotAllowed", "Chỉ Merchant hoặc Admin được phân công Shipper.");
            if (order.MerchantId == null)
                return new("NoMerchant", "Đơn chưa được Admin phân công cho Merchant.");
            if (order.Status == WashOrderStatus.Paid)
                return new("ReturnNotReady", "Merchant cần xác nhận sẵn sàng trả đồ trước khi phân công Shipper trả đồ.");
            if (!task.HasValue)
                return new("WrongStage", "Chỉ phân công khi đơn đã được phân công tiệm (lấy đồ) hoặc tiệm đã xác nhận sẵn sàng trả đồ.");
            return null;
        }

        /// <summary>Lý do không được hủy phân công chuyến lấy/trả chưa hoàn thành; null = được phép.</summary>
        public static Block? Unassign(WashOrder order, string role)
        {
            if (role is not ("Merchant" or "Admin"))
                return new("RoleNotAllowed", "Chỉ Merchant hoặc Admin được hủy phân công Shipper.");
            if (!order.ShipperId.HasValue || StatusAfterUnassign(order) == null)
                return new("NoActiveAssignment", "Đơn không có chuyến lấy đồ hoặc trả đồ đang chờ Shipper thực hiện.");
            return null;
        }

        /// <summary>Lý do Merchant chưa được xác nhận sẵn sàng trả đồ; null = được phép (Paid → ReadyForReturnDispatch).</summary>
        public static Block? ConfirmReturnReady(WashOrder order, string role)
        {
            if (role != "Merchant")
                return new("RoleNotAllowed", "Chỉ Merchant phụ trách đơn được xác nhận đồ đã chuẩn bị xong.");
            if (order.Status != WashOrderStatus.Paid)
                return new("WrongStage", "Chỉ xác nhận sẵn sàng trả đồ khi đơn đã thanh toán và còn ở tiệm.");
            if (order.PaymentStatus != PaymentStatus.Paid)
                return new("NotPaid", "Thanh toán của đơn chưa được hệ thống xác nhận.");
            return null;
        }

        /// <summary>Lý do không được ghi nhận chuyển kho; null = được phép (cùng luật với POST /wash-order/move-to-storage).</summary>
        public static Block? MoveToStorage(WashOrder order, string role, DateTime now)
        {
            if (order.StorageWorkflowVersion >= 1)
                return new("UseStorageWorkflow", "Dùng luồng kho có phân công và xác nhận bàn giao.");
            if (role is not ("Admin" or "Shipper"))
                return new("RoleNotAllowed", "Chỉ Shipper hoặc Admin được ghi nhận chuyển kho.");
            if (order.MovedToStorageAt.HasValue)
                return new("AlreadyMoved", "Đơn hàng đã được ghi nhận chuyển kho trước đó.");
            if (order.Status != WashOrderStatus.ReadyToReturn)
                return new("WrongStage", "Chỉ chuyển kho cho đồ đang nằm tại locker trả.");
            if (!OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, now))
                return new("NotYetDue", $"Chỉ chuyển kho khi đã quá {SystemSetting.StorageTransferAfterHours} giờ kể từ hạn nhận đồ.");
            return null;
        }

        /// <summary>Lý do Admin chưa được hủy đơn quá hạn gửi đồ (deadline lưu trên đơn; đơn cũ đã backfill).</summary>
        public static Block? CancelExpiredDeposit(WashOrder order, string role, DateTime now)
        {
            if (role != "Admin")
                return new("RoleNotAllowed", "Chỉ Admin được hủy đơn quá hạn gửi đồ.");
            if (order.Status != WashOrderStatus.Pending)
                return new("NotPending", "Chỉ hủy theo luồng này khi đơn còn chờ gửi đồ.");
            if (order.DepositDeadline is not { } deadline || now <= deadline)
                return new("NotExpired", "Đơn chưa quá hạn gửi đồ.");
            return null;
        }

        public static DateTime? TransferToStorageEligibleAt(WashOrder order) =>
            order.PickupDeadline?.AddHours(SystemSetting.StorageTransferAfterHours);

        /// <summary>Metadata cho FE; null với vai trò không thao tác (Customer, Shipper).</summary>
        public static OrderAllowedActionsDto? Build(WashOrder order, string role)
        {
            if (role is not ("Merchant" or "Admin"))
                return null;
            var options = ProcessingStatuses.Select(status =>
            {
                var block = StatusChange(order, role, status);
                return new OrderStatusOptionDto
                {
                    Status = status, Allowed = block == null, ReasonCode = block?.Code, Reason = block?.Message
                };
            }).ToList();
            var weigh = Weigh(order, role);
            var assign = Assign(order, role, out var task);
            var storage = MoveToStorage(order, role, DateTime.UtcNow);
            var expired = CancelExpiredDeposit(order, role, DateTime.UtcNow);
            var unassign = Unassign(order, role);
            var ready = ConfirmReturnReady(order, role);
            return new OrderAllowedActionsDto
            {
                AllowedNextStatuses = options.Where(x => x.Allowed).Select(x => x.Status).ToList(),
                StatusOptions = options,
                CanUpdateWeight = weigh == null,
                UpdateWeightReasonCode = weigh?.Code,
                UpdateWeightReason = weigh?.Message,
                WeightAlreadySet = order.ActualWeightKg.HasValue,
                CanAssignShipper = assign == null,
                AssignTaskType = assign == null ? task : null,
                AssignShipperReasonCode = assign?.Code,
                AssignShipperReason = assign?.Message,
                HasAssignedShipper = assign == null && order.ShipperId.HasValue && order.AssignedTaskType == task,
                CanUnassignShipper = unassign == null,
                UnassignShipperReasonCode = unassign?.Code,
                CanConfirmReturnReady = ready == null,
                ConfirmReturnReadyReasonCode = ready?.Code,
                ConfirmReturnReadyReason = ready?.Message,
                CanMoveToStorage = storage == null,
                MoveToStorageReasonCode = storage?.Code,
                MoveToStorageReason = storage?.Message,
                TransferToStorageEligibleAt = TransferToStorageEligibleAt(order),
                CanCancelExpiredDeposit = expired == null,
                CancelExpiredDepositReasonCode = expired?.Code
            };
        }
    }
}
