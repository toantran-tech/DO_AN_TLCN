using System;
using System.Threading.Tasks;
using WashGo.Core.DataAccess.Repository;

namespace WashGo.Domain.Entities.Locker.Interfaces
{
    public interface ILockerRepository : IMesRepository<Locker, Guid>
    {
        Task<Locker?> GetWithBoxesAsync(Guid id);
    }
}
