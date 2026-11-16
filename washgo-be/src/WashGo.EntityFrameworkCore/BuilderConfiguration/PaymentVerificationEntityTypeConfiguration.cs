using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Payment;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class PaymentVerificationEntityTypeConfiguration : IEntityTypeConfiguration<PaymentVerification>
{
    public void Configure(EntityTypeBuilder<PaymentVerification> builder)
    {
        builder.ToTable("payment_verifications", "washgo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.State).HasMaxLength(24).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(80);
        builder.HasIndex(x => new { x.IsActive, x.NextAttemptAt });
        builder.HasIndex(x => new { x.TransactionId, x.CreatedAt });
        builder.HasIndex(x => x.TransactionId).IsUnique().HasDatabaseName("ux_payment_verification_active")
            .HasFilter("is_active = true AND transaction_id IS NOT NULL");
        // Keep operation evidence if the order/receipt is later archived; source IDs are immutable references.
    }
}
