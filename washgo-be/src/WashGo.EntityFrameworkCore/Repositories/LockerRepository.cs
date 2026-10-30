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

        public Task<bool> TryReleaseReservedBoxAsync(Guid lockerId, Guid boxId) =>
            TryReleaseBoxAsync(lockerId, boxId, LockerBoxStatus.Reserved);

        public Task<bool> TryReleaseOccupiedBoxAsync(Guid lockerId, Guid boxId) =>
            TryReleaseBoxAsync(lockerId, boxId, LockerBoxStatus.Occupied);

        private async Task<bool> TryReleaseBoxAsync(Guid lockerId, Guid boxId, LockerBoxStatus expectedStatus)
        {
            var db = await GetDbContextAsync();
            var changed = await db.LockerBoxes
                .Where(x => x.Id == boxId && x.LockerId == lockerId && !x.IsDeleted
                    && x.Status == expectedStatus)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, LockerBoxStatus.Available));
            if (changed != 1)
                return false;

            var lockerChanged = await db.Lockers.Where(x => x.Id == lockerId && !x.IsDeleted)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvailableBoxes,
                    x => x.AvailableBoxes + 1));
            return lockerChanged == 1;
        }

        public async Task<bool> TryOccupyReservedBoxAsync(Guid lockerId, Guid boxId)
        {
            var db = await GetDbContextAsync();
            return await db.LockerBoxes
                .Where(x => x.Id == boxId && x.LockerId == lockerId && !x.IsDeleted
                    && x.Status == LockerBoxStatus.Reserved)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, LockerBoxStatus.Occupied)) == 1;
        }

        public async Task<bool> TrySetUnassignedBoxStatusAsync(Guid lockerId, Guid boxId,
            LockerBoxStatus expectedStatus, LockerBoxStatus newStatus)
        {
            var db = await GetDbContextAsync();
            var changed = await db.LockerBoxes
                .Where(x => x.Id == boxId && x.LockerId == lockerId && !x.IsDeleted
                    && x.Status == expectedStatus
                    && !db.WashOrders.Any(order => !order.IsDeleted &&
                        ((order.BoxId == boxId &&
                          (order.Status == WashOrderStatus.Pending || order.Status == WashOrderStatus.Deposited)) ||
                         (order.Status == WashOrderStatus.Cancelled &&
                          ((order.CancelledGoodsStatus == CancelledGoodsStatus.AtDepositLocker && order.BoxId == boxId) ||
                           (order.CancelledGoodsStatus == CancelledGoodsStatus.AtReturnLocker && order.ReturnBoxId == boxId))) ||
                         (order.Status == WashOrderStatus.ReadyToReturn &&
                          (order.ReturnBoxId == boxId ||
                           (order.ReturnBoxId == null && order.BoxId == boxId))))))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, newStatus));
            if (changed != 1)
                return false;

            if ((expectedStatus == LockerBoxStatus.Available) == (newStatus == LockerBoxStatus.Available))
                return true;

            var becomingAvailable = newStatus == LockerBoxStatus.Available;
            var lockerChanged = await db.Lockers.Where(x => x.Id == lockerId && !x.IsDeleted)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvailableBoxes,
                    x => becomingAvailable
                        ? (x.AvailableBoxes < x.TotalBoxes ? x.AvailableBoxes + 1 : x.TotalBoxes)
                        : (x.AvailableBoxes > 0 ? x.AvailableBoxes - 1 : 0)));
            return lockerChanged == 1;
        }

        public async Task<bool> TryUpdateDetailsAsync(Locker locker, LockerStatus expectedStatus)
        {
            var db = await GetDbContextAsync();
            var query = db.Lockers.Where(x => x.Id == locker.Id && !x.IsDeleted && x.Status == expectedStatus);
            if (locker.Status != expectedStatus && locker.Status != LockerStatus.Active)
            {
                query = query.Where(x =>
                    !db.LockerBoxes.Any(box => box.LockerId == x.Id && !box.IsDeleted &&
                        (box.Status == LockerBoxStatus.Reserved || box.Status == LockerBoxStatus.Occupied)) &&
                    !db.WashOrders.Any(order => !order.IsDeleted &&
                        ((order.LockerId == x.Id &&
                          (order.Status == WashOrderStatus.Pending || order.Status == WashOrderStatus.Deposited)) ||
                         (order.Status == WashOrderStatus.ReadyToReturn &&
                          (order.ReturnBoxId.HasValue
                              ? (order.ReturnLockerId ?? order.LockerId) == x.Id
                              : order.LockerId == x.Id)))));
            }

            return await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Name, locker.Name)
                .SetProperty(x => x.Address, locker.Address)
                .SetProperty(x => x.Ward, locker.Ward)
                .SetProperty(x => x.District, locker.District)
                .SetProperty(x => x.City, locker.City)
                .SetProperty(x => x.Latitude, locker.Latitude)
                .SetProperty(x => x.Longitude, locker.Longitude)
                .SetProperty(x => x.LockerType, locker.LockerType)
                .SetProperty(x => x.Description, locker.Description)
                .SetProperty(x => x.Status, locker.Status)) == 1;
        }
    }
}
