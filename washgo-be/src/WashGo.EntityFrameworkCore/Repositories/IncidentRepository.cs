using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Feedback.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories
{
    public class IncidentRepository : MesRepository<WashGoDbContext, Incident, Guid>, IIncidentRepository
    {
        public IncidentRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }

        public async Task<Incident?> GetForUpdateAsync(Guid id)
        {
            var db = await GetDbContextAsync();
            var rows = await db.Incidents.FromSqlInterpolated(
                $"SELECT * FROM washgo.incidents WHERE id = {id} AND is_deleted = false FOR UPDATE")
                .ToListAsync();
            return rows.FirstOrDefault();
        }
    }
}
