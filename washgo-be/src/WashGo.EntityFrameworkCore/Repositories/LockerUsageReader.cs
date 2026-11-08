using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class LockerUsageReader(IDbContextProvider<WashGoDbContext> contexts) : ILockerUsageReader, ITransientDependency
{
    public async Task<(List<LockerUsageRecord> Items, int Total)> ReadAsync(Guid lockerId, Guid? merchantId, Guid? boxId, DateTime? from, DateTime? to, int page, int take)
    {
        var db = await contexts.GetDbContextAsync();
        // UNION ALL creates two distinct uses of the same order. Date/scope/count/paging stay in PostgreSQL.
        var q = db.Database.SqlQuery<LockerUsageRecord>($"""
            SELECT o.id AS order_id, o.order_code, o.locker_slot_id AS box_id, o.slot_number AS box_number, 1 AS usage_type,
              COALESCE((SELECT to_timestamp(min(t.created_at)) FROM washgo.order_status_history t WHERE t.order_id=o.id AND t.to_status=2),o.deposited_at) AS put_in_at,
              CASE WHEN o.status=99 AND o.cancellation_from_status=2 THEN o.cancelled_goods_returned_at
                   ELSE (SELECT to_timestamp(min(t.created_at)) FROM washgo.order_status_history t WHERE t.order_id=o.id AND t.to_status=3) END AS taken_out_at,
              o.status AS order_status
            FROM washgo.orders o WHERE o.is_deleted=false AND o.locker_id={lockerId} AND o.deposited_at IS NOT NULL
              AND ({merchantId}::uuid IS NULL OR o.merchant_id={merchantId})
            UNION ALL
            SELECT o.id AS order_id, o.order_code, o.return_box_id AS box_id, o.return_box_number AS box_number, 2 AS usage_type,
              (SELECT to_timestamp(min(t.created_at)) FROM washgo.order_status_history t WHERE t.order_id=o.id AND t.to_status=10) AS put_in_at,
              CASE WHEN o.status=99 THEN o.cancelled_goods_returned_at
                   ELSE COALESCE((SELECT to_timestamp(min(t.created_at)) FROM washgo.order_status_history t WHERE t.order_id=o.id AND t.to_status=11),o.moved_to_storage_at) END AS taken_out_at,
              o.status AS order_status
            FROM washgo.orders o WHERE o.is_deleted=false AND o.return_locker_id={lockerId} AND o.return_box_number IS NOT NULL
              AND NOT (o.status=99 AND o.cancellation_from_status=2)
              AND (o.status=99 OR EXISTS(SELECT 1 FROM washgo.order_status_history t WHERE t.order_id=o.id AND t.to_status=10))
              AND ({merchantId}::uuid IS NULL OR o.merchant_id={merchantId})
            """);
        if (boxId.HasValue) q = q.Where(x => x.BoxId == boxId);
        if (from.HasValue) q = q.Where(x => x.PutInAt >= from);
        if (to.HasValue) q = q.Where(x => x.PutInAt <= to);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.PutInAt).ThenBy(x => x.OrderCode).ThenBy(x => x.UsageType).ThenBy(x => x.OrderId).Skip((page - 1) * take).Take(take).ToListAsync();
        return (items, total);
    }
}
