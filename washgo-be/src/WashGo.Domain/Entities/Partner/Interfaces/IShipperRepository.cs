using Volo.Abp.Domain.Repositories;

namespace WashGo.Domain.Entities.Partner.Interfaces;

public interface IShipperRepository : IRepository<Shipper, Guid>
{
    Task<Shipper?> GetForUpdateAsync(Guid id);
}
