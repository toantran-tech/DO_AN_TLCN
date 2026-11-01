using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Results
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? LinkType { get; set; }
        public Guid? LinkId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public long CreatedOn { get; set; }
    }
}
