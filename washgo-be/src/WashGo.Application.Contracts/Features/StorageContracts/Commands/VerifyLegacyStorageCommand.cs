namespace WashGo.Application.Contracts.Features.StorageContracts.Commands;
public class VerifyLegacyStorageCommand : StorageCommand
{
    public bool ConfirmedAtCurrentShop { get; set; }
    public string VerificationReference { get; set; } = string.Empty;
}
