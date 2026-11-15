using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Loyalty;
using WashGo.Domain.Entities.Partner;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class LoyaltyWalletStore(IDbContextProvider<WashGoDbContext> contexts) : ILoyaltyWalletStore, ITransientDependency
{
    public async Task<LoyaltyWallet> LockAsync(Guid customerId)
    {
        var db = await contexts.GetDbContextAsync();
        var key = $"loyalty-wallet:{customerId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
        var wallet = await db.LoyaltyWallets.SingleOrDefaultAsync(x => x.Id == customerId);
        if (wallet != null)
        {
            await db.Entry(wallet).ReloadAsync();
            return wallet;
        }
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == customerId);
        if (customer.LoyaltyPoints < 0) throw new InvalidOperationException("Legacy negative points require review before opening a wallet.");
        wallet = new LoyaltyWallet(customerId) { NetPoints = customer.LoyaltyPoints };
        db.LoyaltyWallets.Add(wallet);
        db.LoyaltyEntries.Add(new LoyaltyEntry(Guid.NewGuid())
        {
            CustomerId = customerId, EventKey = $"opening:{customerId}", Kind = "Opening",
            PointsDelta = customer.LoyaltyPoints, BalanceAfter = customer.LoyaltyPoints
        });
        await db.SaveChangesAsync();
        return wallet;
    }

    public async Task SaveAsync(LoyaltyWallet wallet)
    {
        var db = await contexts.GetDbContextAsync();
        wallet.UpdatedAt = DateTime.UtcNow;
        // Compatibility field stays nonnegative. Wallet endpoints expose debt/held/available explicitly.
        var points = (int)Math.Clamp(wallet.NetPoints, 0, int.MaxValue);
        await db.Customers.Where(x => x.Id == wallet.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LoyaltyPoints, points));
        var tracked = db.ChangeTracker.Entries<Customer>().FirstOrDefault(x => x.Entity.Id == wallet.Id);
        if (tracked != null)
        {
            tracked.Property(x => x.LoyaltyPoints).CurrentValue = points;
            tracked.Property(x => x.LoyaltyPoints).OriginalValue = points;
        }
        await db.SaveChangesAsync();
    }
}
