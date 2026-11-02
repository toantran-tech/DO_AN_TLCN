using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.Payment.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class PaymentTransactionRepository : MesRepository<WashGoDbContext, PaymentTransaction, Guid>,
    IPaymentTransactionRepository
{
    public PaymentTransactionRepository(IDbContextProvider<WashGoDbContext> dbContextProvider,
        IObjectMapper objectMapper) : base(dbContextProvider, objectMapper)
    {
    }

    public async Task<PaymentTransaction?> GetForUpdateByReferenceAsync(string reference)
    {
        var db = await GetDbContextAsync();
        var rows = await db.PaymentTransactions.FromSqlInterpolated(
            $"SELECT * FROM washgo.payment_transactions WHERE transaction_reference = {reference} AND is_deleted = false FOR UPDATE")
            .ToListAsync();
        return rows.FirstOrDefault();
    }
}
