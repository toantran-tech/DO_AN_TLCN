using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public class MerchantProfileRequestEntityTypeConfiguration : IEntityTypeConfiguration<MerchantProfileRequest>
    {
        public void Configure(EntityTypeBuilder<MerchantProfileRequest> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.MerchantProfileRequests, WashGoDbProperties.Schema);
            builder.Property(x => x.ProposedData).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.CurrentData).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.RejectionReason).HasMaxLength(1000);
            builder.HasIndex(x => new { x.MerchantId, x.Status });
            builder.HasIndex(x => x.Status);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
