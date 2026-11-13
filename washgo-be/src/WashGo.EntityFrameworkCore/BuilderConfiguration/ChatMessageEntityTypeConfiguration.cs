using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Chatbot;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class ChatMessageEntityTypeConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
        builder.ToTable("chat_messages", "washgo");
        builder.Property(x => x.Sequence).UseIdentityAlwaysColumn();
        builder.Property(x => x.Content).HasMaxLength(12000).IsRequired();
        builder.Property(x => x.Fingerprint).HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.HasIndex(x => x.Sequence).IsUnique();
        builder.HasIndex(x => new { x.ConversationId, x.Sequence });
        builder.HasIndex(x => new { x.ConversationId, x.RequestId }).IsUnique().HasFilter("request_id IS NOT NULL");
        builder.HasIndex(x => x.ReplyToMessageId).IsUnique();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne<ChatConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
