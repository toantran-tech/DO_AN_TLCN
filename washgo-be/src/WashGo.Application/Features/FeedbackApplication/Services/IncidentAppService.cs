using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Features.AuditApplication.Services;
using System.Net;
using WashGo.Application.Contracts.Features.FeedbackContracts.Commands;
using WashGo.Application.Contracts.Features.FeedbackContracts.Queries;
using WashGo.Application.Contracts.Features.FeedbackContracts.Results;
using WashGo.Application.Contracts.Features.FeedbackContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Feedback.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.FeedbackApplication.Services
{
    public class IncidentAppService : WashGoAppService, IIncidentAppService
    {
        private const decimal MaxItemValue = 100_000_000m;

        private readonly IIncidentRepository _incidentRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IConfiguration _configuration;
        private readonly NotificationPublisher _notifications;
        private readonly SystemSettingProvider _settings;

        public IncidentAppService(
            IIncidentRepository incidentRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IConfiguration configuration,
            NotificationPublisher notifications,
            SystemSettingProvider settings)
        {
            _incidentRepo = incidentRepo;
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
            _merchantRepo = merchantRepo;
            _configuration = configuration;
            _notifications = notifications;
            _settings = settings;
        }

        public async Task<BaseResponse<IncidentResultDto>> CreateAsync(CreateIncidentCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<IncidentResultDto>("Không xác định được tài khoản khách hàng.");
            var customerId = CurrentUser.Id.Value;
            var requestId = string.IsNullOrWhiteSpace(command.ClientRequestId) ? null : command.ClientRequestId.Trim();
            if (requestId?.Length > 64)
                return BadRequest<IncidentResultDto>("Mã yêu cầu tối đa 64 ký tự.");
            // A retried submit with the same request id returns the incident created the first time.
            if (requestId != null &&
                await _incidentRepo.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.ClientRequestId == requestId) is { } existing)
                return Success(ToDto(existing), "Yêu cầu khiếu nại này đã được ghi nhận trước đó.");
            if (!Enum.IsDefined(command.Type) || !Enum.IsDefined(command.Severity))
                return BadRequest<IncidentResultDto>("Vui lòng chọn loại sự cố và mức độ hợp lệ.");
            if (command.ItemValue < 0 || command.ItemValue > MaxItemValue || decimal.Round(command.ItemValue, 2) != command.ItemValue)
                return BadRequest<IncidentResultDto>("Giá trị món đồ từ 0 đến 100.000.000đ, tối đa hai chữ số thập phân.");
            var description = command.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description) || description.Length > 2000)
                return BadRequest<IncidentResultDto>("Mô tả sự cố phải có từ 1 đến 2000 ký tự.");
            // Evidence: at least one photo or one video.
            var videoError = UploadStorage.ValidateOwnedVideos(_configuration, command.VideoUrls, customerId, out var videos);
            if (videoError != null)
                return BadRequest<IncidentResultDto>(videoError);
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, customerId, command.Type is IncidentType.Damaged or IncidentType.Lost ? 1 : videos.Count > 0 ? 0 : 1, out var images);
            if (imageError != null)
                return BadRequest<IncidentResultDto>(videos.Count == 0 && images.Count == 0
                    ? "Vui lòng upload ít nhất 1 ảnh hoặc video minh chứng." : imageError);

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orderRepo.GetForUpdateAsync(command.OrderId);
            if (order == null || order.CustomerId != customerId)
                return NotFound<IncidentResultDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status != WashOrderStatus.Completed)
                return BadRequest<IncidentResultDto>("Chỉ báo cáo sự cố cho đơn hàng đã hoàn thành.");

            // "Nhận đồ" is the moment the order became Completed in its history.
            var completedAt = await AsyncExecuter.MaxAsync((await _timelineRepo.GetQueryableAsync())
                .Where(x => x.WashOrderId == order.Id && x.Status == WashOrderStatus.Completed)
                .Select(x => (long?)x.Timestamp));
            if (!completedAt.HasValue ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - completedAt.Value > Incident.ReportWindowDays * 86400L)
                return BadRequest<IncidentResultDto>("Đã quá thời hạn báo cáo sự cố.");
            if (await _incidentRepo.AnyAsync(x => x.OrderId == order.Id && x.Status == IncidentStatus.Pending))
                return BadRequest<IncidentResultDto>("Đơn hàng đang có khiếu nại chờ xử lý.");

            var incident = new Incident
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerId = customerId,
                CustomerName = order.CustomerName,
                MerchantId = order.MerchantId,
                // Older orders never stored the Merchant name, so fall back to the Merchant record.
                MerchantName = !string.IsNullOrWhiteSpace(order.MerchantName) || !order.MerchantId.HasValue
                    ? order.MerchantName : (await _merchantRepo.FindAsync(order.MerchantId.Value))?.BusinessName,
                Type = command.Type,
                Severity = command.Severity,
                Description = description, DeclaredItemValue = command.ItemValue, ItemValue = command.ItemValue,
                ImageUrls = images,
                VideoUrls = videos,
                ClientRequestId = requestId,
                Status = IncidentStatus.Pending
            };
            await _incidentRepo.InsertAsync(incident, autoSave: true);
            await _notifications.IncidentCreatedAsync(incident);
            await Audit.LogAsync(new AuditEntry("Incident", "Create", "Incident", incident.Id.ToString(), incident.OrderCode));
            await uow.CompleteAsync();
            return Created(ToDto(incident),
                "Báo cáo của bạn đã được gửi. Chúng tôi sẽ xử lý trong thời gian sớm nhất.");
        }

        public async Task<PaginatedResult<IncidentResultDto>> GetMineAsync(int page, int take)
        {
            page = Math.Max(1, page);
            take = Math.Clamp(take, 1, 100);
            var queryable = (await _incidentRepo.GetQueryableAsync()).Where(x => x.CustomerId == CurrentUser.Id);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable.OrderByDescending(x => x.CreatedOn)
                .Skip((page - 1) * take).Take(take));
            return Success(items.Select(ToDto).ToList(), total, page, take,
                total == 0 ? "Bạn chưa có khiếu nại nào." : null);
        }

        public async Task<PaginatedResult<IncidentResultDto>> GetListAsync(IncidentFilterQuery query)
        {
            if (query.DateRangeError() != null || query.CompensationStatus.HasValue && !Enum.IsDefined(query.CompensationStatus.Value) || query.Keyword?.Length > 200 || query.Status.HasValue && !Enum.IsDefined(query.Status.Value) || query.Type.HasValue && !Enum.IsDefined(query.Type.Value) || query.Severity.HasValue && !Enum.IsDefined(query.Severity.Value))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = query.DateRangeError() ?? "Bộ lọc không hợp lệ." };
            var queryable = (await _incidentRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            if (query.CompensationStatus.HasValue) queryable = queryable.Where(x => x.CompensationStatus == query.CompensationStatus.Value);
            if (query.OrderId.HasValue) queryable = queryable.Where(x => x.OrderId == query.OrderId.Value);
            if (query.CustomerId.HasValue) queryable = queryable.Where(x => x.CustomerId == query.CustomerId.Value);
            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);
            if (query.Severity.HasValue)
                queryable = queryable.Where(x => x.Severity == query.Severity.Value);
            if (query.Type.HasValue)
                queryable = queryable.Where(x => x.Type == query.Type.Value);
            if (query.MerchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == query.MerchantId.Value);
            if (query.FromDate > 0)
                queryable = queryable.Where(x => x.CreatedOn >= query.FromDate);
            if (query.ToDate > 0)
                queryable = queryable.Where(x => x.CreatedOn <= query.ToDate);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.OrderCode.ToLower().Contains(kw) ||
                    x.CustomerName.ToLower().Contains(kw) ||
                    (x.MerchantName != null && x.MerchantName.ToLower().Contains(kw)));
            }

            IOrderedQueryable<Incident>? ordered = null;
            foreach (var option in query.SortBy ?? [])
            {
                var field = option?.Field?.Trim().ToLowerInvariant();
                if (field is not ("createdon" or "createdat" or "status" or "severity" or "compensationamount" or "id"))
                    return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "SortBy chỉ hỗ trợ CreatedOn, Status, Severity, CompensationAmount, Id." };
                void Add<TKey>(Expression<Func<Incident, TKey>> key) => ordered = ordered == null
                    ? option!.IsDescending ? queryable.OrderByDescending(key) : queryable.OrderBy(key)
                    : option!.IsDescending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
                switch (field)
                {
                    case "createdon": case "createdat": Add(x => x.CreatedOn); break;
                    case "status": Add(x => x.Status); break;
                    case "severity": Add(x => x.Severity); break;
                    case "compensationamount": Add(x => x.CompensationAmount); break;
                    case "id": Add(x => x.Id); break;
                }
            }
            ordered ??= queryable.OrderBy(x => x.Status == IncidentStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedOn);
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(ordered.ThenBy(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            var dtos = items.Select(ToDto).ToList();
            await LazyServiceProvider.LazyGetRequiredService<IncidentContactEnricher>().FillAsync(dtos);
            return Success(dtos, total, page, take,
                total == 0 ? "Chưa có khiếu nại nào." : null);
        }

        public async Task<BaseResponse<IncidentResultDto>> GetByIdAsync(Guid id)
        {
            var incident = await _incidentRepo.FindAsync(id);
            if (incident == null || (!CurrentUser.IsInRole("Admin") && incident.CustomerId != CurrentUser.Id))
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            var dto = ToDto(incident);
            await LazyServiceProvider.LazyGetRequiredService<IncidentContactEnricher>().FillAsync([dto]);
            return Success(dto);
        }
        public async Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeSummaryDto>> GetSummaryAsync()
        {
            var groups = await AsyncExecuter.ToListAsync((await _incidentRepo.GetQueryableAsync()).Where(x => !x.IsDeleted)
                .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
            return Success(new WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeSummaryDto { Total = groups.Sum(x => x.Count), Counts = groups.ToDictionary(x => x.Status.ToString(), x => x.Count), GeneratedAt = DateTime.UtcNow });
        }

        public async Task<BaseResponse<IncidentResultDto>> ResolveAsync(Guid id, ResolveIncidentCommand command)
        {
            var note = command.ResolutionNote?.Trim();
            if (string.IsNullOrWhiteSpace(note) || note.Length > 2000)
                return BadRequest<IncidentResultDto>("Vui lòng nhập kết quả xử lý (tối đa 2000 ký tự).");
            if (!Enum.IsDefined(command.CompensationLevel) ||
                (command.ResponsibleParty.HasValue && !Enum.IsDefined(command.ResponsibleParty.Value)))
                return BadRequest<IncidentResultDto>("Mức bồi thường hoặc bên chịu trách nhiệm không hợp lệ.");
            var compensating = command.CompensationLevel != CompensationLevel.None;
            if (compensating && (command.ItemValue <= 0 || command.ItemValue > MaxItemValue || decimal.Round(command.ItemValue, 2) != command.ItemValue))
                return BadRequest<IncidentResultDto>("Giá trị đồ phải lớn hơn 0 và không quá 100.000.000đ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<IncidentResultDto>(command.OperationId, CurrentUser.Id!.Value, "Incident", "Resolve", id.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync("system-settings");
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại đã được xử lý trước đó.");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != incident.Revision.ToString()) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Khiếu nại hoặc trao đổi/minh chứng đã thay đổi.");
            var settings = await _settings.GetAsync();
            var settingsVersion = settings.UpdatedAt.HasValue ? (settings.UpdatedAt.Value.Ticks / 10).ToString() : "defaults-v1";
            if (string.IsNullOrWhiteSpace(command.ExpectedSettingsVersion)) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.BadRequest, "SettingsVersionRequired", "Lấy expectedSettingsVersion từ GET /system-setting.");
            if (settingsVersion != command.ExpectedSettingsVersion) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.Conflict, "SettingsChanged", "Mức bồi thường đã thay đổi; hãy xem lại.");

            incident.Status = IncidentStatus.Resolved;
            incident.ResolutionNote = note;
            incident.ResponsibleParty = command.ResponsibleParty;
            incident.CompensationLevel = command.CompensationLevel;
            incident.ItemValue = compensating ? command.ItemValue : 0m;
            // The amount is computed from the configured percentage (UC 3.9.4), never taken from the client.
            var percent = settings.CompensationPercent(command.CompensationLevel);
            incident.CompensationPercentApplied = percent;
            incident.CompensationSettingsVersion = settingsVersion;
            incident.Revision++;
            incident.CompensationAmount = compensating ? Incident.CalculateCompensation(percent, command.ItemValue) : 0m;
            incident.CompensationStatus = incident.CompensationAmount > 0 ? CompensationStatus.Pending : CompensationStatus.None;
            incident.ReviewedBy = CurrentUser.Id;
            incident.ReviewedAt = DateTime.UtcNow;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await _notifications.IncidentProcessedAsync(incident);
            await Audit.LogAsync(new AuditEntry("Incident", "Resolve", "Incident", id.ToString(), incident.OrderCode) { Details = new { percent, settingsVersion, incident.CompensationAmount } });
            var response = Success(ToDto(incident), incident.CompensationAmount > 0
                ? $"Đã xử lý khiếu nại; tạo yêu cầu bồi thường {incident.CompensationAmount:N0}đ cho khách hàng."
                : "Đã xử lý khiếu nại.");
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Incident", "Resolve", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<IncidentResultDto>> RejectAsync(Guid id, RejectIncidentCommand command)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
                return BadRequest<IncidentResultDto>("Vui lòng nhập lý do từ chối (tối đa 2000 ký tự).");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<IncidentResultDto>(command.OperationId, CurrentUser.Id!.Value, "Incident", "Reject", id.ToString(), command) is { } replay) return replay;
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại đã được xử lý trước đó.");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != incident.Revision.ToString()) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Khiếu nại đã thay đổi.");

            incident.Status = IncidentStatus.Rejected;
            incident.ResolutionNote = reason;
            incident.ReviewedBy = CurrentUser.Id;
            incident.ReviewedAt = DateTime.UtcNow;
            incident.Revision++;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await _notifications.IncidentProcessedAsync(incident);
            var response = Success(ToDto(incident), "Đã từ chối khiếu nại.");
            await Audit.LogAsync(new AuditEntry("Incident", "Reject", "Incident", id.ToString(), incident.OrderCode) { Reason = reason });
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Incident", "Reject", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<IncidentResultDto>> ConfirmCompensationAsync(Guid id, ConfirmCompensationCommand command)
        {
            var proofError = UploadStorage.ValidateOwnedImages(_configuration, command.ProofImageUrls, CurrentUser.Id!.Value, 0, out var proofs);
            if (proofError != null || proofs.Count > 3) return BadRequest<IncidentResultDto>(proofError ?? "Chỉ được đính kèm tối đa 3 ảnh biên lai.");
            var reference = command.Reference?.Trim();
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
                return BadRequest<IncidentResultDto>("Vui lòng nhập mã tham chiếu chuyển khoản (tối đa 120 ký tự).");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<IncidentResultDto>(command.OperationId, CurrentUser.Id!.Value, "Incident", "ConfirmCompensation", id.ToString(), command) is { } replay) return replay;
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.CompensationStatus == CompensationStatus.Paid)
            {
                if (incident.CompensationReference != reference || !incident.CompensationProofImageUrls.SequenceEqual(proofs)) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.Conflict, "PaymentReferenceChanged", "Khoản bồi thường đã trả với mã tham chiếu khác.");
                var old = Success(ToDto(incident), "Bồi thường đã được ghi nhận trước đó.");
                await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Incident", "ConfirmCompensation", id.ToString(), command, old);
                await uow.CompleteAsync(); return old;
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != incident.Revision.ToString()) return AdministrativeWriteGuard.Error<IncidentResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Khiếu nại đã thay đổi.");
            if (incident.Status != IncidentStatus.Resolved || incident.CompensationStatus != CompensationStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại không có khoản bồi thường chờ chi trả.");

            incident.CompensationStatus = CompensationStatus.Paid;
            incident.CompensationReference = reference;
            incident.CompensationPaidAt = DateTime.UtcNow;
            incident.CompensationPaidBy = CurrentUser.Id; incident.CompensationProofImageUrls = proofs;
            incident.Revision++;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            var response = Success(ToDto(incident), "Đã ghi nhận chi trả bồi thường.");
            await Audit.LogAsync(new AuditEntry("Incident", "ConfirmCompensation", "Incident", id.ToString(), incident.OrderCode) { Details = new { reference, incident.CompensationAmount } });
            await _notifications.SendAsync([incident.CustomerId], NotificationType.IncidentProcessed, "Đã chi trả bồi thường", $"Đã trả {incident.CompensationAmount:N0}đ cho khiếu nại đơn #{incident.OrderCode}; mã {reference}.", "incident", incident.Id);
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "Incident", "ConfirmCompensation", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public static IncidentResultDto ToDto(Incident x) => new()
        {
            Version = x.Revision.ToString(), CompensationPercentApplied = x.CompensationPercentApplied, CompensationSettingsVersion = x.CompensationSettingsVersion,
            ReviewedBy = x.ReviewedBy,
            Id = x.Id,
            OrderId = x.OrderId,
            OrderCode = x.OrderCode,
            CustomerId = x.CustomerId,
            CustomerName = x.CustomerName,
            MerchantId = x.MerchantId,
            MerchantName = x.MerchantName,
            Type = x.Type,
            Severity = x.Severity,
            Description = x.Description,
            ImageUrls = new List<string>(x.ImageUrls),
            VideoUrls = new List<string>(x.VideoUrls),
            Status = x.Status,
            ResolutionNote = x.ResolutionNote,
            ResponsibleParty = x.ResponsibleParty,
            CompensationLevel = x.CompensationLevel,
            ItemValue = x.ItemValue, DeclaredItemValue = x.DeclaredItemValue, CompensationPaidBy = x.CompensationPaidBy,
            CompensationProofImageUrls = new List<string>(x.CompensationProofImageUrls),
            CompensationAmount = x.CompensationAmount,
            CompensationStatus = x.CompensationStatus,
            CompensationReference = x.CompensationReference,
            CompensationPaidAt = x.CompensationPaidAt,
            ReviewedAt = x.ReviewedAt,
            CreatedOn = x.CreatedOn,
            ClientRequestId = x.ClientRequestId,
            CustomerUpdatedAt = x.CustomerUpdatedAt,
            WithdrawnAt = x.WithdrawnAt,
            WithdrawReason = x.WithdrawReason
        };
    }
}
