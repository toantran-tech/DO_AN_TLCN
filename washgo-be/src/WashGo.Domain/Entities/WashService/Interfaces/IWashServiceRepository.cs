using System;
using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.WashService.Interfaces
{
    public interface IWashServiceRepository : IMesRepository<WashService, Guid>
    {
    }
}
