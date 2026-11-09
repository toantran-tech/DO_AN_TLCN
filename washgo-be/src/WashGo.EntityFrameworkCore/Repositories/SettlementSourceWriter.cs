using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Finance;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
namespace WashGo.EntityFrameworkCore.Repositories;
public class SettlementSourceWriter(IDbContextProvider<WashGoDbContext> contexts) : ISettlementSourceWriter, ITransientDependency
{
    public async Task<bool> AttachAsync(Guid id, List<Guid> payments, List<Guid> refunds)
    {
        var db = await contexts.GetDbContextAsync();
        var p = await db.PaymentTransactions.Where(x => payments.Contains(x.Id) && x.SettlementId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.SettlementId, id));
        var r = await db.RefundRequests.Where(x => refunds.Contains(x.Id) && x.SettlementId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.SettlementId, id));
        return p == payments.Count && r == refunds.Count;
    }
}
