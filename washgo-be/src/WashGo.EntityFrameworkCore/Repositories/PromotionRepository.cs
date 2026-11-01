using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Entities.Promotion.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories
{
    public class PromotionRepository : MesRepository<WashGoDbContext, Promotion, Guid>, IPromotionRepository
    {
        public PromotionRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }

        public async Task<bool> TryConsumeAsync(Guid promotionId, DateTime now)
        {
            var db = await GetDbContextAsync();
            // The conditional UPDATE both enforces the global limit and serializes concurrent orders on this code.
            return await db.Promotions
                .Where(x => x.Id == promotionId && !x.IsDeleted && x.IsActive
                    && x.StartAt <= now && x.EndAt >= now
                    && (x.UsageLimit == null || x.UsedCount < x.UsageLimit))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedCount, x => x.UsedCount + 1)) == 1;
        }

        public async Task<bool> TryReleaseAsync(Guid promotionId)
        {
            var db = await GetDbContextAsync();
            return await db.Promotions
                .Where(x => x.Id == promotionId && x.UsedCount > 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedCount, x => x.UsedCount - 1)) == 1;
        }
    }
}
