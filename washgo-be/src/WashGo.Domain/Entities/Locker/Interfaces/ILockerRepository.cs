using System;
using System.Threading.Tasks;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Locker.Interfaces
{
    public interface ILockerRepository : IMesRepository<Locker, Guid>
    {
        Task<Locker?> GetWithBoxesAsync(Guid id);
        Task<bool> TryReserveBoxAsync(Guid lockerId, Guid boxId);
        Task<bool> TryReleaseReservedBoxAsync(Guid lockerId, Guid boxId);
        Task<bool> TryReleaseOccupiedBoxAsync(Guid lockerId, Guid boxId);
        Task<bool> TryOccupyReservedBoxAsync(Guid lockerId, Guid boxId);
        Task<bool> TrySetUnassignedBoxStatusAsync(Guid lockerId, Guid boxId,
            LockerBoxStatus expectedStatus, LockerBoxStatus newStatus);
        Task<bool> TryUpdateDetailsAsync(Locker locker, LockerStatus expectedStatus);
    }
}
