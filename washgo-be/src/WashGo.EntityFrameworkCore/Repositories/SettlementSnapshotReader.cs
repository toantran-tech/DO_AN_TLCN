using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Finance;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class SettlementSnapshotReader(IDbContextProvider<WashGoDbContext> contexts) : ISettlementSnapshotReader, ITransientDependency
{
    public async Task<(List<string> Lines, int Total)?> ReadAsync(Guid id, Guid? merchantId, int page, int take)
    {
        var db = await contexts.GetDbContextAsync();
        var counts = await db.Database.SqlQuery<int>($"""
            SELECT jsonb_array_length(s.breakdown_snapshot_json->'Lines') AS "Value"
            FROM washgo.merchant_settlements s
            WHERE s.id={id} AND NOT s.is_deleted AND s.breakdown_snapshot_json IS NOT NULL
              AND ({merchantId}::uuid IS NULL OR s.merchant_id={merchantId})
            """).ToListAsync();
        if (counts.Count == 0) return null;
        var offset = ((long)page - 1) * take;
        var lines = await db.Database.SqlQuery<string>($"""
            SELECT item.value::text AS "Value"
            FROM washgo.merchant_settlements s
            CROSS JOIN LATERAL jsonb_array_elements(s.breakdown_snapshot_json->'Lines') WITH ORDINALITY item(value, ordinal)
            WHERE s.id={id} AND NOT s.is_deleted
              AND ({merchantId}::uuid IS NULL OR s.merchant_id={merchantId})
            ORDER BY item.ordinal OFFSET {offset} LIMIT {take}
            """).ToListAsync();
        return (lines, counts[0]);
    }
}
