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

        public IncidentAppService(
            IIncidentRepository incidentRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IConfiguration configuration)
        {
            _incidentRepo = incidentRepo;
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
            _merchantRepo = merchantRepo;
            _configuration = configuration;
        }

        public async Task<BaseResponse<IncidentResultDto>> CreateAsync(CreateIncidentCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<IncidentResultDto>("Không xác định được tài khoản khách hàng.");
            var customerId = CurrentUser.Id.Value;
            if (!Enum.IsDefined(command.Type) || !Enum.IsDefined(command.Severity))
                return BadRequest<IncidentResultDto>("Vui lòng chọn loại sự cố và mức độ hợp lệ.");
            var description = command.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description) || description.Length > 2000)
                return BadRequest<IncidentResultDto>("Mô tả sự cố phải có từ 1 đến 2000 ký tự.");
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, customerId, 1, out var images);
            if (imageError != null)
                return BadRequest<IncidentResultDto>(imageError);

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
                Description = description,
                ImageUrls = images,
                Status = IncidentStatus.Pending
            };
            await _incidentRepo.InsertAsync(incident, autoSave: true);
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
            var queryable = await _incidentRepo.GetQueryableAsync();
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

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable
                .OrderBy(x => x.Status == IncidentStatus.Pending ? 0 : 1)
                .ThenByDescending(x => x.CreatedOn)
                .Skip((page - 1) * take).Take(take));
            return Success(items.Select(ToDto).ToList(), total, page, take,
                total == 0 ? "Chưa có khiếu nại nào." : null);
        }

        public async Task<BaseResponse<IncidentResultDto>> GetByIdAsync(Guid id)
        {
            var incident = await _incidentRepo.FindAsync(id);
            if (incident == null || (!CurrentUser.IsInRole("Admin") && incident.CustomerId != CurrentUser.Id))
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            return Success(ToDto(incident));
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
            if (compensating && (command.ItemValue <= 0 || command.ItemValue > MaxItemValue))
                return BadRequest<IncidentResultDto>("Giá trị đồ phải lớn hơn 0 và không quá 100.000.000đ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại đã được xử lý trước đó.");

            incident.Status = IncidentStatus.Resolved;
            incident.ResolutionNote = note;
            incident.ResponsibleParty = command.ResponsibleParty;
            incident.CompensationLevel = command.CompensationLevel;
            incident.ItemValue = compensating ? command.ItemValue : 0m;
            // The amount is computed from the configured percentage, never taken from the client.
            incident.CompensationAmount = compensating
                ? Incident.CalculateCompensation(command.CompensationLevel, command.ItemValue) : 0m;
            incident.CompensationStatus = incident.CompensationAmount > 0 ? CompensationStatus.Pending : CompensationStatus.None;
            incident.ReviewedBy = CurrentUser.Id;
            incident.ReviewedAt = DateTime.UtcNow;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await uow.CompleteAsync();
            return Success(ToDto(incident), incident.CompensationAmount > 0
                ? $"Đã xử lý khiếu nại; tạo yêu cầu bồi thường {incident.CompensationAmount:N0}đ cho khách hàng."
                : "Đã xử lý khiếu nại.");
        }

        public async Task<BaseResponse<IncidentResultDto>> RejectAsync(Guid id, RejectIncidentCommand command)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
                return BadRequest<IncidentResultDto>("Vui lòng nhập lý do từ chối (tối đa 2000 ký tự).");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.Status != IncidentStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại đã được xử lý trước đó.");

            incident.Status = IncidentStatus.Rejected;
            incident.ResolutionNote = reason;
            incident.ReviewedBy = CurrentUser.Id;
            incident.ReviewedAt = DateTime.UtcNow;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await uow.CompleteAsync();
            return Success(ToDto(incident), "Đã từ chối khiếu nại.");
        }

        public async Task<BaseResponse<IncidentResultDto>> ConfirmCompensationAsync(Guid id, ConfirmCompensationCommand command)
        {
            var reference = command.Reference?.Trim();
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 120)
                return BadRequest<IncidentResultDto>("Vui lòng nhập mã tham chiếu chuyển khoản (tối đa 120 ký tự).");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var incident = await _incidentRepo.GetForUpdateAsync(id);
            if (incident == null)
                return NotFound<IncidentResultDto>("Không tìm thấy khiếu nại!");
            if (incident.CompensationStatus == CompensationStatus.Paid)
                return Success(ToDto(incident), "Bồi thường đã được ghi nhận trước đó.");
            if (incident.Status != IncidentStatus.Resolved || incident.CompensationStatus != CompensationStatus.Pending)
                return BadRequest<IncidentResultDto>("Khiếu nại không có khoản bồi thường chờ chi trả.");

            incident.CompensationStatus = CompensationStatus.Paid;
            incident.CompensationReference = reference;
            incident.CompensationPaidAt = DateTime.UtcNow;
            await _incidentRepo.UpdateAsync(incident, autoSave: true);
            await uow.CompleteAsync();
            return Success(ToDto(incident), "Đã ghi nhận chi trả bồi thường.");
        }

        private static IncidentResultDto ToDto(Incident x) => new()
        {
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
            Status = x.Status,
            ResolutionNote = x.ResolutionNote,
            ResponsibleParty = x.ResponsibleParty,
            CompensationLevel = x.CompensationLevel,
            ItemValue = x.ItemValue,
            CompensationAmount = x.CompensationAmount,
            CompensationStatus = x.CompensationStatus,
            CompensationReference = x.CompensationReference,
            CompensationPaidAt = x.CompensationPaidAt,
            ReviewedAt = x.ReviewedAt,
            CreatedOn = x.CreatedOn
        };
    }
}
