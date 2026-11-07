using System;
using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.WashService.Interfaces
{
    public interface IWashServiceRepository : IMesRepository<WashService, Guid>
    {
        /// <summary>Khóa tư vấn (pg_advisory_xact_lock) theo khóa chuỗi, giữ tới hết transaction hiện tại.</summary>
        System.Threading.Tasks.Task AcquireLockAsync(string key);
    }
}
