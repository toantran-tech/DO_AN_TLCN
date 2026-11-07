using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Application.Contracts.Features.FeedbackContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Feedback.Interfaces;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FeedbackApplication.Services
{
    /// <summary>
    /// Khiếu nại phía Customer (UC 1.4.2–1.4.3): lọc khiếu nại của mình, xem điều kiện báo cáo của một đơn,
    /// sửa hoặc rút khiếu nại khi còn chờ xử lý, trao đổi với Admin và bổ sung minh chứng.
    /// </summary>
    public class CustomerIncidentAppService : WashGoAppService, ICustomerIncidentAppService
    {
        private const string NotFoundMessage = "Không tìm thấy khiếu nại!";
        private const string NotPendingMessage = "Khiếu nại đã được xử lý hoặc đã rút, không thể thay đổi.";

        private readonly IIncidentRepository _incidentRepo;
        private readonly IRepository<IncidentMessage, Guid> _messageRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IConfiguration _configuration;
        private readonly NotificationPublisher _notifications;

        public CustomerIncidentAppService(
            IIncidentRepository incidentRepo,
            IRepository<IncidentMessage, Guid> messageRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<User, Guid> userRepo,
            IConfiguration configuration,
            NotificationPublisher notifications)
        {
            _incidentRepo = incidentRepo;
            _messageRepo = messageRepo;
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
            _userRepo = userRepo;
            _configuration = configuration;
            _notifications = notifications;
        }

        private bool IsAdmin => CurrentUser.IsInRole("Admin");

        private static PaginatedResult<IncidentResultDto> Invalid(string message) => new()
        {
            StatusCode = System.Net.HttpStatusCode.BadRequest,
            Succeeded = false,
            Message = message
        };

        public async Task<PaginatedResult<IncidentResultDto>> FilterMineAsync(CustomerIncidentFilterQuery query)
        {
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            if (query.Statuses?.Any(x => !Enum.IsDefined(x)) == true || query.Types?.Any(x => !Enum.IsDefined(x)) == true)
                return Invalid("Trạng thái hoặc loại sự cố không hợp lệ.");
            if (query.FromDate > query.ToDate)
                return Invalid("Thời gian bắt đầu phải trước thời gian kết thúc.");
            var sort = query.Sort?.Trim().ToLowerInvariant() ?? "newest";
            if (sort is not ("newest" or "oldest" or "status"))
                return Invalid("Kiểu sắp xếp không hợp lệ (newest, oldest, status).");

            var mine = (await _incidentRepo.GetQueryableAsync()).Where(x => x.CustomerId == CurrentUser.Id);
            var hasAny = await AsyncExecuter.AnyAsync(mine);
            var queryable = mine;
            if (query.Statuses is { Count: > 0 } statuses)
                queryable = queryable.Where(x => statuses.Contains(x.Status));
            if (query.Types is { Count: > 0 } types)
                queryable = queryable.Where(x => types.Contains(x.Type));
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.OrderCode.ToLower().Contains(kw));
            }
            if (query.FromDate.HasValue)
                queryable = queryable.Where(x => x.CreatedOn >= query.FromDate.Value);
            if (query.ToDate.HasValue)
                queryable = queryable.Where(x => x.CreatedOn <= query.ToDate.Value);

            var ordered = sort switch
            {
                "oldest" => queryable.OrderBy(x => x.CreatedOn),
                "status" => queryable.OrderBy(x => x.Status == IncidentStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedOn),
                _ => queryable.OrderByDescending(x => x.CreatedOn)
            };
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(ordered.Skip((page - 1) * take).Take(take));
            return Success(items.Select(IncidentAppService.ToDto).ToList(), total, page, take,
                total > 0 ? null : hasAny ? "Không có khiếu nại phù hợp." : "Bạn chưa có khiếu nại nào.");
        }

        public async Task<BaseResponse<IncidentEligibilityDto>> GetEligibilityAsync(Guid orderId)
        {
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || order.CustomerId != CurrentUser.Id)
                return NotFound<IncidentEligibilityDto>("Không tìm thấy đơn hàng của khách hàng.");

            var incidents = await _incidentRepo.GetListAsync(x => x.OrderId == orderId);
            var completedAt = await AsyncExecuter.MaxAsync((await _timelineRepo.GetQueryableAsync())
                .Where(x => x.WashOrderId == orderId && x.Status == WashOrderStatus.Completed)
                .Select(x => (long?)x.Timestamp));
            var result = new IncidentEligibilityDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CompletedAt = completedAt.HasValue ? DateTimeOffset.FromUnixTimeSeconds(completedAt.Value).UtcDateTime : null,
                ReportDeadline = completedAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(completedAt.Value + Incident.ReportWindowDays * 86400L).UtcDateTime : null,
                PendingIncidentId = incidents.FirstOrDefault(x => x.Status == IncidentStatus.Pending)?.Id,
                IncidentIds = incidents.OrderByDescending(x => x.CreatedOn).Select(x => x.Id).ToList()
            };
            // Same rules and order as creating an incident (UC 1.4.2).
            result.Reason =
                order.Status != WashOrderStatus.Completed || !completedAt.HasValue ? "Chỉ báo cáo sự cố cho đơn hàng đã hoàn thành."
                : DateTime.UtcNow > result.ReportDeadline ? "Đã quá thời hạn báo cáo sự cố."
                : result.PendingIncidentId.HasValue ? "Đơn hàng đang có khiếu nại chờ xử lý."
                : null;
            result.CanReport = result.Reason == null;
            return Success(result);
        }

        public async Task<BaseResponse<IncidentResultDto>> GetByRequestIdAsync(string clientRequestId)
        {
            var requestId = clientRequestId?.Trim();
            var incident = string.IsNullOrEmpty(requestId) ? null
                : await _incidentRepo.FirstOrDefaultAsync(x => x.CustomerId == CurrentUser.Id && x.ClientRequestId == requestId);
            return incident == null
                ? NotFound<IncidentResultDto>("Chưa có khiếu nại nào với mã yêu cầu này.")
                : Success(IncidentAppService.ToDto(incident));
        }

        public async Task<BaseResponse<IncidentResultDto>> UpdateAsync(Guid id, UpdateIncidentCommand command)
        {
            var customerId = CurrentUser.Id!.Value;
            if (!Enum.IsDefined(command.Type) || !Enum.IsDefined(command.Severity))
                return BadRequest<IncidentResultDto>("Vui lòng chọn loại sự cố và mức độ hợp lệ.");
            var description = command.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description) || description.Length > 2000)
                return BadRequest<IncidentResultDto>("Mô tả sự cố phải có từ 1 đến 2000 ký tự.");
            var evidenceError = ValidateEvidence(command.ImageUrls, command.VideoUrls, customerId, true, out var images, out var videos);
            if (evidenceError != null)
                return BadRequest<IncidentResultDto>(evidenceError);

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null || incident.CustomerId != customerId)
                return NotFound<IncidentResultDto>(NotFoundMessage);
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>(NotPendingMessage);

            incident.Type = command.Type;
            incident.Severity = command.Severity;
            incident.Description = description;
            incident.ImageUrls = images;
            incident.VideoUrls = videos;
            incident.CustomerUpdatedAt = DateTime.UtcNow;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await _notifications.SendToAdminsAsync(NotificationType.IncidentMessage, "Khách cập nhật khiếu nại",
                $"Khách {incident.CustomerName} đã cập nhật khiếu nại về đơn #{incident.OrderCode}.", "incident", incident.Id,
                NotificationPublisher.Message("incidentUpdated.admin", ("orderCode", incident.OrderCode), ("customerName", incident.CustomerName)));
            await uow.CompleteAsync();
            return Success(IncidentAppService.ToDto(incident), "Đã cập nhật khiếu nại.");
        }

        public async Task<BaseResponse<IncidentResultDto>> WithdrawAsync(Guid id, WithdrawIncidentCommand command)
        {
            var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
            if (reason?.Length > 500)
                return BadRequest<IncidentResultDto>("Lý do rút khiếu nại tối đa 500 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null || incident.CustomerId != CurrentUser.Id)
                return NotFound<IncidentResultDto>(NotFoundMessage);
            if (incident.Status == IncidentStatus.Withdrawn)
            {
                await uow.CompleteAsync();
                return Success(IncidentAppService.ToDto(incident), "Khiếu nại đã được rút trước đó.");
            }
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>(NotPendingMessage);

            incident.Status = IncidentStatus.Withdrawn;
            incident.WithdrawnAt = DateTime.UtcNow;
            incident.WithdrawReason = reason;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await _notifications.SendToAdminsAsync(NotificationType.IncidentMessage, "Khách rút khiếu nại",
                $"Khách {incident.CustomerName} đã rút khiếu nại về đơn #{incident.OrderCode}.", "incident", incident.Id,
                NotificationPublisher.Message("incidentWithdrawn.admin", ("orderCode", incident.OrderCode), ("customerName", incident.CustomerName)));
            await uow.CompleteAsync();
            return Success(IncidentAppService.ToDto(incident), "Đã rút khiếu nại.");
        }

        public async Task<BaseResponse<List<IncidentMessageDto>>> GetMessagesAsync(Guid id)
        {
            var incident = await _incidentRepo.FindAsync(id);
            if (incident == null || (!IsAdmin && incident.CustomerId != CurrentUser.Id))
                return NotFound<List<IncidentMessageDto>>(NotFoundMessage);
            var messages = await AsyncExecuter.ToListAsync((await _messageRepo.GetQueryableAsync())
                .Where(x => x.IncidentId == id).OrderBy(x => x.CreatedAt));
            return Success(messages.Select(ToDto).ToList(), messages.Count == 0 ? "Chưa có trao đổi nào." : null);
        }

        public async Task<BaseResponse<IncidentMessageDto>> AddMessageAsync(Guid id, AddIncidentMessageCommand command)
        {
            var userId = CurrentUser.Id!.Value;
            var content = command.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
                return BadRequest<IncidentMessageDto>("Nội dung trao đổi phải có từ 1 đến 2000 ký tự.");
            var evidenceError = ValidateEvidence(command.ImageUrls, command.VideoUrls, userId, false, out var images, out var videos);
            if (evidenceError != null)
                return BadRequest<IncidentMessageDto>(evidenceError);

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null || (!IsAdmin && incident.CustomerId != userId))
                return NotFound<IncidentMessageDto>(NotFoundMessage);
            // The customer adds evidence while the incident is open; the Admin can still explain the outcome later.
            if (!IsAdmin && incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentMessageDto>(NotPendingMessage);

            var message = new IncidentMessage
            {
                IncidentId = incident.Id,
                SenderId = userId,
                SenderRole = IsAdmin ? "Admin" : "Customer",
                SenderName = (await _userRepo.FindAsync(userId))?.FullName ?? (IsAdmin ? "Admin" : incident.CustomerName),
                Content = content,
                ImageUrls = images,
                VideoUrls = videos,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.InsertAsync(message, autoSave: true);
            if (IsAdmin)
                await _notifications.SendAsync([incident.CustomerId], NotificationType.IncidentMessage, "Phản hồi khiếu nại",
                    $"WashGo đã phản hồi khiếu nại của bạn về đơn #{incident.OrderCode}.", "incident", incident.Id,
                    NotificationPublisher.Message("incidentMessage.customer", ("orderCode", incident.OrderCode)));
            else
                await _notifications.SendToAdminsAsync(NotificationType.IncidentMessage, "Khách bổ sung khiếu nại",
                    $"Khách {incident.CustomerName} gửi trao đổi/bổ sung minh chứng cho khiếu nại đơn #{incident.OrderCode}.",
                    "incident", incident.Id,
                    NotificationPublisher.Message("incidentMessage.admin", ("orderCode", incident.OrderCode), ("customerName", incident.CustomerName)));
            await uow.CompleteAsync();
            return Created(ToDto(message), "Đã gửi trao đổi.");
        }

        /// <summary>Own uploads only; an incident itself needs at least one photo or video.</summary>
        private string? ValidateEvidence(List<string>? imageUrls, List<string>? videoUrls, Guid ownerId, bool required,
            out List<string> images, out List<string> videos)
        {
            images = [];
            var videoError = UploadStorage.ValidateOwnedVideos(_configuration, videoUrls, ownerId, out videos);
            if (videoError != null)
                return videoError;
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, imageUrls, ownerId, required && videos.Count == 0 ? 1 : 0, out images);
            if (imageError != null)
                return required && videos.Count == 0 && images.Count == 0 ? "Vui lòng upload ít nhất 1 ảnh hoặc video minh chứng." : imageError;
            return null;
        }

        private IncidentMessageDto ToDto(IncidentMessage x) => new()
        {
            Id = x.Id,
            IncidentId = x.IncidentId,
            SenderRole = x.SenderRole,
            SenderName = x.SenderName,
            IsMine = x.SenderId == CurrentUser.Id,
            Content = x.Content,
            ImageUrls = new List<string>(x.ImageUrls),
            VideoUrls = new List<string>(x.VideoUrls),
            CreatedAt = x.CreatedAt
        };
    }
}
