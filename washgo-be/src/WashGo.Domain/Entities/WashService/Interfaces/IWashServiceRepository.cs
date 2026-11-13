using System;
using WashGo.Core.Domain.DataAccess.Repository;
using System.Threading.Tasks;

namespace WashGo.Domain.Entities.WashService.Interfaces
{
    public interface IWashServiceRepository : IMesRepository<WashService, Guid>
    {
        /// <summary>Khóa tư vấn (pg_advisory_xact_lock) theo khóa chuỗi, giữ tới hết transaction hiện tại.</summary>
        Task AcquireLockAsync(string key);
    }
}
