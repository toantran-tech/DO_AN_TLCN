using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Commands
{
    public class SettingWriteCommand
    {
        public string? ExpectedVersion { get; set; }
        public Guid? OperationId { get; set; }
    }
    public class UpdateSlaSettingsCommand : SettingWriteCommand
    {
        [JsonRequired]
        public int DepositWindowMinutes { get; set; }
        [JsonRequired]
        public int MerchantCollectSlaHours { get; set; }
        [JsonRequired]
        public int MerchantReturnSlaHours { get; set; }
        [JsonRequired]
        public int FreePickupHours { get; set; }
    }

    public class UpdateCommissionSettingsCommand : SettingWriteCommand
    {
        [JsonRequired]
        public decimal DefaultCommissionRate { get; set; }
    }

    public class UpdatePenaltySettingsCommand : SettingWriteCommand
    {
        [JsonRequired]
        public decimal OverdueFeeDay1 { get; set; }
        [JsonRequired]
        public decimal OverdueFeeDay2 { get; set; }
        [JsonRequired]
        public decimal OverdueFeeDay3 { get; set; }
        [JsonRequired]
        public decimal StorageFeePerDay { get; set; }
        [JsonRequired]
        public int CancelLatePendingPercent { get; set; }
        [JsonRequired]
        public int CancelDepositedPercent { get; set; }
        [JsonRequired]
        public int CancelCollectedPercent { get; set; }
        [JsonRequired]
        public int CompensationLightPercent { get; set; }
        [JsonRequired]
        public int CompensationMediumPercent { get; set; }
        [JsonRequired]
        public int CompensationSeverePercent { get; set; }
        [JsonRequired]
        public int CompensationLostPercent { get; set; }
    }
}
