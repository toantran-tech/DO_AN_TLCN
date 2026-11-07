using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Feedback
{
    /// <summary>Trao đổi giữa khách và Admin trong một khiếu nại, kèm minh chứng bổ sung (ảnh/video).</summary>
    [SevagoTable(WashGoDbProperties.IncidentMessages, Schema = WashGoDbProperties.Schema)]
    public class IncidentMessage : Entity<Guid>
    {
        public IncidentMessage() { }
        public IncidentMessage(Guid id) : base(id) { }

        public Guid IncidentId { get; set; }
        public Guid SenderId { get; set; }
        /// <summary>Customer hoặc Admin.</summary>
        public string SenderRole { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = [];
        public List<string> VideoUrls { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }
}
