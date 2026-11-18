using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Domain.Shared.Validation;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

/// <summary>
/// Cấp mã PPdddd qua bảng claim có khóa duy nhất: INSERT ... ON CONFLICT DO NOTHING nên va chạm không làm hỏng
/// transaction PostgreSQL; hai transaction cùng chọn một mã thì transaction sau chờ và chọn lại.
/// </summary>
public class BusinessCodeAllocator(IDbContextProvider<WashGoDbContext> contexts) : IBusinessCodeAllocator, ITransientDependency
{
    private const int RandomAttempts = 8;
    private const int FreeScanAttempts = 3;

    public async Task<string> AllocateAsync(string prefix, Guid resourceId)
    {
        var db = await contexts.GetDbContextAsync();
        // Replay or resume for the same resource returns the code it already holds.
        if (await ClaimedAsync(db, resourceId) is { } held) return held;
        for (var i = 0; i < RandomAttempts; i++)
            if (await TryClaimAsync(db, prefix, BusinessCodeRules.Format(prefix, RandomNumberGenerator.GetInt32(0, BusinessCodeRules.Space)), resourceId) is { } code)
                return code;
        // Repeated random collisions do not prove the space is full: look for a free number directly.
        for (var i = 0; i < FreeScanAttempts; i++)
        {
            var free = await FreeNumberAsync(db, "washgo.business_code_claims", "code", prefix);
            if (free == null) throw Exhausted(prefix);
            if (await TryClaimAsync(db, prefix, BusinessCodeRules.Format(prefix, free.Value), resourceId) is { } code)
                return code;
        }
        throw new CatalogRuleException("CodeAllocationBusy", "Hệ thống đang cấp nhiều mã cùng lúc; vui lòng thử lại.");
    }

    internal static CatalogRuleException Exhausted(string prefix) =>
        new("CodeSpaceExhausted", $"Đã dùng hết {BusinessCodeRules.Space} mã {prefix}xxxx; cần thống nhất định dạng mã mới trước khi tạo thêm.");

    /// <summary>A random unused number of the prefix in the claim table, or null when all 10.000 are taken.</summary>
    internal static async Task<int?> FreeNumberAsync(WashGoDbContext db, string table, string column, string prefix)
    {
        // Table/column names are constants of this assembly; only the prefix is a parameter.
        var sql = $"SELECT n AS \"Value\" FROM generate_series(0, {BusinessCodeRules.Space - 1}) AS n " +
                  $"WHERE NOT EXISTS (SELECT 1 FROM {table} c WHERE c.{column} = {{0}} || lpad(n::text, 4, '0')) ORDER BY random() LIMIT 1";
        var rows = await db.Database.SqlQueryRaw<int>(sql, prefix).ToListAsync();
        return rows.Count == 0 ? null : rows[0];
    }

    private static async Task<string?> TryClaimAsync(WashGoDbContext db, string prefix, string candidate, Guid resourceId)
    {
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO washgo.business_code_claims(code, prefix, resource_id, issued_at) VALUES ({candidate}, {prefix}, {resourceId}, {DateTime.UtcNow}) ON CONFLICT DO NOTHING");
        return inserted == 1 ? candidate : await ClaimedAsync(db, resourceId);
    }

    private static Task<string?> ClaimedAsync(WashGoDbContext db, Guid resourceId) =>
        db.BusinessCodeClaims.AsNoTracking().Where(x => x.ResourceId == resourceId).Select(x => (string?)x.Id).FirstOrDefaultAsync();
}
