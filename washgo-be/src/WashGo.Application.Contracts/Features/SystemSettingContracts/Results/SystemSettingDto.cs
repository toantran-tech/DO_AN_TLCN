using System;

namespace WashGo.Application.Contracts.Features.SystemSettingContracts.Results
{
    public class SystemSettingDto
    {
        public string Version { get; set; } = string.Empty;
        public bool IsPersisted { get; set; }
        public string Source => IsPersisted ? "database" : "defaults";
        public DateTime ServerNow { get; set; }
        public Guid? UpdatedBy { get; set; }
        public int DepositWindowMinutes { get; set; }
        public int MerchantCollectSlaHours { get; set; }
        public int MerchantReturnSlaHours { get; set; }
        /// <summary>Giới hạn nhiệm vụ chưa hoàn tất của mỗi Shipper (1–20).</summary>
        public int MaxActiveTasksPerShipper { get; set; }
        public int FreePickupHours { get; set; }
        public decimal DefaultCommissionRate { get; set; }
        public decimal LoyaltyEarnPercent { get; set; }
        public decimal LoyaltyMaxUsePercent { get; set; }
        public decimal OverdueFeeDay1 { get; set; }
        public decimal OverdueFeeDay2 { get; set; }
        public decimal OverdueFeeDay3 { get; set; }
        public decimal StorageFeePerDay { get; set; }
        public int StorageTransferAfterHours { get; set; }
        public int CancelLatePendingPercent { get; set; }
        public int CancelDepositedPercent { get; set; }
        public int CancelCollectedPercent { get; set; }
        public int CompensationLightPercent { get; set; }
        public int CompensationMediumPercent { get; set; }
        public int CompensationSeverePercent { get; set; }
        public int CompensationLostPercent { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
