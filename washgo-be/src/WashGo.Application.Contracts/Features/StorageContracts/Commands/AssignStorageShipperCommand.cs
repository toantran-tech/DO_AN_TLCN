namespace WashGo.Application.Contracts.Features.StorageContracts.Commands;
public class AssignStorageShipperCommand : StorageCommand
{
    public Guid ShipperId { get; set; }
}
