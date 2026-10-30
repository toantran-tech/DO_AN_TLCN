using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class WashOrderEntityTypeConfiguration : EntityBaseConfiguration<WashOrder>
    {
        public override void Configure(EntityTypeBuilder<WashOrder> builder)
        {
            base.Configure(builder);
            builder.ToTable(WashGoDbProperties.Orders, WashGoDbProperties.Schema);
            builder.Property(x => x.OrderCode).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => x.OrderCode).IsUnique();
            builder.Property(x => x.MerchantOrderCode).HasMaxLength(50);
            builder.HasIndex(x => x.MerchantOrderCode).IsUnique();
            builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
            builder.Property(x => x.CustomerPhoneNumber).HasMaxLength(20).IsRequired();
            builder.Property(x => x.LockerName).HasMaxLength(150);
            builder.Property(x => x.BoxId).HasColumnName("locker_slot_id");
            builder.Property(x => x.BoxNumber).HasColumnName("slot_number").HasMaxLength(20);
            builder.Property(x => x.DepositPinCode).HasMaxLength(10);
            builder.Property(x => x.PickupPinCode).HasMaxLength(10);
            builder.Property(x => x.QrCodeString).HasColumnName("qr_code").HasMaxLength(500);
            builder.Property(x => x.ShipperTaskPinCode).HasMaxLength(10);
            builder.Property(x => x.ShipperTaskQrString).HasMaxLength(500);
            builder.Property(x => x.PickupSessionTokenHash).HasMaxLength(128);
            builder.Property(x => x.CustomerNote).HasColumnName("note");
            builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
            builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            builder.Property(x => x.FinalAmount).HasPrecision(18, 2);
            builder.Property(x => x.ActualWeightKg).HasPrecision(18, 2);
            builder.Property(x => x.IncludedWeightKg).HasPrecision(18, 2);
            builder.Property(x => x.WeightSurchargeAmount).HasPrecision(18, 2);
            builder.Property(x => x.OverdueFeeTotal).HasPrecision(18, 2);
            builder.Property(x => x.StorageFeeTotal).HasPrecision(18, 2);
            builder.Property(x => x.LockerOverdueFeeAtStorageTransfer).HasPrecision(18, 2);
            builder.Property(x => x.StorageLocation).HasMaxLength(200);
            builder.Property(x => x.StorageReference).HasMaxLength(120);
            builder.Property(x => x.CancelFeeAmount).HasPrecision(18, 2);
            builder.HasIndex(x => x.CustomerId);
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.PromotionId);
            builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Shipper>().WithMany().HasForeignKey(x => x.ShipperId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Locker>().WithMany().HasForeignKey(x => x.LockerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<LockerBox>().WithMany().HasForeignKey(x => x.BoxId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Locker>().WithMany().HasForeignKey(x => x.ReturnLockerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<LockerBox>().WithMany().HasForeignKey(x => x.ReturnBoxId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.SetNull);
            builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.WashOrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Timeline).WithOne().HasForeignKey(x => x.WashOrderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
