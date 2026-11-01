using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Notification.Interfaces;

namespace WashGo.Application.Features.NotificationApplication.Services
{
    /// <summary>Hộp thông báo của người dùng đang đăng nhập; không ai xem/sửa được thông báo của người khác.</summary>
    public class NotificationAppService : WashGoAppService, INotificationAppService
    {
        private readonly INotificationRepository _notificationRepo;

        public NotificationAppService(INotificationRepository notificationRepo)
        {
            _notificationRepo = notificationRepo;
        }

        // PostgreSQL keeps microseconds; trimming .NET's extra precision keeps responses equal to later reads.
        private static DateTime UtcNowMicroseconds()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        }

        public async Task<PaginatedResult<NotificationDto>> GetMineAsync(int page, int take, bool unreadOnly)
        {
            page = Math.Max(1, page);
            take = Math.Clamp(take, 1, 100);
            var queryable = (await _notificationRepo.GetQueryableAsync()).Where(x => x.UserId == CurrentUser.Id);
            if (unreadOnly)
                queryable = queryable.Where(x => !x.IsRead);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable
                .OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            return Success(items.Select(ToDto).ToList(), total, page, take,
                total == 0 ? "Bạn chưa có thông báo nào." : null);
        }

        public async Task<BaseResponse<int>> GetUnreadCountAsync() =>
            Success(await _notificationRepo.CountAsync(x => x.UserId == CurrentUser.Id && !x.IsRead));

        public async Task<BaseResponse<NotificationDto>> MarkReadAsync(Guid id)
        {
            var notification = await _notificationRepo.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUser.Id);
            if (notification == null)
                return NotFound<NotificationDto>("Không tìm thấy thông báo!");
            // An already-read notification keeps its original read time.
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = UtcNowMicroseconds();
                await _notificationRepo.UpdateAsync(notification, autoSave: true);
            }
            return Success(ToDto(notification));
        }

        public async Task<BaseResponse<int>> MarkAllReadAsync()
        {
            var changed = await _notificationRepo.MarkAllReadAsync(CurrentUser.Id!.Value, UtcNowMicroseconds());
            return Success(changed, "Đã đánh dấu tất cả là đã đọc.");
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
        {
            var notification = await _notificationRepo.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUser.Id);
            if (notification == null)
                return NotFound<bool>("Không tìm thấy thông báo!");
            await _notificationRepo.DeleteAsync(notification, autoSave: true);
            return Success(true, "Đã xóa thông báo.");
        }

        public async Task<BaseResponse<int>> DeleteAllAsync()
        {
            var deleted = await _notificationRepo.DeleteAllAsync(CurrentUser.Id!.Value);
            return Success(deleted, "Đã xóa tất cả thông báo.");
        }

        public static NotificationDto ToDto(Notification x) => new()
        {
            Id = x.Id,
            Type = x.Type,
            Title = x.Title,
            Content = x.Content,
            LinkType = x.LinkType,
            LinkId = x.LinkId,
            IsRead = x.IsRead,
            ReadAt = x.ReadAt,
            CreatedOn = x.CreatedOn
        };
    }
}
