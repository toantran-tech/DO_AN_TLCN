using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Constants;
using Volo.Abp.Domain.Entities;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    internal static class GuidKeyConfiguration
    {
        public static void ConfigureGeneratedGuid<TEntity>(EntityTypeBuilder<TEntity> builder)
            where TEntity : Entity<Guid>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasValueGenerator<SequentialGuidValueGenerator>();
        }
    }
}
