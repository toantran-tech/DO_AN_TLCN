namespace WashGo.Domain.Entities.Finance;
public interface ISettlementSourceWriter
{
    Task<bool> AttachAsync(Guid settlementId, List<Guid> paymentIds, List<Guid> refundIds);
}
