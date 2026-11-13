namespace WashGo.Application.Contracts.Features.StorageContracts.Commands;
public class StorageHandoverCommand : StorageCommand
{
    public Guid TaskId { get; set; }
    public string? PinCode { get; set; }
    public string? QrCode { get; set; }
    public string? ReceiptReference { get; set; }
}
