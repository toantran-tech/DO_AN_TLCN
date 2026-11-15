namespace WashGo.Domain.Entities.Loyalty;

public interface ILoyaltyWalletStore
{
    /// <summary>Caller owns a transactional UoW. Always acquire after transaction/order locks.</summary>
    Task<LoyaltyWallet> LockAsync(Guid customerId);
    Task SaveAsync(LoyaltyWallet wallet);
}
