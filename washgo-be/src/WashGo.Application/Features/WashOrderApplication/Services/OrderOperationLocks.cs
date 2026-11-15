using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WashGo.Domain.Entities.WashOrder.Interfaces;

namespace WashGo.Application.Features.WashOrderApplication.Services;

/// <summary>All OrderOperation writers acquire these keys before order/capacity locks.</summary>
public static class OrderOperationLocks
{
    public static Task AcquireAsync(IWashOrderRepository repository, Guid? operationId) =>
        AcquireManyAsync(repository, operationId.HasValue ? new[] { operationId.Value } : Array.Empty<Guid>());

    public static async Task AcquireManyAsync(IWashOrderRepository repository, IEnumerable<Guid> operationIds)
    {
        foreach (var id in operationIds.Distinct().OrderBy(x => x))
            await repository.AcquireLockAsync($"order-operation:{id}");
    }
}
