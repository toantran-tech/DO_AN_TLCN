using System.Linq;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    public static class DeliveryTaskRules
    {
        public static IQueryable<DeliveryTask> ActiveAssignments(IQueryable<DeliveryTask> tasks, IQueryable<WashOrder> orders)
        {
            var active = ActiveDeliveries(orders);
            return tasks.Where(x => x.Status == DeliveryTaskStatus.Assigned &&
                active.Any(o => o.Id == x.OrderId && o.ShipperId == x.ShipperId && o.AssignedTaskType == x.TaskType));
        }
        /// <summary>Đơn đang có nhiệm vụ lấy/trả chưa hoàn tất của Shipper được gán.</summary>
        public static IQueryable<WashOrder> ActiveDeliveries(IQueryable<WashOrder> orders) =>
            orders.Where(x => !x.IsDeleted && x.ShipperId != null &&
                (x.Status == WashOrderStatus.Deposited && x.AssignedTaskType == DeliveryTaskType.Collect ||
                 x.Status == WashOrderStatus.Paid && x.AssignedTaskType == DeliveryTaskType.Return ||
                 (x.Status == WashOrderStatus.ReadyToReturn || x.Status == WashOrderStatus.TransferringToStorage) && x.AssignedTaskType == DeliveryTaskType.StorageTransfer ||
                 (x.Status == WashOrderStatus.MovedToStorage || x.Status == WashOrderStatus.ReturningFromStorage) && x.AssignedTaskType == DeliveryTaskType.StorageReturn ||
                 x.Status == WashOrderStatus.Cancelled && x.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant &&
                    x.AssignedTaskType == DeliveryTaskType.Return));

        /// <summary>Cùng điều kiện với <see cref="ActiveDeliveries"/> cho một đơn đã tải về.</summary>
        public static bool IsActive(bool hasShipper, WashOrderStatus status, DeliveryTaskType? task, CancelledGoodsStatus goods) =>
            hasShipper &&
            (status == WashOrderStatus.Deposited && task == DeliveryTaskType.Collect ||
             status == WashOrderStatus.Paid && task == DeliveryTaskType.Return ||
             (status == WashOrderStatus.ReadyToReturn || status == WashOrderStatus.TransferringToStorage) && task == DeliveryTaskType.StorageTransfer ||
             (status == WashOrderStatus.MovedToStorage || status == WashOrderStatus.ReturningFromStorage) && task == DeliveryTaskType.StorageReturn ||
             status == WashOrderStatus.Cancelled && goods == CancelledGoodsStatus.AtMerchant && task == DeliveryTaskType.Return);
    }
}
