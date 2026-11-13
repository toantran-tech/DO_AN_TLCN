using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using WashGo.Domain.Entities.Chatbot;
using WashGo.Domain.Entities.Chatbot.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories;

public class ChatConversationRepository(IDbContextProvider<WashGoDbContext> provider)
    : EfCoreRepository<WashGoDbContext, ChatConversation, Guid>(provider), IChatConversationRepository
{
    public async Task LockCustomerAsync(Guid customerId)
    {
        var db = await GetDbContextAsync();
        var key = $"chatbot:{customerId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
    }

    public async Task PurgeAsync(DateTime cutoff)
    {
        var db = await GetDbContextAsync();
        // Remove replies first. No navigation FK to reply: both rows expire independently.
        // Keep tombstoned message rows until expiry: deleting a conversation must not reset daily usage.
        await db.ChatMessages.Where(x => x.CreatedAt < cutoff)
            .ExecuteDeleteAsync();
        await db.ChatConversations.Where(c => (c.IsDeleted || c.LastMessageAt < cutoff) &&
            !db.ChatMessages.Any(m => m.ConversationId == c.Id)).ExecuteDeleteAsync();
    }
}
