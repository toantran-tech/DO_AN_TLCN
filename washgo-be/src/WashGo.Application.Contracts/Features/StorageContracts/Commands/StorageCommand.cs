using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
namespace WashGo.Application.Contracts.Features.StorageContracts.Commands;
public class StorageCommand : GuardedOrderCommand
{
    public string? Reason { get; set; }
}
