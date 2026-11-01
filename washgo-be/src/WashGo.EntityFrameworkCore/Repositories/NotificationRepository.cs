using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.ObjectMapping;
using WashGo.Core.DataAccess.Repository;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Notification.Interfaces;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.Repositories
{
    public class NotificationRepository : MesRepository<WashGoDbContext, Notification, Guid>, INotificationRepository
    {
        public NotificationRepository(
            IDbContextProvider<WashGoDbContext> dbContextProvider,
            IObjectMapper objectMapper)
            : base(dbContextProvider, objectMapper)
        {
        }

        public async Task<int> MarkAllReadAsync(Guid userId, DateTime readAt)
        {
            var db = await GetDbContextAsync();
            return await db.Notifications
                .Where(x => x.UserId == userId && !x.IsDeleted && !x.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, readAt));
        }

        public async Task<int> DeleteAllAsync(Guid userId)
        {
            var db = await GetDbContextAsync();
            return await db.Notifications.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        }
    }
}
