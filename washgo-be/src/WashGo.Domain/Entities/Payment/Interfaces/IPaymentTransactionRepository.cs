using WashGo.Core.DataAccess.Repository;

namespace WashGo.Domain.Entities.Payment.Interfaces;

public interface IPaymentTransactionRepository : IMesRepository<PaymentTransaction, Guid>
{
    Task<PaymentTransaction?> GetForUpdateByReferenceAsync(string reference);
}
