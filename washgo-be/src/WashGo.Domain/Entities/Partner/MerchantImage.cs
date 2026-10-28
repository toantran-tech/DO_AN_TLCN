using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Partner
{

    [SevagoTable(WashGoDbProperties.MerchantImages, Schema = WashGoDbProperties.Schema)]
    public class MerchantImage : Entity<Guid>
    {
        public MerchantImage() { }
        public MerchantImage(Guid id) : base(id) { }

        public Guid MerchantId { get; set; }
        public string ImageType { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public string SecureUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
        public string? Metadata { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
