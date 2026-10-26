using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.Domain.Shared.Enums;

namespace WashGo.EntityFrameworkCore.Repositories
{
    public class LockerRepository : MesRepository<WashGoDbContext, Locker, Guid>, ILockerRepository
    {
        public LockerRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }

        public async Task<Locker?> GetWithBoxesAsync(Guid id)
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet
                .Include(x => x.Boxes)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<bool> TryReserveBoxAsync(Guid lockerId, Guid boxId)
        {
            var db = await GetDbContextAsync();
            // A single conditional UPDATE wins the slot even when two requests read it as available.
            var changed = await db.LockerBoxes
                .Where(x => x.Id == boxId && x.LockerId == lockerId && !x.IsDeleted
                    && x.Status == LockerBoxStatus.Available
                    && db.Lockers.Any(locker => locker.Id == lockerId && !locker.IsDeleted
                        && locker.Status == LockerStatus.Active
                        && db.Merchants.Any(merchant => merchant.Id == locker.MerchantId
                            && merchant.Status == MerchantStatus.Active)))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, LockerBoxStatus.Reserved));
            if (changed != 1)
                return false;

            var lockerChanged = await db.Lockers.Where(x => x.Id == lockerId && !x.IsDeleted
                    && x.Status == LockerStatus.Active)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvailableBoxes,
                    x => x.AvailableBoxes > 0 ? x.AvailableBoxes - 1 : 0));
            return lockerChanged == 1;
        }
    }

    public class WashServiceRepository : MesRepository<WashGoDbContext, WashService, Guid>, IWashServiceRepository
    {
        public WashServiceRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }
    }
}
