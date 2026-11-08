namespace WashGo.Domain.Entities.Finance;

public interface ISettlementSnapshotReader
{
    Task<(List<string> Lines, int Total)?> ReadAsync(Guid id, Guid? merchantId, int page, int take);
}
