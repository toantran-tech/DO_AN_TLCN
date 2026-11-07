using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Policy
{
    /// <summary>Customer đã xem và đồng ý một phiên bản điều khoản (lưu ở server để đồng bộ mọi thiết bị, có dấu vết).</summary>
    [SevagoTable(WashGoDbProperties.TermsAcceptances, Schema = WashGoDbProperties.Schema)]
    public class TermsAcceptance : Entity<Guid>
    {
        public TermsAcceptance() { }
        public TermsAcceptance(Guid id) : base(id) { }

        public Guid UserId { get; set; }
        public Guid PolicyId { get; set; }
        public string Version { get; set; } = string.Empty;
        public DateTime AcceptedAt { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}
