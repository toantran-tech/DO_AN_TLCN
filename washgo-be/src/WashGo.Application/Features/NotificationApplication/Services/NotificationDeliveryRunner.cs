using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder.Interfaces;

namespace WashGo.Application.Features.NotificationApplication.Services;

public class NotificationDeliveryRunner(IRepository<NotificationDelivery, Guid> deliveries, IRepository<Notification, Guid> inbox,
    IRepository<User, Guid> users, IRepository<Role, Guid> roles, IRepository<Shipper, Guid> shippers,
    IWashOrderRepository orders, IUnitOfWorkManager units, IRealtimeNotifier realtime, ILogger<NotificationDeliveryRunner> logger)
    : INotificationDeliveryRunner, ITransientDependency
{
    [UnitOfWork(IsDisabled = true)]
    public async Task DrainAsync()
    {
        List<Guid> ids;
        using (var scan = units.Begin(requiresNew: true, isTransactional: false))
        {
            var now = DateTime.UtcNow;
            ids = await deliveries.AsyncExecuter.ToListAsync((await deliveries.GetQueryableAsync())
                .Where(x => x.State == "Pending" && x.NextAttemptAt <= now || x.State == "Sending" && x.LeaseUntil < now)
                .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(40).Select(x => x.Id));
            await scan.CompleteAsync();
        }
        foreach (var id in ids)
        {
            Guid token; Guid userId; NotificationDto? payload;
            using (var claim = units.Begin(requiresNew: true, isTransactional: true))
            {
                await orders.AcquireLockAsync($"notification-delivery:{id}");
                var job = await deliveries.FindAsync(id); var now = DateTime.UtcNow;
                if (job == null || job.State is "Delivered" or "Suppressed" or "DeadLetter" || job.LeaseUntil > now || job.NextAttemptAt > now) continue;
                var row = await inbox.FindAsync(job.NotificationId);
                var user = row == null ? null : await users.FindAsync(row.UserId);
                var role = user == null ? null : (await roles.FindAsync(user.RoleId))?.Name;
                if (row == null || row.IsDeleted || row.IsRead || user?.IsActive != true || role == null || !await CanDeliverAsync(row, role))
                { job.State = "Suppressed"; await deliveries.UpdateAsync(job, true); await claim.CompleteAsync(); continue; }
                userId = row.UserId;
                payload = NotificationAppService.ToDto(row);
                if (role != "Admin" && row.LinkType == "order") payload = OrderNotificationPrivacy.Apply(payload, role == "Customer");
                token = Guid.NewGuid(); job.LeaseToken = token; job.LeaseUntil = now.AddMinutes(2); job.State = "Sending"; job.Attempts++;
                await deliveries.UpdateAsync(job, true); await claim.CompleteAsync();
            }
            string? error = null;
            try { await realtime.SendAsync(userId, payload).WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception) { error = "RealtimeDeliveryFailed"; }
            using var finish = units.Begin(requiresNew: true, isTransactional: true);
            await orders.AcquireLockAsync($"notification-delivery:{id}");
            var delivery = await deliveries.FindAsync(id);
            if (delivery == null || delivery.LeaseToken != token) continue;
            delivery.LeaseToken = null; delivery.LeaseUntil = null; delivery.LastError = error;
            delivery.State = error == null ? "Delivered" : delivery.Attempts >= 8 ? "DeadLetter" : "Pending";
            if (error == null) delivery.DeliveredAt = DateTime.UtcNow;
            else delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(1800, 15 * Math.Pow(2, delivery.Attempts)) + Random.Shared.Next(0, 10));
            if (delivery.State == "DeadLetter") logger.LogWarning("Notification outbox dead-letter {NotificationId} for {UserId}", delivery.NotificationId, delivery.UserId);
            await deliveries.UpdateAsync(delivery, true); await finish.CompleteAsync();
        }
    }
    private async Task<bool> CanDeliverAsync(Notification row, string role)
    {
        if (row.EventId.HasValue || row.MessageKey?.Contains(".admin", StringComparison.Ordinal) == true) return role == "Admin";
        if (row.LinkType != "order" || !row.LinkId.HasValue || role == "Admin") return true;
        var order = await orders.FindAsync(row.LinkId.Value);
        if (order == null || order.IsDeleted) return false;
        return role switch
        {
            "Customer" => order.CustomerId == row.UserId,
            "Merchant" => order.MerchantId == row.UserId,
            "Shipper" => await shippers.AnyAsync(x => x.Id == order.ShipperId && x.UserId == row.UserId && x.IsActive),
            _ => false
        };
    }
}
