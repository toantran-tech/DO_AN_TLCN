using System;
using System.Threading.Tasks;
using WashGo.Core.Domain.DataAccess.Repository;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder.Interfaces
{
    public interface IWashOrderRepository : IMesRepository<WashOrder, Guid>
    {
        Task<WashOrder?> GetWithDetailsAsync(Guid id);
        Task<bool> TryChangeStatusAsync(Guid id, WashOrderStatus expected, WashOrderStatus next);
        Task<WashOrder?> GetForUpdateAsync(Guid id);
        /// <summary>pg_advisory_xact_lock theo khóa chuỗi, giữ tới hết transaction hiện tại.</summary>
        Task AcquireLockAsync(string key);
    }
}
