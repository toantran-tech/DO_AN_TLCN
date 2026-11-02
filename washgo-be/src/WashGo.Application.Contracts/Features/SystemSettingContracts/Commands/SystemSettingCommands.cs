namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Commands
{
    public class UpdateSlaSettingsCommand
    {
        public int DepositWindowMinutes { get; set; }
        public int MerchantCollectSlaHours { get; set; }
        public int MerchantReturnSlaHours { get; set; }
        public int FreePickupHours { get; set; }
    }

    public class UpdateCommissionSettingsCommand
    {
        public decimal DefaultCommissionRate { get; set; }
    }

    public class UpdatePenaltySettingsCommand
    {
        public decimal OverdueFeeDay1 { get; set; }
        public decimal OverdueFeeDay2 { get; set; }
        public decimal OverdueFeeDay3 { get; set; }
        public decimal StorageFeePerDay { get; set; }
        public int CancelLatePendingPercent { get; set; }
        public int CancelDepositedPercent { get; set; }
        public int CancelCollectedPercent { get; set; }
        public int CompensationLightPercent { get; set; }
        public int CompensationMediumPercent { get; set; }
        public int CompensationSeverePercent { get; set; }
        public int CompensationLostPercent { get; set; }
    }
}
