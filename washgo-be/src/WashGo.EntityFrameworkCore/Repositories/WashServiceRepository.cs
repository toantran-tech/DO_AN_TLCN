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

    public class WashServiceRepository : MesRepository<WashGoDbContext, WashService, Guid>, IWashServiceRepository
    {
        public WashServiceRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }
    }
}
