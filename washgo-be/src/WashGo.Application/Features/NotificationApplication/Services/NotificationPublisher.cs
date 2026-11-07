using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Notification.Interfaces;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.NotificationApplication.Services
{
    /// <summary>
    /// Ghi thông báo trong cùng transaction với nghiệp vụ (rollback thì không có thông báo),
    /// rồi chỉ đẩy real-time sau khi commit. Lỗi đẩy real-time chỉ ghi log, không ảnh hưởng nghiệp vụ.
    /// </summary>
    /// <summary>Template key and arguments stored with a notification so clients can translate it.</summary>
    public record NotificationMessage(string Key, IReadOnlyDictionary<string, string> Args);

    public class NotificationPublisher : ITransientDependency
    {
        private readonly INotificationRepository _notificationRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Role, Guid> _roleRepo;
        private readonly IShipperRepository _shipperRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IRealtimeNotifier _realtime;
        private readonly ILogger<NotificationPublisher> _logger;

        public NotificationPublisher(
            INotificationRepository notificationRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Role, Guid> roleRepo,
            IShipperRepository shipperRepo,
            ILockerRepository lockerRepo,
            IUnitOfWorkManager unitOfWorkManager,
            IRealtimeNotifier realtime,
            ILogger<NotificationPublisher> logger)
        {
            _notificationRepo = notificationRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _shipperRepo = shipperRepo;
            _lockerRepo = lockerRepo;
            _unitOfWorkManager = unitOfWorkManager;
            _realtime = realtime;
            _logger = logger;
        }

        public async Task SendAsync(IEnumerable<Guid?> userIds, NotificationType type, string title, string content,
            string? linkType = null, Guid? linkId = null, NotificationMessage? message = null)
        {
            var args = message?.Args is { Count: > 0 } a ? JsonSerializer.Serialize(a) : null;
            var notifications = userIds.Where(x => x.HasValue && x.Value != Guid.Empty).Select(x => x!.Value).Distinct()
                .Select(userId => new Notification
                {
                    UserId = userId,
                    Type = type,
                    Title = title,
                    Content = content,
                    LinkType = linkType,
                    LinkId = linkId,
                    MessageKey = message?.Key,
                    MessageArgs = args
                }).ToList();
            if (notifications.Count == 0) return;
            await _notificationRepo.InsertManyAsync(notifications, autoSave: true);

            var uow = _unitOfWorkManager.Current;
            if (uow == null) await PushAsync(notifications);
            else uow.OnCompleted(() => PushAsync(notifications));
        }

        private async Task PushAsync(List<Notification> notifications)
        {
            foreach (var notification in notifications)
            {
                try
                {
                    await _realtime.SendAsync(notification.UserId, NotificationAppService.ToDto(notification));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không đẩy được thông báo real-time {NotificationId} tới {UserId}.",
                        notification.Id, notification.UserId);
                }
            }
        }

        public async Task SendToAdminsAsync(NotificationType type, string title, string content,
            string? linkType = null, Guid? linkId = null, NotificationMessage? message = null)
        {
            var roles = await _roleRepo.GetQueryableAsync();
            var users = await _userRepo.GetQueryableAsync();
            var admins = await _userRepo.AsyncExecuter.ToListAsync(
                from user in users
                join role in roles on user.RoleId equals role.Id
                where role.Name == "Admin" && user.IsActive
                select (Guid?)user.Id);
            await SendAsync(admins, type, title, content, linkType, linkId, message);
        }

        public async Task<Guid?> ShipperUserIdAsync(Guid? shipperId) =>
            shipperId.HasValue ? (await _shipperRepo.FindAsync(shipperId.Value))?.UserId : null;

        private static string Money(decimal amount) => $"{amount:N0}đ";

        private static string Invariant(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Template key + the order fields a client needs to render the message in its own language.</summary>
        private static NotificationMessage Msg(string key, WashOrder o, params (string Name, string? Value)[] extra)
        {
            var args = new Dictionary<string, string>
            {
                ["orderCode"] = o.OrderCode,
                ["lockerName"] = o.LockerName ?? "",
                ["boxNumber"] = o.BoxNumber ?? "",
                ["amount"] = Invariant(o.FinalAmount)
            };
            if (o.PickupDeadline is { } pickup) args["pickupDeadline"] = pickup.ToString("O", CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(o.CancellationReason)) args["reason"] = o.CancellationReason;
            if (!string.IsNullOrEmpty(o.StorageLocation)) args["storageLocation"] = o.StorageLocation;
            if (!string.IsNullOrEmpty(o.StorageReference)) args["storageReference"] = o.StorageReference;
            foreach (var (name, value) in extra)
                if (value != null) args[name] = value;
            return new NotificationMessage(key, args);
        }

        public static NotificationMessage Message(string key, params (string Name, string? Value)[] args) =>
            new(key, args.Where(x => x.Value != null).ToDictionary(x => x.Name, x => x.Value!));

        // ---- Customer order lifecycle ----

        public async Task OrderCreatedAsync(WashOrder o)
        {
            await SendAsync([o.CustomerId], NotificationType.OrderCreated, "Đơn hàng đã được tạo",
                $"Đơn hàng #{o.OrderCode} đã được tạo. Vui lòng đến {o.LockerName}, ô số {o.BoxNumber} để gửi đồ trong 30 phút.",
                "order", o.Id, Msg("orderCreated.customer", o));
            await SendAsync([o.MerchantId], NotificationType.NewOrderForMerchant, "Đơn hàng mới",
                $"Bạn có đơn hàng mới #{o.OrderCode} cần xử lý.", "order", o.Id, Msg("orderCreated.merchant", o));
        }

        public async Task DepositedAsync(WashOrder o)
        {
            await SendAsync([o.CustomerId], NotificationType.Deposited, "Gửi đồ thành công",
                $"Bạn đã gửi đồ thành công cho đơn #{o.OrderCode}!", "order", o.Id, Msg("deposited.customer", o));
            await SendAsync([o.MerchantId], NotificationType.CustomerDeposited, "Khách hàng đã gửi đồ",
                $"Khách hàng đã gửi đồ #{o.OrderCode} vào {o.LockerName}, ô số {o.BoxNumber}. Vui lòng phân công Shipper.", "order", o.Id, Msg("deposited.merchant", o));
            // Orders get their Merchant from the Admin after deposit (UC 3.3).
            await SendToAdminsAsync(NotificationType.CustomerDeposited, "Đơn mới chờ phân công",
                o.MerchantId == null
                    ? $"Khách hàng đã gửi đồ #{o.OrderCode} vào {o.LockerName}, ô số {o.BoxNumber}. Vui lòng phân công Merchant."
                    : $"Khách hàng đã gửi đồ #{o.OrderCode} vào {o.LockerName}, ô số {o.BoxNumber}.", "order", o.Id, Msg(o.MerchantId == null ? "deposited.admin.assignMerchant" : "deposited.admin", o));
        }

        public async Task ShipperAssignedAsync(WashOrder o, DeliveryTaskType task)
        {
            var content = task == DeliveryTaskType.Collect
                ? $"Bạn có đơn mới #{o.OrderCode} cần lấy đồ tại {o.LockerName}, ô số {o.BoxNumber}."
                : $"Bạn có đơn #{o.OrderCode} cần trả đồ về locker.";
            await SendAsync([await ShipperUserIdAsync(o.ShipperId)], NotificationType.NewTaskForShipper,
                "Đơn mới được phân công", content, "order", o.Id, Msg(task == DeliveryTaskType.Collect ? "shipperAssigned.collect.shipper" : "shipperAssigned.return.shipper", o));
        }

        public async Task CollectedAsync(WashOrder o)
        {
            await SendAsync([await ShipperUserIdAsync(o.ShipperId)], NotificationType.ShipperCollected,
                "Lấy đồ thành công", $"Bạn đã lấy đồ đơn #{o.OrderCode} thành công!", "order", o.Id, Msg("collected.shipper", o));
            await SendAsync([o.CustomerId], NotificationType.OrderAccepted, "Đơn hàng đã được tiếp nhận",
                $"Đơn hàng #{o.OrderCode} của bạn đã được tiếp nhận.", "order", o.Id, Msg("collected.customer", o));
        }

        public Task ProcessingAsync(WashOrder o)
        {
            if (o.Status == WashOrderStatus.AwaitingPayment)
                return SendAsync([o.CustomerId], NotificationType.AwaitingPayment, "Nhắc nhở thanh toán",
                    $"Đơn hàng #{o.OrderCode} đã giặt xong. Số tiền cần thanh toán: {Money(o.FinalAmount)}. Vui lòng thanh toán để nhận đồ.",
                    "order", o.Id, Msg("processing.awaitingPayment.customer", o));
            var step = o.Status switch
            {
                WashOrderStatus.Washing => "giặt",
                WashOrderStatus.Drying => "sấy",
                WashOrderStatus.Ironing => "ủi",
                WashOrderStatus.Folding => "gấp và đóng gói",
                _ => null
            };
            return step == null ? Task.CompletedTask
                : SendAsync([o.CustomerId], NotificationType.OrderProcessing, "Cập nhật trạng thái đơn",
                    $"Đơn hàng #{o.OrderCode} đang được {step}.", "order", o.Id, Msg("processing.step.customer", o, ("step", o.Status.ToString())));
        }

        public async Task PaymentSucceededAsync(WashOrder o)
        {
            if (o.Status == WashOrderStatus.MovedToStorage)
            {
                // UC 1.2.4 7b: paid after the move to storage; the customer collects at the storage.
                await SendAsync([o.CustomerId], NotificationType.PaymentSucceeded, "Thanh toán thành công",
                    $"Thanh toán thành công! Đồ của đơn #{o.OrderCode} đang được lưu tại {o.StorageLocation} (mã {o.StorageReference}). Vui lòng thanh toán phí lưu kho (nếu có) và đến nhận đồ.", "order", o.Id, Msg("paymentSucceeded.storage.customer", o));
                await SendAsync([o.MerchantId], NotificationType.PaymentSucceeded, "Khách đã thanh toán",
                    $"Khách đã thanh toán đơn #{o.OrderCode} đang lưu kho (mã {o.StorageReference}). Khách sẽ đến nhận đồ tại kho.", "order", o.Id, Msg("paymentSucceeded.storage.merchant", o));
                return;
            }
            await SendAsync([o.CustomerId], NotificationType.PaymentSucceeded, "Thanh toán thành công",
                $"Thanh toán thành công! Đơn hàng #{o.OrderCode} sẽ được trả vào locker trong thời gian sớm nhất.", "order", o.Id, Msg("paymentSucceeded.customer", o));
            // UC 1.2.4 step 9: the Merchant can now send the clean laundry back.
            await SendAsync([o.MerchantId], NotificationType.PaymentSucceeded, "Khách đã thanh toán",
                $"Khách đã thanh toán đơn #{o.OrderCode}. Vui lòng phân công Shipper trả đồ về locker.", "order", o.Id, Msg("paymentSucceeded.merchant", o));
        }

        // UC 1.2.4 7a.
        public Task PaymentFailedAsync(WashOrder o) =>
            SendAsync([o.CustomerId], NotificationType.PaymentFailed, "Thanh toán thất bại",
                $"Thanh toán thất bại. Vui lòng thử lại. (Đơn #{o.OrderCode})", "order", o.Id, Msg("paymentFailed.customer", o));

        public async Task ReturnedToLockerAsync(WashOrder o)
        {
            var locker = o.ReturnLockerId.HasValue ? await _lockerRepo.FindAsync(o.ReturnLockerId.Value) : null;
            var place = $"{locker?.Name ?? o.LockerName}, ô số {o.ReturnBoxNumber}";
            await SendAsync([await ShipperUserIdAsync(o.ShipperId)], NotificationType.ShipperReturned,
                "Trả đồ thành công", $"Bạn đã trả đồ đơn #{o.OrderCode} thành công!", "order", o.Id, Msg("returnedToLocker.shipper", o));
            await SendAsync([o.MerchantId], NotificationType.ShipperReturned, "Shipper đã trả đồ",
                $"Shipper đã trả đồ đơn #{o.OrderCode} vào {place}.", "order", o.Id, Msg("returnedToLocker.merchant", o, ("place", place)));
            var content = o.Status == WashOrderStatus.Cancelled
                ? $"Đồ của đơn đã hủy #{o.OrderCode} đã được đưa về {place}. Vui lòng thanh toán phí hủy (nếu có) và đến nhận lại."
                : $"Đồ của bạn đã sẵn sàng tại {place}. Vui lòng đến lấy trước {o.PickupDeadline?.AddHours(7):HH:mm dd/MM/yyyy}.";
            await SendAsync([o.CustomerId], NotificationType.ReadyForPickup, "Đồ đã sẵn sàng", content, "order", o.Id, Msg(o.Status == WashOrderStatus.Cancelled ? "returnedToLocker.cancelled.customer" : "returnedToLocker.customer", o, ("place", place)));
        }

        public async Task PickedUpAsync(WashOrder o)
        {
            await SendAsync([o.CustomerId], NotificationType.PickedUp, "Lấy đồ thành công",
                $"Bạn đã lấy đồ đơn #{o.OrderCode} thành công! Cảm ơn bạn đã sử dụng dịch vụ.", "order", o.Id, Msg("pickedUp.customer", o));
            await SendToAdminsAsync(NotificationType.CustomerPickedUp, "Khách hàng đã lấy đồ",
                $"Khách hàng đã lấy đồ #{o.OrderCode}. Hoàn tất.", "order", o.Id, Msg("pickedUp.admin", o));
        }

        public async Task CancelledAsync(WashOrder o, Guid? voidedTaskShipperId)
        {
            await SendAsync([o.CustomerId], NotificationType.OrderCancelled, "Đơn hàng đã bị hủy",
                $"Đơn hàng #{o.OrderCode} đã bị hủy. Lý do: {o.CancellationReason}.", "order", o.Id, Msg("cancelled.customer", o));
            await SendAsync([o.MerchantId], NotificationType.OrderCancelled, "Đơn hàng đã bị hủy",
                $"Đơn hàng #{o.OrderCode} đã bị khách hủy.", "order", o.Id, Msg("cancelled.merchant", o));
            await SendAsync([await ShipperUserIdAsync(voidedTaskShipperId)], NotificationType.OrderCancelled,
                "Nhiệm vụ đã bị hủy", $"Đơn hàng #{o.OrderCode} đã bị khách hủy; nhiệm vụ lấy đồ của bạn không còn hiệu lực.",
                "order", o.Id, Msg("cancelled.shipper", o));
        }

        public async Task CancelledGoodsReturnedAsync(WashOrder o)
        {
            await SendAsync([o.CustomerId], NotificationType.PickedUp, "Đã nhận lại đồ",
                $"Bạn đã nhận lại đồ của đơn đã hủy #{o.OrderCode}.", "order", o.Id, Msg("cancelledGoodsReturned.customer", o));
            await SendAsync([o.MerchantId], NotificationType.PickedUp, "Khách đã nhận lại đồ",
                $"Khách hàng đã nhận lại đồ của đơn đã hủy #{o.OrderCode}.", "order", o.Id, Msg("cancelledGoodsReturned.merchant", o));
        }

        // UC 1.2.4 7b.
        public async Task UnpaidMovedToStorageAsync(WashOrder o)
        {
            await SendAsync([o.CustomerId], NotificationType.MovedToStorage, "Thông báo chuyển kho",
                $"Đơn #{o.OrderCode} đã quá hạn thanh toán 7 ngày nên đồ được chuyển sang lưu kho tại {o.StorageLocation}. Vui lòng thanh toán tiền giặt và phí lưu kho để nhận đồ.", "order", o.Id, Msg("unpaidMovedToStorage.customer", o));
            await SendAsync([o.MerchantId], NotificationType.MovedToStorage, "Đơn chuyển lưu kho",
                $"Đơn #{o.OrderCode} quá hạn thanh toán đã chuyển sang lưu kho (mã {o.StorageReference}). Vui lòng giữ đồ đến khi khách thanh toán và đến nhận.", "order", o.Id, Msg("unpaidMovedToStorage.merchant", o));
        }

        public Task MovedToStorageAsync(WashOrder o) =>
            SendAsync([o.CustomerId], NotificationType.MovedToStorage, "Thông báo chuyển kho",
                $"Đồ của đơn #{o.OrderCode} đã được chuyển về kho lưu trữ. Vui lòng liên hệ để nhận.", "order", o.Id, Msg("movedToStorage.customer", o));

        // ---- Feedback & finance ----

        public Task RefundRequestedAsync(WashOrder o, decimal amount) =>
            SendToAdminsAsync(NotificationType.RefundRequested, "Yêu cầu hoàn tiền",
                $"Yêu cầu hoàn tiền {Money(amount)} cho đơn #{o.OrderCode} cần phê duyệt.", "order", o.Id, Msg("refundRequested.admin", o, ("refundAmount", Invariant(amount))));

        public Task IncidentCreatedAsync(Incident i) =>
            SendToAdminsAsync(NotificationType.NewIncident, "Khiếu nại mới",
                $"Có khiếu nại mới từ khách {i.CustomerName} về đơn #{i.OrderCode}.", "incident", i.Id,
                Message("incidentCreated.admin", ("orderCode", i.OrderCode), ("customerName", i.CustomerName)));

        public async Task IncidentProcessedAsync(Incident i)
        {
            var result = i.Status == IncidentStatus.Rejected
                ? "đã bị từ chối"
                : i.CompensationAmount > 0 ? $"đã được xử lý, bồi thường {Money(i.CompensationAmount)}" : "đã được xử lý";
            var outcome = i.Status == IncidentStatus.Rejected ? "rejected" : i.CompensationAmount > 0 ? "compensated" : "resolved";
            var message = Message($"incidentProcessed.{outcome}", ("orderCode", i.OrderCode),
                ("compensationAmount", Invariant(i.CompensationAmount)));
            await SendAsync([i.CustomerId], NotificationType.IncidentProcessed, "Khiếu nại được xử lý",
                $"Khiếu nại của bạn về đơn #{i.OrderCode} {result}.", "incident", i.Id, message);
            await SendAsync([i.MerchantId], NotificationType.IncidentProcessed, "Kết quả khiếu nại",
                $"Khiếu nại về đơn #{i.OrderCode} {result}.", "order", i.OrderId, message);
        }

        public Task RatingCreatedAsync(OrderRating r) =>
            SendToAdminsAsync(NotificationType.NewRating, "Đánh giá mới",
                $"Có đánh giá mới từ khách {r.CustomerName} ({r.OverallStars}⭐) cho đơn #{r.OrderCode}.", "rating", r.Id,
                Message("ratingCreated.admin", ("orderCode", r.OrderCode), ("customerName", r.CustomerName), ("stars", r.OverallStars.ToString(CultureInfo.InvariantCulture))));
    }
}
