using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WashGo.Domain.Entities.Chatbot;
using WashGo.Domain.Entities.Identity;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration;

public class ChatConversationEntityTypeConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        GuidKeyConfiguration.ConfigureGeneratedGuid(builder);
        builder.ToTable("chat_conversations", "washgo");
        builder.Property(x => x.Sequence).UseIdentityAlwaysColumn();
        builder.Property(x => x.Title).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.Sequence).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.Sequence });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
