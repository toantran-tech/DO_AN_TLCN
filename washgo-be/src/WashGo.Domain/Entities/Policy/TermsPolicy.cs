using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Policy
{
    /// <summary>
    /// Một phiên bản điều khoản dịch vụ dành cho Customer. Phiên bản hiện hành là bản có
    /// <see cref="EffectiveFrom"/> mới nhất không vượt quá thời điểm hiện tại.
    /// </summary>
    [SevagoTable(WashGoDbProperties.TermsPolicies, Schema = WashGoDbProperties.Schema)]
    public class TermsPolicy : Entity<Guid>
    {
        public static readonly Guid DefaultId = Guid.Parse("7e2a5000-0000-4000-8000-000000000001");
        public const string DefaultVersion = "1.0";

        public TermsPolicy() { }
        public TermsPolicy(Guid id) : base(id) { }

        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
