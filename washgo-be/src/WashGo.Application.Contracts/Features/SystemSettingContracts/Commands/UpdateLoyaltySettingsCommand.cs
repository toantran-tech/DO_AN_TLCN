using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;

public class UpdateLoyaltySettingsCommand : SettingWriteCommand
{
    [JsonRequired]
    public decimal EarnPercent { get; set; }
    [JsonRequired]
    public decimal MaxUsePercent { get; set; }
}
