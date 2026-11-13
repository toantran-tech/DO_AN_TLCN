using WashGo.Core.Domain.Common;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.Chatbot;

public class ChatMessage : BaseEntity
{
    public ChatMessage() { }
    public ChatMessage(Guid id) : base(id) { }
    public Guid ConversationId { get; set; }
    public long Sequence { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? RequestId { get; set; }
    public string? Fingerprint { get; set; }
    public ChatTurnStatus? Status { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? Model { get; set; }
    public int? TotalTokens { get; set; }
}
