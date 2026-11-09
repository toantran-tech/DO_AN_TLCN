using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NpgsqlTypes;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class MerchantSettlementEntityTypeConfiguration : IEntityTypeConfiguration<MerchantSettlement>
    {
        public void Configure(EntityTypeBuilder<MerchantSettlement> builder)
        {
            builder.ToTable(WashGoDbProperties.MerchantSettlements, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property<NpgsqlTsVector>("SearchVector");
            builder.Property(x => x.MerchantName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.GrossRevenue).HasPrecision(18, 2);
            builder.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            builder.Property(x => x.NetRevenue).HasPrecision(18, 2);
            builder.Property(x => x.CommissionRate).HasPrecision(5, 2);
            builder.Property(x => x.CommissionAmount).HasPrecision(18, 2);
            builder.Property(x => x.PayoutAmount).HasPrecision(18, 2);
            builder.Property(x => x.PeriodPayoutAmount).HasPrecision(18, 2);
            builder.Property(x => x.CarryForwardAmount).HasPrecision(18, 2);
            builder.HasIndex(x => x.CarriedToSettlementId);
            builder.Property(x => x.PaymentReference).HasMaxLength(120);
            builder.HasIndex(x => new { x.MerchantId, x.PeriodFrom, x.PeriodTo });
            builder.HasIndex(x => x.Status);
        }
    }
}
