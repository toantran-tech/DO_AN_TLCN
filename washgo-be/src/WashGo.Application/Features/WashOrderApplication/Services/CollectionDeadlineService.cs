using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Scheduling;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Single source of the collect deadline: CollectSlaHoursSnapshot hours of the responsible Merchant's opening shifts,
    /// counted from DepositedAt (never from assignment). Computed once and stored with the applied schedule; later schedule,
    /// SLA, Shipper or Merchant changes do not move a computed deadline.
    /// </summary>
    public class CollectionDeadlineService : ITransientDependency
    {
        private readonly IRepository<Merchant, Guid> _merchants;
        private readonly IRepository<WashOrder, Guid> _orders;
        private readonly SystemSettingProvider _settings;
        private readonly NotificationPublisher _notifications;
        private readonly IAsyncQueryableExecuter _executer;

        public CollectionDeadlineService(IRepository<Merchant, Guid> merchants, IRepository<WashOrder, Guid> orders,
            SystemSettingProvider settings, NotificationPublisher notifications, IAsyncQueryableExecuter executer)
        {
            _merchants = merchants;
            _orders = orders;
            _settings = settings;
            _notifications = notifications;
            _executer = executer;
        }

        /// <summary>Fills the deadline fields when the order is deposited and has no promised deadline yet. Caller saves the order.</summary>
        public async Task CaptureAsync(WashOrder order)
        {
            if (order.DepositedAt == null || order.CollectionDeadlineAt != null ||
                order.CollectionDeadlineState is CollectionDeadlineStates.Scheduled or CollectionDeadlineStates.Legacy)
                return;
            order.CollectSlaHoursSnapshot ??= (await _settings.ForOrderAsync(order)).MerchantCollectSlaHours;
            if (order.MerchantId == null)
            {
                order.CollectionDeadlineState = CollectionDeadlineStates.AwaitingMerchant;
                return;
            }
            var merchant = await _merchants.FindAsync(order.MerchantId.Value);
            var schedule = OpeningScheduleRules.Deserialize(merchant?.OpeningSchedule);
            order.CollectionDeadlineComputedAt = DateTime.UtcNow;
            if (schedule == null)
            {
                var first = order.CollectionDeadlineState != CollectionDeadlineStates.ScheduleMissing;
                order.CollectionDeadlineState = CollectionDeadlineStates.ScheduleMissing;
                // No fake hours and no silent 24/7: the order waits for the Admin to add the Merchant's opening hours.
                if (first)
                    await _notifications.SendToAdminsAsync(NotificationType.SlaWarning, "Merchant thiếu giờ mở cửa",
                        $"⚠️ Đơn #{order.OrderCode} của {merchant?.BusinessName ?? "Merchant"} chưa có hạn thu gom vì Merchant chưa có giờ mở cửa hợp lệ. Hãy bổ sung giờ mở cửa; hạn sẽ được tính từ lúc khách gửi đồ.",
                        "order", order.Id);
                return;
            }
            var window = OpeningScheduleRules.Compute(order.DepositedAt.Value, order.CollectSlaHoursSnapshot.Value, schedule);
            order.CollectionStartsAt = window.StartsAtUtc;
            order.CollectionDeadlineAt = window.DeadlineUtc;
            order.CollectionDeadlineState = window.DeadlineUtc.HasValue ? CollectionDeadlineStates.Scheduled : CollectionDeadlineStates.NoUpcomingShift;
            order.CollectionScheduleVersion = OpeningScheduleRules.Version(schedule);
            order.CollectionScheduleSnapshot = OpeningScheduleRules.Serialize(schedule);
        }

        /// <summary>After a Merchant gets a valid schedule: orders still waiting for one get their deadline (from DepositedAt).</summary>
        public async Task<int> RecomputeWaitingAsync(Guid merchantId)
        {
            var waiting = await _executer.ToListAsync((await _orders.GetQueryableAsync()).Where(o => !o.IsDeleted &&
                o.MerchantId == merchantId && o.DepositedAt != null && o.CollectionDeadlineAt == null &&
                (o.CollectionDeadlineState == CollectionDeadlineStates.ScheduleMissing || o.CollectionDeadlineState == CollectionDeadlineStates.NoUpcomingShift) &&
                WashOrderStatusFlow.AtDepositLocker.Contains(o.Status)));
            foreach (var order in waiting)
            {
                await CaptureAsync(order);
                await _orders.UpdateAsync(order, autoSave: true);
            }
            return waiting.Count;
        }
    }
}
