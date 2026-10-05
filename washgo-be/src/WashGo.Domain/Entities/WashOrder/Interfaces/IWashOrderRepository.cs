using System;
using System.Threading.Tasks;
using WashGo.Core.DataAccess.Repository;

namespace WashGo.Domain.Entities.WashOrder.Interfaces
{
    public interface IWashOrderRepository : IMesRepository<WashOrder, Guid>
    {
        Task<WashOrder?> GetWithDetailsAsync(Guid id);
    }
}
