using WashGo.Core.Domain.Common;

namespace WashGo.Domain.Entities.Chatbot;

public class ChatConversation : BaseEntity
{
    public ChatConversation() { }
    public ChatConversation(Guid id) : base(id) { }
    public Guid CustomerId { get; set; }
    public long Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public Guid? ActiveMessageId { get; set; }
    public DateTime? LeaseUntil { get; set; }
}
