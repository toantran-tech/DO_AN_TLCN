using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.HttpApi.Hubs;

namespace WashGo.HttpApi.Host.Workers;

public sealed class OrderChangeDeliveryWorker(
    IServiceScopeFactory scopes,
    IHubContext<NotificationHub> hub,
    ILogger<OrderChangeDeliveryWorker> logger) : BackgroundService
{
    private const int BatchSize = 40;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested && await DeliverBatchAsync(stoppingToken) == BatchSize)
                {
                    // Keep draining while a full batch was available.
                }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(error, "Order change delivery failed. Unacknowledged events will be retried.");
            }
        }
    }

    private async Task<int> DeliverBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WashGoDbContext>();
        var lease = Guid.NewGuid();
        var events = await db.Database.SqlQuery<OrderChangeRow>($"""
            SELECT event_id AS "EventId", order_id AS "OrderId", revision AS "Revision",
                   change_type AS "ChangeType", occurred_at AS "OccurredAt", previous_merchant_id AS "PreviousMerchantId"
            FROM washgo.claim_order_changes({lease}, {BatchSize})
            """).ToListAsync(cancellationToken);

        foreach (var change in events)
        {
            await DeliverAsync(db, change, cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM washgo.order_change_outbox WHERE event_id = {change.EventId} AND lease_token = {lease}
                """, cancellationToken);
        }
        return events.Count;
    }

    private async Task DeliverAsync(WashGoDbContext db, OrderChangeRow change, CancellationToken cancellationToken)
    {
        var audience = await db.WashOrders.AsNoTracking()
            .Where(order => order.Id == change.OrderId && !order.IsDeleted)
            .Select(order => new { order.CustomerId, order.MerchantId })
            .SingleOrDefaultAsync(cancellationToken);

        var activeUsers = from user in db.Users.AsNoTracking()
                          join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
                          where user.IsActive && !user.IsDeleted && role.IsActive
                          select new { user.Id, Role = role.Name };

        if (audience != null)
        {
            var recipients = await activeUsers
                .Where(user => user.Role == "Admin" ||
                    user.Role == "Customer" && user.Id == audience.CustomerId ||
                    user.Role == "Merchant" && user.Id == audience.MerchantId)
                .Select(user => user.Id.ToString()).ToListAsync(cancellationToken);

            await hub.Clients.Users(recipients).SendAsync(NotificationHub.OrderChangedMethod, new
            {
                change.EventId, change.OrderId, change.Revision, change.ChangeType, change.OccurredAt
            }, cancellationToken);
        }

        if (change.PreviousMerchantId is { } previous && previous != audience?.MerchantId &&
            await activeUsers.AnyAsync(user => user.Id == previous && user.Role == "Merchant", cancellationToken))
        {
            await hub.Clients.User(previous.ToString()).SendAsync(NotificationHub.OrderScopeChangedMethod,
                new { change.EventId, change.OccurredAt }, cancellationToken);
        }
    }

    public sealed class OrderChangeRow
    {
        public Guid EventId { get; set; }
        public Guid OrderId { get; set; }
        public long Revision { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public Guid? PreviousMerchantId { get; set; }
    }
}
