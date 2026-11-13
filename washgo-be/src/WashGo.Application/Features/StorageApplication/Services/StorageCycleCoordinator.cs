using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Domain.Entities.SystemConfig;
namespace WashGo.Application.Features.StorageApplication.Services;
public class StorageCycleCoordinator(IRepository<OrderStorageCycle, Guid> cycles, IRepository<Merchant, Guid> merchants) : ITransientDependency
{
    // Caller holds the order lock and owns the transaction; no fabricated historical handover events.
    public async Task<OrderStorageCycle?> StartAsync(WashOrder order, StorageCycleState state, DateTime now, string basis)
    {
        if (order.ActiveStorageCycleId.HasValue) return await cycles.FindAsync(order.ActiveStorageCycleId.Value);
        var merchant = order.MerchantId.HasValue ? await merchants.FindAsync(order.MerchantId.Value) : null;
        if (merchant == null || string.IsNullOrWhiteSpace(merchant.Address) || string.IsNullOrWhiteSpace(merchant.City)) return null;
        var row = new OrderStorageCycle(Guid.NewGuid())
        {
            OrderId = order.Id, MerchantId = merchant.Id, State = state, CreatedAt = now, Basis = basis,
            ShopAddress = string.Join(", ", new[] { merchant.Address, merchant.District, merchant.City }.Where(x => !string.IsNullOrWhiteSpace(x))),
            SourceLockerId = state == StorageCycleState.AwaitingCollection ? order.ReturnLockerId ?? order.LockerId : null,
            SourceBoxId = state == StorageCycleState.AwaitingCollection ? order.ReturnBoxId ?? order.BoxId : null,
            IntakeAt = state == StorageCycleState.Stored ? order.MovedToStorageAt : null
        };
        await cycles.InsertAsync(row, autoSave: true);
        order.StorageWorkflowVersion = 1; order.ActiveStorageCycleId = row.Id;
        return row;
    }
    public async Task<OrderStorageCycle?> AdoptStoredAsync(WashOrder order, SystemSetting settings, DateTime now, bool legacyVerified = false)
    {
        if (order.Status != WashOrderStatus.MovedToStorage || !order.MovedToStorageAt.HasValue) return null;
        if (order.ActiveStorageCycleId.HasValue) return await cycles.FindAsync(order.ActiveStorageCycleId.Value);
        var legacy = order.StorageWorkflowVersion == 0;
        if (legacy && !legacyVerified) return null;
        if (legacy) order.AccumulatedStorageFee = order.LockerOverdueFeeAtStorageTransfer ?? OverdueFeeCalculator.Calculate(order.PickupDeadline, order.MovedToStorageAt.Value, settings);
        var cycle = await StartAsync(order, StorageCycleState.Stored, now, legacy ? "LegacyAdminVerifiedCurrentShop" : "SystemStoredAtShop");
        if (cycle != null) cycle.LockerFee = order.AccumulatedStorageFee;
        return cycle;
    }
}
