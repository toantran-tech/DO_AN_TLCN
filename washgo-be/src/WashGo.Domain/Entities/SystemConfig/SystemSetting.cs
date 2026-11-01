using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.SystemConfig
{
    /// <summary>
    /// Cấu hình vận hành toàn hệ thống (UC 3.9), chỉ có một bản ghi. Giá trị mặc định trùng với
    /// quy tắc đang áp dụng trước khi có màn hình cấu hình.
    /// </summary>
    [SevagoTable(WashGoDbProperties.SystemSettings, Schema = WashGoDbProperties.Schema)]
    public class SystemSetting : Entity<Guid>
    {
        public static readonly Guid SingletonId = Guid.Parse("5e771165-0000-4000-8000-000000000001");

        /// <summary>Phí quá hạn lưu locker có 3 mức ngày; sau đó đồ được chuyển kho.</summary>
        public const int StorageTransferAfterHours = 72;

        public SystemSetting()
        {
            Id = SingletonId;
        }

        // SLA (UC 3.9.1)
        public int DepositWindowMinutes { get; set; } = 30;
        public int MerchantCollectSlaHours { get; set; } = 4;
        public int MerchantReturnSlaHours { get; set; } = 24;
        public int FreePickupHours { get; set; } = 24;

        // Commission (UC 3.9.2)
        public decimal DefaultCommissionRate { get; set; } = 20;

        // Overdue locker / storage fees (UC 3.9.4)
        public decimal OverdueFeeDay1 { get; set; } = 10000;
        public decimal OverdueFeeDay2 { get; set; } = 20000;
        public decimal OverdueFeeDay3 { get; set; } = 50000;
        public decimal StorageFeePerDay { get; set; } = 100000;

        // Cancellation fee percentages (UC 3.9.4, áp dụng theo luồng hủy đơn đã chốt)
        public int CancelLatePendingPercent { get; set; } = 30;
        public int CancelDepositedPercent { get; set; } = 30;
        public int CancelCollectedPercent { get; set; } = 50;

        // Compensation percentages of item value (UC 3.9.4)
        public int CompensationLightPercent { get; set; } = 30;
        public int CompensationMediumPercent { get; set; } = 70;
        public int CompensationSeverePercent { get; set; } = 100;
        public int CompensationLostPercent { get; set; } = 200;

        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public int CompensationPercent(CompensationLevel level) => level switch
        {
            CompensationLevel.Light => CompensationLightPercent,
            CompensationLevel.Medium => CompensationMediumPercent,
            CompensationLevel.Severe => CompensationSeverePercent,
            CompensationLevel.Lost => CompensationLostPercent,
            _ => 0
        };
    }
}
