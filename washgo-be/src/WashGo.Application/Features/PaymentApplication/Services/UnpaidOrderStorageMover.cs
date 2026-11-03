using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.PaymentApplication.Services
{
    /// <summary>
    /// UC 1.2.4 7b: an order still unpaid 7 days after it became "Chờ thanh toán" is moved to storage. The laundry is
    /// already at the Merchant's shop, so it stays there as storage; the customer pays the laundry and the storage fee,
    /// then picks it up (existing storage pickup flow).
    /// </summary>
    public class UnpaidOrderStorageMover : ITransientDependency
    {
        public const int StorageAfterDays = 7;
        public const int ReminderBeforeHours = 24;

        private readonly IWashOrderRepository _orders;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly NotificationPublisher _notifications;

        public UnpaidOrderStorageMover(IWashOrderRepository orders, IUnitOfWorkManager unitOfWorkManager,
            NotificationPublisher notifications)
        {
            _orders = orders;
            _unitOfWorkManager = unitOfWorkManager;
            _notifications = notifications;
        }

        public static DateTime DeadlineFrom(DateTime awaitingPaymentAt) => awaitingPaymentAt.AddDays(StorageAfterDays);

        /// <summary>Moves the order when it is still unpaid past its deadline; false when nothing changed.</summary>
        public async Task<bool> MoveAsync(Guid orderId, DateTime now)
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orders.GetForUpdateAsync(orderId);
            var order = await _orders.GetWithDetailsAsync(orderId);
            if (order == null || order.Status != WashOrderStatus.AwaitingPayment ||
                order.PaymentStatus != PaymentStatus.Unpaid || order.PaymentDeadline is not { } deadline || now < deadline)
                return false;
            if (!await _orders.TryChangeStatusAsync(order.Id, WashOrderStatus.AwaitingPayment, WashOrderStatus.MovedToStorage))
                return false;

            order.Status = WashOrderStatus.MovedToStorage;
            order.MovedToStorageAt = now;
            order.StorageLocation = $"Kho tiệm {order.MerchantName ?? "Merchant"}";
            order.StorageReference = order.MerchantOrderCode ?? order.OrderCode;
            // The laundry never sat in a return locker, so only the warehouse fee applies from now on.
            order.LockerOverdueFeeAtStorageTransfer = 0m;
            order.OverdueFeeTotal = 0m;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.AwaitingPayment,
                Status = WashOrderStatus.MovedToStorage,
                ActorName = "WashGo",
                ActorRole = "System",
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
                Note = $"Quá hạn thanh toán {StorageAfterDays} ngày; đồ được chuyển sang lưu kho tại {order.StorageLocation}, mã lưu kho {order.StorageReference}."
            });

            await _orders.UpdateAsync(order, autoSave: true);
            await _notifications.UnpaidMovedToStorageAsync(order);
            await uow.CompleteAsync();
            return true;
        }
    }
}
