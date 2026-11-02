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

    public class MerchantServiceAreaEntityTypeConfiguration : IEntityTypeConfiguration<MerchantServiceArea>
    {
        public void Configure(EntityTypeBuilder<MerchantServiceArea> builder)
        {
            GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
            builder.ToTable(WashGoDbProperties.MerchantServiceAreas, WashGoDbProperties.Schema);
            builder.HasIndex(x => new { x.MerchantId, x.ServiceAreaId }).IsUnique();
            builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ServiceArea>().WithMany().HasForeignKey(x => x.ServiceAreaId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
