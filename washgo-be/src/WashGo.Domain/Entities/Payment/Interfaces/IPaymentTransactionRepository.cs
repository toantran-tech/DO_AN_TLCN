using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.Payment.Interfaces;

public interface IPaymentTransactionRepository : IMesRepository<PaymentTransaction, Guid>
{
    Task<PaymentTransaction?> GetForUpdateByReferenceAsync(string reference);
}
