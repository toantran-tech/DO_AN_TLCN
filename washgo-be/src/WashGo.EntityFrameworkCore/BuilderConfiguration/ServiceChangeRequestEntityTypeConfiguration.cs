using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{

    public class ServiceChangeRequestEntityTypeConfiguration : IEntityTypeConfiguration<ServiceChangeRequest>
    {
        public void Configure(EntityTypeBuilder<ServiceChangeRequest> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.ServiceChangeRequests, WashGoDbProperties.Schema);
            builder.Property(x => x.ProposedData).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.CurrentData).HasColumnType("jsonb");
            builder.HasIndex(x => x.MerchantId);
            builder.HasIndex(x => x.ServiceId);
            builder.HasIndex(x => x.Status);
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<WashService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
