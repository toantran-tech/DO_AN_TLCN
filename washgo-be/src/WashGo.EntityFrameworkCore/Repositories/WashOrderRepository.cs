using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.Domain.Shared.Enums;

namespace WashGo.EntityFrameworkCore.Repositories
{
    public class WashOrderRepository : MesRepository<WashGoDbContext, WashOrder, Guid>, IWashOrderRepository
    {
        public WashOrderRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }

        public async Task<WashOrder?> GetWithDetailsAsync(Guid id)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet
                .Include(x => x.Items)
                .Include(x => x.Timeline)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<bool> TryChangeStatusAsync(Guid id, WashOrderStatus expected, WashOrderStatus next)
        {
            var db = await GetDbContextAsync();
            return await db.WashOrders
                .Where(x => x.Id == id && !x.IsDeleted && x.Status == expected)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, next)) == 1;
        }

        public async Task<WashOrder?> GetForUpdateAsync(Guid id)
        {
            var db = await GetDbContextAsync();
            var rows = await db.WashOrders.FromSqlInterpolated(
                $"SELECT * FROM washgo.orders WHERE id = {id} AND is_deleted = false FOR UPDATE")
                .ToListAsync();
            return rows.FirstOrDefault();
        }
    }
}
