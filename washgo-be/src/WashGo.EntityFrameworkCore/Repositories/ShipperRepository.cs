using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class ShipperRepository(IDbContextProvider<WashGoDbContext> provider)
    : EfCoreRepository<WashGoDbContext, Shipper, Guid>(provider), IShipperRepository
{
    public async Task<Shipper?> GetForUpdateAsync(Guid id)
    {
        var db = await GetDbContextAsync();
        var rows = await db.Shippers.FromSqlInterpolated(
            $"SELECT * FROM washgo.shippers WHERE id = {id} FOR UPDATE").ToListAsync();
        return rows.FirstOrDefault();
    }
}
