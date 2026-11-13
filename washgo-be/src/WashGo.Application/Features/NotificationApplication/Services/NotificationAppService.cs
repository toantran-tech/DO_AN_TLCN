using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.NotificationContracts.Queries;
using WashGo.Application.Contracts.Features.NotificationContracts.Results;
using WashGo.Application.Contracts.Features.NotificationContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Notification;
using WashGo.Domain.Entities.Notification.Interfaces;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.NotificationApplication.Services
{
    /// <summary>Hộp thông báo của người dùng đang đăng nhập; không ai xem/sửa được thông báo của người khác.</summary>
    public class NotificationAppService : WashGoAppService, INotificationAppService
    {
        private const int MaxKeywordLength = 100;

        private readonly INotificationRepository _notificationRepo;
        private readonly IRealtimeNotifier _realtime;

        public NotificationAppService(INotificationRepository notificationRepo, IRealtimeNotifier realtime)
        {
            _notificationRepo = notificationRepo;
            _realtime = realtime;
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
            return Success(items.Select(ToCurrentUserDto).ToList(), total, page, take,
                total == 0 ? "Bạn chưa có thông báo nào." : null);
        }

        public async Task<BaseResponse<NotificationPageDto>> FilterAsync(NotificationFilterQuery query)
        {
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var keyword = query.Keyword?.Trim();
            if (keyword?.Length > MaxKeywordLength)
                return BadRequest<NotificationPageDto>($"Từ khóa tối đa {MaxKeywordLength} ký tự.");
            var groups = query.Groups?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList() ?? [];
            if (groups.Any(x => !NotificationGroups.All.Contains(x)))
                return BadRequest<NotificationPageDto>("Nhóm thông báo không hợp lệ.");
            if (query.Types?.Any(x => !Enum.IsDefined(x)) == true)
                return BadRequest<NotificationPageDto>("Loại thông báo không hợp lệ.");
            if (query.FromDate > query.ToDate)
                return BadRequest<NotificationPageDto>("Thời gian bắt đầu phải trước thời gian kết thúc.");
            var sort = query.Sort?.Trim().ToLowerInvariant() ?? "newest";
            if (sort is not ("newest" or "oldest" or "unreadfirst"))
                return BadRequest<NotificationPageDto>("Kiểu sắp xếp không hợp lệ (newest, oldest, unreadFirst).");

            var mine = (await _notificationRepo.GetQueryableAsync()).Where(x => x.UserId == CurrentUser.Id);
            var filtered = mine;
            if (!string.IsNullOrEmpty(keyword))
            {
                var lowered = keyword.ToLower();
                var customer = CurrentUser.IsInRole("Customer");
                // Legacy order content may contain a warehouse address. Never search withheld text/count it for Customer.
                filtered = filtered.Where(x => x.Title.ToLower().Contains(lowered) ||
                    ((!customer || x.LinkType != "order") && x.Content.ToLower().Contains(lowered)));
            }
            if (groups.Count > 0)
            {
                var groupTypes = groups.SelectMany(NotificationGroups.TypesOf).Distinct().ToList();
                filtered = filtered.Where(x => groupTypes.Contains(x.Type));
            }
            if (query.Types is { Count: > 0 } types)
                filtered = filtered.Where(x => types.Contains(x.Type));
            if (query.UnreadOnly == true)
                filtered = filtered.Where(x => !x.IsRead);
            if (query.FromDate.HasValue)
                filtered = filtered.Where(x => x.CreatedOn >= query.FromDate.Value);
            if (query.ToDate.HasValue)
                filtered = filtered.Where(x => x.CreatedOn <= query.ToDate.Value);

            var ordered = sort switch
            {
                "oldest" => filtered.OrderBy(x => x.CreatedOn).ThenBy(x => x.Id),
                "unreadfirst" => filtered.OrderBy(x => x.IsRead).ThenByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id),
                _ => filtered.OrderByDescending(x => x.CreatedOn).ThenByDescending(x => x.Id)
            };
            var total = await AsyncExecuter.CountAsync(filtered);
            var items = await AsyncExecuter.ToListAsync(ordered.Skip((page - 1) * take).Take(take));

            // Counts cover the whole inbox so the group tabs keep their numbers while a filter is applied.
            var byType = await AsyncExecuter.ToListAsync(mine.GroupBy(x => x.Type)
                .Select(g => new { Type = g.Key, Total = g.Count(), Unread = g.Count(x => !x.IsRead) }));
            var counts = NotificationGroups.All.Select(group => new NotificationGroupCountDto
            {
                Group = group,
                Total = byType.Where(x => NotificationGroups.Of(x.Type) == group && group != NotificationGroups.Promotion).Sum(x => x.Total),
                Unread = byType.Where(x => NotificationGroups.Of(x.Type) == group && group != NotificationGroups.Promotion).Sum(x => x.Unread)
            }).ToList();

            var result = new NotificationPageDto
            {
                Items = items.Select(ToCurrentUserDto).ToList(),
                Total = total,
                Page = page,
                Take = take,
                UnreadTotal = byType.Sum(x => x.Unread),
                Counts = counts
            };
            return Success(result, total == 0
                ? (byType.Count == 0 ? "Bạn chưa có thông báo nào." : "Không có thông báo phù hợp.")
                : null);
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
                await PushStateAsync("read", [id]);
            }
            return Success(ToCurrentUserDto(notification));
        }

        public async Task<BaseResponse<int>> MarkAllReadAsync()
        {
            var changed = await _notificationRepo.MarkAllReadAsync(CurrentUser.Id!.Value, UtcNowMicroseconds());
            if (changed > 0)
                await PushStateAsync("readAll", []);
            return Success(changed, "Đã đánh dấu tất cả là đã đọc.");
        }

        public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
        {
            var notification = await _notificationRepo.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUser.Id);
            if (notification == null)
                return NotFound<bool>("Không tìm thấy thông báo!");
            await _notificationRepo.DeleteAsync(notification, autoSave: true);
            await PushStateAsync("deleted", [id]);
            return Success(true, "Đã xóa thông báo.");
        }

        public async Task<BaseResponse<int>> DeleteAllAsync()
        {
            var deleted = await _notificationRepo.DeleteAllAsync(CurrentUser.Id!.Value);
            if (deleted > 0)
                await PushStateAsync("deletedAll", []);
            return Success(deleted, "Đã xóa tất cả thông báo.");
        }

        /// <summary>Tells the user's other devices to update their list and badge; sent after the change is committed.</summary>
        private async Task PushStateAsync(string action, List<Guid> ids)
        {
            var userId = CurrentUser.Id!.Value;
            // Counted inside the unit of work (the change is already saved); only the push waits for the commit.
            var state = new NotificationStateDto
            {
                Action = action,
                Ids = ids,
                UnreadCount = await _notificationRepo.CountAsync(x => x.UserId == userId && !x.IsRead),
                At = DateTime.UtcNow
            };
            async Task SendAsync()
            {
                try
                {
                    await _realtime.SendStateAsync(userId, state);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Không đẩy được trạng thái thông báo {Action} tới {UserId}.", action, userId);
                }
            }
            if (UnitOfWorkManager.Current is { } uow) uow.OnCompleted(SendAsync);
            else await SendAsync();
        }

        private NotificationDto ToCurrentUserDto(Notification row)
        {
            var dto = ToDto(row);
            if (CurrentUser.IsInRole("Admin")) return dto;
            return OrderNotificationPrivacy.Apply(dto, customer: CurrentUser.IsInRole("Customer"));
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
            CreatedOn = x.CreatedOn,
            Group = NotificationGroups.Of(x.Type),
            MessageKey = x.MessageKey ?? NotificationGroups.DefaultKey(x.Type),
            MessageArgs = ReadArgs(x.MessageArgs)
        };

        private static Dictionary<string, string> ReadArgs(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []; }
            catch (JsonException) { return []; }
        }
    }
}
