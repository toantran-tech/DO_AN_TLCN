using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.NotificationApplication.Services
{
    /// <summary>
    /// Nhắc nhở theo thời gian (bảng thông báo real-time trong tài liệu tổng hợp chức năng).
    /// Các mốc lấy từ cấu hình SLA (UC 3.9); mỗi lời nhắc có khóa riêng trong <see cref="ReminderLog"/>
    /// nên quét lặp lại không gửi trùng. Đơn đã đổi trạng thái thì không còn khớp điều kiện và không bị nhắc.
    /// </summary>
    public class OrderReminderAppService : WashGoAppService, IOrderReminderAppService
    {
        /// <summary>Shipper được nhắc nếu sau ngần này phút kể từ khi phân công vẫn chưa lấy đồ.</summary>
        public const int ShipperCollectReminderMinutes = 30;
        private const int DepositReminderBeforeMinutes = 15;
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        private readonly IWashOrderRepository _orderRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IRepository<ReminderLog, Guid> _logRepo;
        private readonly NotificationPublisher _notifications;
        private readonly SystemSettingProvider _settings;
        private readonly Dictionary<string, int> _sent = [];

        public OrderReminderAppService(
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo,
            IRepository<ReminderLog, Guid> logRepo,
            NotificationPublisher notifications,
            SystemSettingProvider settings)
        {
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
            _logRepo = logRepo;
            _notifications = notifications;
            _settings = settings;
        }

        public async Task<BaseResponse<ReminderRunResultDto>> RunAsync()
        {
            var now = DateTime.UtcNow;
            var settings = await _settings.GetAsync();
            _sent.Clear();

            await RemindDepositAsync(now, settings);
            await RemindCollectionAsync(now, settings);
            await RemindReturnAsync(now, settings);
            await RemindPickupAsync(now, settings);

            return Success(new ReminderRunResultDto { RanAt = now, Sent = new(_sent), Total = _sent.Values.Sum() },
                "Đã quét nhắc nhở.");
        }

        /// <summary>Ghi khóa trước khi gửi; khóa đã tồn tại nghĩa là lời nhắc này đã gửi ở lần quét trước.</summary>
        private async Task<bool> ClaimAsync(string kind, string key, Guid? orderId, DateTime now)
        {
            if (await _logRepo.AnyAsync(x => x.Key == key))
                return false;
            await _logRepo.InsertAsync(new ReminderLog { Key = key, OrderId = orderId, SentAt = now }, autoSave: true);
            _sent[kind] = _sent.GetValueOrDefault(kind) + 1;
            return true;
        }

        private async Task<List<WashOrder>> OrdersAsync(Func<IQueryable<WashOrder>, IQueryable<WashOrder>> filter) =>
            await AsyncExecuter.ToListAsync(filter((await _orderRepo.GetQueryableAsync()).Where(x => !x.IsDeleted)));

        private static string Hours(double hours) => $"{Math.Max(1, (int)Math.Round(hours))} giờ";

        // Khách chưa gửi đồ: nhắc khi còn 15 phút trước hạn giữ ô (hoặc nửa thời hạn nếu thời hạn ngắn).
        private async Task RemindDepositAsync(DateTime now, SystemSetting s)
        {
            var window = TimeSpan.FromMinutes(s.DepositWindowMinutes);
            var before = TimeSpan.FromMinutes(Math.Min(DepositReminderBeforeMinutes, s.DepositWindowMinutes / 2.0));
            var nowTs = new DateTimeOffset(now).ToUnixTimeSeconds();
            var remindFrom = nowTs - (long)(window - before).TotalSeconds;
            var expiredBefore = nowTs - (long)window.TotalSeconds;
            foreach (var o in await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.Pending
                         && x.CreatedOn <= remindFrom && x.CreatedOn > expiredBefore)))
            {
                if (!await ClaimAsync("deposit", $"deposit:{o.Id}", o.Id, now)) continue;
                var left = DateTimeOffset.FromUnixTimeSeconds(o.CreatedOn).UtcDateTime + window - now;
                await _notifications.SendAsync([o.CustomerId], NotificationType.DepositReminder, "Nhắc nhở gửi đồ",
                    $"Bạn còn {Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))} phút để gửi đồ vào {o.LockerName}, ô số {o.BoxNumber}. " +
                    "Quá hạn sẽ không thể gửi đồ cho đơn này.", "order", o.Id);
            }
        }

        // Đồ đã gửi nhưng chưa được lấy về tiệm: nhắc Shipper sau 30 phút, cảnh báo Merchant/Admin khi trễ SLA lấy đồ.
        private async Task RemindCollectionAsync(DateTime now, SystemSetting s)
        {
            var shipperDue = now.AddMinutes(-ShipperCollectReminderMinutes);
            foreach (var o in await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.Deposited
                         && x.ShipperId != null && x.AssignedTaskType == DeliveryTaskType.Collect
                         && x.ShipperAssignedAt != null && x.ShipperAssignedAt <= shipperDue)))
            {
                // A reassignment gets its own reminder, so the key includes the assignment time.
                if (!await ClaimAsync("shipper-collect", $"shipper-collect:{o.Id}:{o.ShipperAssignedAt!.Value.Ticks}", o.Id, now)) continue;
                await _notifications.SendAsync([await _notifications.ShipperUserIdAsync(o.ShipperId)],
                    NotificationType.ShipperCollectReminder, "Nhắc nhở lấy đồ",
                    $"Đơn #{o.OrderCode} đã chờ {ShipperCollectReminderMinutes} phút. Vui lòng đến {o.LockerName}, ô số {o.BoxNumber} để lấy đồ.",
                    "order", o.Id);
            }

            var slaDue = now.AddHours(-s.MerchantCollectSlaHours);
            foreach (var o in await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.Deposited
                         && x.DepositedAt != null && x.DepositedAt <= slaDue)))
            {
                if (!await ClaimAsync("collect-late", $"collect-late:{o.Id}", o.Id, now)) continue;
                var late = $"⚠️ Đơn #{o.OrderCode} đã quá {s.MerchantCollectSlaHours} giờ kể từ khi khách gửi đồ nhưng chưa được lấy khỏi locker.";
                await _notifications.SendAsync([o.MerchantId], NotificationType.SlaWarning, "Cảnh báo trễ lấy đồ", late, "order", o.Id);
                await _notifications.SendToAdminsAsync(NotificationType.SlaWarning, "Cảnh báo trễ lấy đồ", late, "order", o.Id);
            }
        }

        // Đơn đã thanh toán nhưng chưa trả về locker: nhắc ở nửa SLA trả đồ, cảnh báo khi trễ SLA.
        private async Task RemindReturnAsync(DateTime now, SystemSetting s)
        {
            var sla = TimeSpan.FromHours(s.MerchantReturnSlaHours);
            var remindDue = now - sla / 2;
            foreach (var o in await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.Paid
                         && x.PaidAt != null && x.PaidAt <= remindDue)))
            {
                if (!await ClaimAsync("return-remind", $"return-remind:{o.Id}", o.Id, now)) continue;
                var left = o.PaidAt!.Value + sla - now;
                await _notifications.SendAsync([o.MerchantId], NotificationType.MerchantReturnReminder, "Nhắc nhở trả đồ",
                    $"Đơn #{o.OrderCode} đã giặt xong và thanh toán. Vui lòng trả vào locker trong {Hours(left.TotalHours)} tới.",
                    "order", o.Id);
                if (o.AssignedTaskType == DeliveryTaskType.Return)
                    await _notifications.SendAsync([await _notifications.ShipperUserIdAsync(o.ShipperId)],
                        NotificationType.ShipperReturnReminder, "Nhắc nhở trả đồ",
                        $"Đơn #{o.OrderCode} đã giặt xong. Vui lòng trả vào locker.", "order", o.Id);
            }

            var lateDue = now - sla;
            foreach (var o in await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.Paid
                         && x.PaidAt != null && x.PaidAt <= lateDue)))
            {
                if (!await ClaimAsync("return-late", $"return-late:{o.Id}", o.Id, now)) continue;
                await _notifications.SendAsync([o.MerchantId], NotificationType.SlaWarning, "Cảnh báo trễ trả đồ",
                    $"⚠️ Đơn #{o.OrderCode} đã quá hạn trả đồ {s.MerchantReturnSlaHours} giờ. Phí phạt sẽ được áp dụng.", "order", o.Id);
                await _notifications.SendToAdminsAsync(NotificationType.SlaWarning, "Merchant trễ trả đồ",
                    $"⚠️ Merchant {o.MerchantName ?? "(chưa rõ)"} trễ trả đồ đơn #{o.OrderCode} quá {s.MerchantReturnSlaHours} giờ.", "order", o.Id);
            }
        }

        // Đồ đang chờ khách lấy: nhắc ở nửa thời gian miễn phí, báo quá hạn và từng ngày phí phạt, báo cần chuyển kho.
        private async Task RemindPickupAsync(DateTime now, SystemSetting s)
        {
            var orders = await OrdersAsync(q => q.Where(x => x.Status == WashOrderStatus.ReadyToReturn && x.PickupDeadline != null));
            var lockerNames = new Dictionary<Guid, string>();
            var overdueCount = 0;
            foreach (var o in orders)
            {
                var deadline = o.PickupDeadline!.Value;
                var lockerId = o.ReturnLockerId ?? o.LockerId;
                if (!lockerNames.TryGetValue(lockerId, out var lockerName))
                    lockerNames[lockerId] = lockerName = (await _lockerRepo.FindAsync(lockerId))?.Name ?? o.LockerName;
                var place = $"{lockerName}, ô số {o.ReturnBoxNumber ?? o.BoxNumber}";

                if (now < deadline)
                {
                    if (now >= deadline.AddHours(-s.FreePickupHours / 2.0) &&
                        await ClaimAsync("pickup-remind", $"pickup-remind:{o.Id}", o.Id, now))
                        await _notifications.SendAsync([o.CustomerId], NotificationType.PickupReminder, "Nhắc nhở lấy đồ",
                            $"Đồ của bạn đang chờ tại {place}. Còn {Hours((deadline - now).TotalHours)} để lấy miễn phí.", "order", o.Id);
                    continue;
                }

                overdueCount++;
                var fee = OverdueFeeCalculator.Calculate(deadline, now, s);
                var chargedDays = (int)Math.Ceiling((now - deadline).TotalHours / 24d);
                // One message per started overdue day (the first one also announces the overdue status).
                if (chargedDays <= 3 && await ClaimAsync("overdue-fee", $"overdue-fee:{o.Id}:{chargedDays}", o.Id, now))
                {
                    var content = chargedDays == 1
                        ? $"Đồ của bạn tại {place} đã quá hạn lấy miễn phí. Phí phạt lưu locker hiện tại: {fee:N0}đ và tăng theo mỗi ngày quá hạn."
                        : $"Phí phạt lưu locker quá hạn: {fee:N0}đ đã được tính cho đơn #{o.OrderCode}. Vui lòng thanh toán và lấy đồ.";
                    await _notifications.SendAsync([o.CustomerId], NotificationType.OverdueFeeApplied,
                        chargedDays == 1 ? "Đồ đã quá hạn lấy" : "Thông báo phí phạt", content, "order", o.Id);
                }

                if (OverdueFeeCalculator.IsTransferToStorageDue(deadline, now) &&
                    await ClaimAsync("storage-due", $"storage-due:{o.Id}", o.Id, now))
                {
                    var due = $"Đơn #{o.OrderCode} tại {place} đã quá hạn lấy đồ hơn {SystemSetting.StorageTransferAfterHours} giờ. Cần chuyển đồ về kho lưu trữ.";
                    await _notifications.SendToAdminsAsync(NotificationType.StorageTransferDue, "Cần chuyển kho", due, "order", o.Id);
                    await _notifications.SendAsync([o.MerchantId], NotificationType.StorageTransferDue, "Cần chuyển kho", due, "order", o.Id);
                }
            }

            // Daily summary for Admin, keyed by the Vietnamese calendar day.
            var today = (now + VietnamOffset).ToString("yyyy-MM-dd");
            if (overdueCount > 0 && await ClaimAsync("overdue-summary", $"overdue-summary:{today}", null, now))
                await _notifications.SendToAdminsAsync(NotificationType.OverdueSummary, "Cảnh báo đơn tồn đọng",
                    $"Có {overdueCount} đơn hàng đã quá hạn lấy đồ. Vui lòng kiểm tra.");
        }
    }
}
