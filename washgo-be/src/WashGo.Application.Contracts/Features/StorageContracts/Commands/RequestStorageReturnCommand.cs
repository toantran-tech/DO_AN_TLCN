namespace WashGo.Application.Contracts.Features.StorageContracts.Commands;
public class RequestStorageReturnCommand : StorageCommand
{
    public string QuoteToken { get; set; } = string.Empty;
}
