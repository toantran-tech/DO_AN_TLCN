using System;
using WashGo.Core.DataAccess.Repository;

namespace WashGo.Domain.Entities.WashService.Interfaces
{
    public interface IWashServiceRepository : IMesRepository<WashService, Guid>
    {
    }
}
