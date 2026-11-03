using System.Linq;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    public static class DeliveryTaskRules
    {
        /// <summary>Đơn đang có nhiệm vụ lấy/trả chưa hoàn tất của Shipper được gán.</summary>
        public static IQueryable<WashOrder> ActiveDeliveries(IQueryable<WashOrder> orders) =>
            orders.Where(x => !x.IsDeleted && x.ShipperId != null &&
                (x.Status == WashOrderStatus.Deposited && x.AssignedTaskType == DeliveryTaskType.Collect ||
                 x.Status == WashOrderStatus.Paid && x.AssignedTaskType == DeliveryTaskType.Return ||
                 x.Status == WashOrderStatus.Cancelled && x.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant &&
                    x.AssignedTaskType == DeliveryTaskType.Return));

        /// <summary>Cùng điều kiện với <see cref="ActiveDeliveries"/> cho một đơn đã tải về.</summary>
        public static bool IsActive(bool hasShipper, WashOrderStatus status, DeliveryTaskType? task, CancelledGoodsStatus goods) =>
            hasShipper &&
            (status == WashOrderStatus.Deposited && task == DeliveryTaskType.Collect ||
             status == WashOrderStatus.Paid && task == DeliveryTaskType.Return ||
             status == WashOrderStatus.Cancelled && goods == CancelledGoodsStatus.AtMerchant && task == DeliveryTaskType.Return);
    }
}
