using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Shared.Validation;

namespace WashGo.EntityFrameworkCore.EntityFrameworkCore;

public partial class WashGoDbContext
{
    private async Task PrepareBusinessChangesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        await GuardCatalogAsync(ct);
        await IssueStorageReferencesAsync(ct);
        await PrepareOrderEventsAsync(ct);
        foreach (var entry in ChangeTracker.Entries<Notification>().Where(x => x.State == EntityState.Added).ToArray())
        {
            entry.Entity.AssignIdIfEmptyForBulkInsert();
            if (!NotificationDeliveries.Local.Any(x => x.NotificationId == entry.Entity.Id))
                NotificationDeliveries.Add(new NotificationDelivery(Guid.NewGuid()) { NotificationId = entry.Entity.Id, UserId = entry.Entity.UserId });
        }
    }

    private async Task CatalogLockAsync(CancellationToken ct) =>
        await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('service-catalog'))", ct);

    private async Task GuardCatalogAsync(CancellationToken ct)
    {
        var services = ChangeTracker.Entries<WashService>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray();
        var items = ChangeTracker.Entries<WashOrderItem>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var proposals = ChangeTracker.Entries<ServiceChangeRequest>().Where(x => x.State == EntityState.Added && x.Entity.Status == ServiceApprovalStatus.Pending).ToArray();
        if (services.Length + items.Length + proposals.Length == 0) return;
        await CatalogLockAsync(ct);
        foreach (var entry in services)
        {
            var service = entry.Entity;
            if (entry.State == EntityState.Added && service.CategoryId == Guid.Empty)
                service.CategoryId = ServiceCategoryRules.LegacyId((int)service.ServiceType);
            var original = entry.State == EntityState.Added ? null : await WashServices.AsNoTracking().Where(x => x.Id == service.Id)
                .Select(x => new { x.CategoryId, x.IsActive, x.ApprovalStatus }).FirstOrDefaultAsync(ct);
            // Editing metadata on a locked category is allowed; attaching or reactivating is not.
            if (!service.IsDeleted && (entry.State == EntityState.Added || original == null || original.CategoryId != service.CategoryId ||
                !original.IsActive && service.IsActive || original.ApprovalStatus != ServiceApprovalStatus.Approved && service.ApprovalStatus == ServiceApprovalStatus.Approved))
                await RequireCategoryAsync(service.CategoryId, ct);
        }
        foreach (var proposal in proposals)
        {
            using var data = JsonDocument.Parse(proposal.Entity.ProposedData);
            var root = data.RootElement;
            var id = root.TryGetProperty("categoryId", out var category) && category.ValueKind == JsonValueKind.String && category.TryGetGuid(out var explicitId)
                ? explicitId : root.TryGetProperty("serviceType", out var type) && type.TryGetInt32(out var value) && value is >= 1 and <= 5
                    ? ServiceCategoryRules.LegacyId(value) : Guid.Empty;
            await RequireCategoryAsync(id, ct);
        }
        foreach (var item in items)
        {
            var service = await WashServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.ServiceId && !x.IsDeleted, ct);
            if (service == null || service.UnitPrice <= 0 || !service.IsActive || service.ApprovalStatus != ServiceApprovalStatus.Approved)
                throw new CatalogRuleException("ServiceUnavailable", "Dịch vụ không còn khả dụng; tải lại giỏ hàng.");
            var category = await RequireCategoryAsync(service.CategoryId, ct);
            item.CategoryIdSnapshot = category.Id; item.CategoryNameSnapshot = category.Name; item.CategoryCodeSnapshot = category.Code;
            item.ProcessingTypeSnapshot = service.ServiceType;
        }
    }

    private async Task<ServiceCategoryEntity> RequireCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await ServiceCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (category == null || category.IsDeleted) throw new CatalogRuleException("CategoryDeleted", "Danh mục không tồn tại hoặc đã xóa.");
        if (!category.IsActive) throw new CatalogRuleException("CategoryInactive", "Danh mục đang khóa; không thể chọn hoặc mở bán dịch vụ.");
        return category;
    }

    private async Task IssueStorageReferencesAsync(CancellationToken ct)
    {
        foreach (var entry in ChangeTracker.Entries<OrderStorageCycle>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            var cycle = entry.Entity;
            if (cycle.State != StorageCycleState.Stored || cycle.StorageReference != null) continue;
            var previousState = entry.State == EntityState.Added ? null : await StorageCycles.AsNoTracking()
                .Where(x => x.Id == cycle.Id).Select(x => (StorageCycleState?)x.State).FirstOrDefaultAsync(ct);
            var issued = DateTime.UtcNow;
            string? code = await StorageReferenceClaims.Where(x => x.Id == cycle.Id).Select(x => x.Code).FirstOrDefaultAsync(ct);
            for (var attempt = 0; code == null && attempt < 20; attempt++)
            {
                var candidate = StorageReferenceRules.Format(issued, RandomNumberGenerator.GetInt32(0, 10000));
                // ON CONFLICT keeps PostgreSQL usable: collision never aborts the business transaction.
                var inserted = await Database.ExecuteSqlInterpolatedAsync($"INSERT INTO washgo.storage_reference_claims(id,code,issued_at) VALUES ({cycle.Id},{candidate},{issued}) ON CONFLICT DO NOTHING", ct);
                code = inserted == 1 ? candidate : await StorageReferenceClaims.Where(x => x.Id == cycle.Id).Select(x => x.Code).FirstOrDefaultAsync(ct);
            }
            if (code == null) throw new InvalidOperationException("Storage reference allocation exhausted; retry the operation.");
            cycle.StorageReference = code; cycle.ReferenceIssuedAt = issued;
            cycle.ReferenceBasis = previousState != StorageCycleState.Stored ? "StoredConfirmation" : "IssuedForVerifiedExistingCycle";
            var order = WashOrders.Local.FirstOrDefault(x => x.Id == cycle.OrderId) ?? await WashOrders.FirstOrDefaultAsync(x => x.Id == cycle.OrderId, ct);
            if (order != null && order.Status == WashOrderStatus.MovedToStorage && (order.ActiveStorageCycleId == null || order.ActiveStorageCycleId == cycle.Id))
            { order.StorageLocation = cycle.ShopAddress; order.StorageReference = code; }
        }
    }

    private async Task PrepareOrderEventsAsync(CancellationToken ct)
    {
        var entries = ChangeTracker.Entries<WashOrderTimeline>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        if (entries.Length == 0) return;
        var admins = await (from u in Users.AsNoTracking() join r in Roles.AsNoTracking() on u.RoleId equals r.Id
                            where u.IsActive && r.Name == "Admin" select u.Id).ToListAsync(ct);
        foreach (var group in entries.GroupBy(x => x.WashOrderId).OrderBy(x => x.Key))
        {
            var key = $"timeline:{group.Key}";
            await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
            var sequence = await WashOrderTimelines.Where(x => x.WashOrderId == group.Key).MaxAsync(x => x.Sequence, ct) ?? 0;
            foreach (var row in group)
            {
                row.Sequence = ++sequence;
                if (row.EventType == "Legacy") row.EventType = row.FromStatus == row.Status ? "OrderNote" : row.Status.ToString();
                row.Basis = "Captured";
                var order = WashOrders.Local.FirstOrDefault(x => x.Id == row.WashOrderId) ?? await WashOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.WashOrderId, ct);
                var cycle = row.StorageCycleId.HasValue ? StorageCycles.Local.FirstOrDefault(x => x.Id == row.StorageCycleId) ?? await StorageCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.StorageCycleId, ct) : null;
                foreach (var admin in admins)
                {
                    var notification = new Notification { EventId = row.Id, UserId = admin, Type = row.EventType switch
                        { "Deposited" => NotificationType.CustomerDeposited, "Completed" => NotificationType.CustomerPickedUp,
                          "LaundryPaid" => NotificationType.PaymentSucceeded, "CancellationFeePaid" or "StorageFeePaid" => NotificationType.PaymentSucceeded,
                          "Cancelled" => NotificationType.OrderCancelled, "OrderCreated" => NotificationType.OrderCreated, _ => NotificationType.OrderEvent },
                        Title = OrderEventLabels.Title(row.EventType, row.Status), Content = $"Đơn #{order?.OrderCode}: {OrderEventLabels.Title(row.EventType, row.Status)}." + (cycle?.StorageReference == null ? "" : $" Mã lưu giữ {cycle.StorageReference}."), LinkType = "order", LinkId = row.WashOrderId,
                        MessageKey = $"orderEvent.{row.EventType}", MessageArgs = JsonSerializer.Serialize(new { orderCode = order?.OrderCode, eventId = row.Id.ToString(), eventType = row.EventType,
                            storageCycleId = row.StorageCycleId?.ToString(), taskId = row.TaskId?.ToString(), storageReference = cycle?.StorageReference, storageLocation = cycle?.ShopAddress, amount = row.Amount?.ToString(CultureInfo.InvariantCulture) }) };
                    Notifications.Add(notification);
                }
            }
        }
    }
}
