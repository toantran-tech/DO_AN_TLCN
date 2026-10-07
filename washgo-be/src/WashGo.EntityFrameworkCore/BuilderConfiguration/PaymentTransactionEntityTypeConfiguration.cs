using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class PaymentTransactionEntityTypeConfiguration : IEntityTypeConfiguration<PaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            builder.ToTable(WashGoDbProperties.PaymentTransactions, WashGoDbProperties.Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
            builder.Property(x => x.TransactionReference).HasMaxLength(120).IsRequired();
            builder.HasIndex(x => x.TransactionReference).IsUnique();
            builder.Property(x => x.ProviderTransactionId).HasMaxLength(120);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.PaymentUrl).HasMaxLength(1000);
            builder.Property(x => x.FailureReason).HasMaxLength(500);
            builder.HasIndex(x => x.OrderId);
            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
