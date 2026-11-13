using Volo.Abp.Domain.Repositories;

namespace WashGo.Domain.Entities.Chatbot.Interfaces;

public interface IChatConversationRepository : IRepository<ChatConversation, Guid>
{
    Task LockCustomerAsync(Guid customerId);
    Task PurgeAsync(DateTime cutoff);
}
