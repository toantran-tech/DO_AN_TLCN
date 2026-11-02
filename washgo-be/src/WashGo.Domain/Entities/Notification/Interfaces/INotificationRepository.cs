using System;
using System.Threading.Tasks;
using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.Notification.Interfaces
{
    public interface INotificationRepository : IMesRepository<Notification, Guid>
    {
        Task<int> MarkAllReadAsync(Guid userId, DateTime readAt);
        Task<int> DeleteAllAsync(Guid userId);
    }
}
