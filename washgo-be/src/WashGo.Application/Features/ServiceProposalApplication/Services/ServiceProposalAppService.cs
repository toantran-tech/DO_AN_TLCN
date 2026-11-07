using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Results;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Services;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.WashServiceApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ServiceProposalApplication.Services
{
    /// <summary>
    /// UC 2.6 Merchant xem dịch vụ và đề xuất thêm/sửa; UC 3.5.5–3.5.6 Admin phê duyệt hoặc từ chối.
    /// Đề xuất chỉ thay đổi dịch vụ khi được duyệt; giá trên các đơn đã đặt không bị ảnh hưởng (đơn lưu snapshot giá).
    /// </summary>
    public class ServiceProposalAppService : WashGoAppService, IServiceProposalAppService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        // Serializes proposal submissions: pending name / one-pending-per-service checks and the insert run under it.
        private const string SubmitLock = "service-proposal:submit";

        private readonly IRepository<ServiceChangeRequest, Guid> _requestRepo;
        private readonly IWashServiceRepository _serviceRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly NotificationPublisher _notifications;
        private readonly IConfiguration _configuration;

        public ServiceProposalAppService(
            IRepository<ServiceChangeRequest, Guid> requestRepo,
            IWashServiceRepository serviceRepo,
            IRepository<Merchant, Guid> merchantRepo,
            NotificationPublisher notifications,
            IConfiguration configuration)
        {
            _requestRepo = requestRepo;
            _serviceRepo = serviceRepo;
            _merchantRepo = merchantRepo;
            _notifications = notifications;
            _configuration = configuration;
        }

        // Merchant accounts share their id with the Merchant profile.
        private Guid MerchantId => CurrentUser.Id!.Value;

        private static readonly string[] SortFields = ["Name", "UnitPrice", "EstimatedDurationHours", "CreatedOn"];

        public async Task<PaginatedResult<WashServiceResultDto>> GetMyServicesAsync(WashServiceFilterQuery query)
        {
            var sorts = query.SortBy ?? [];
            if (sorts.Length > 1 || sorts.Any(x => !SortFields.Contains(x.Field, StringComparer.OrdinalIgnoreCase)))
                return new PaginatedResult<WashServiceResultDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.BadRequest, CurrentPage = query.Page, PageSize = query.Take,
                    Message = "Chỉ hỗ trợ sắp xếp theo một trường: Name, UnitPrice, EstimatedDurationHours hoặc CreatedOn."
                };
            // The catalog is shared: every Merchant processes the same approved services at the same prices.
            var queryable = (await _serviceRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && x.ApprovalStatus == ServiceApprovalStatus.Approved);
            if (query.ServiceType.HasValue)
                queryable = queryable.Where(x => x.ServiceType == query.ServiceType.Value);
            if (query.IsActive.HasValue)
                queryable = queryable.Where(x => x.IsActive == query.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.Name.ToLower().Contains(kw) || x.Code.ToLower().Contains(kw));
            }
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var sort = sorts.FirstOrDefault();
            // Whitelisted sort over the whole result before paging; Id keeps equal values in a stable order.
            IOrderedQueryable<WashService> ordered = (sort?.Field.ToLowerInvariant(), sort?.IsDescending ?? false) switch
            {
                ("unitprice", false) => queryable.OrderBy(x => x.UnitPrice),
                ("unitprice", true) => queryable.OrderByDescending(x => x.UnitPrice),
                ("estimateddurationhours", false) => queryable.OrderBy(x => x.EstimatedDurationHours),
                ("estimateddurationhours", true) => queryable.OrderByDescending(x => x.EstimatedDurationHours),
                ("createdon", false) => queryable.OrderBy(x => x.CreatedOn),
                ("createdon", true) => queryable.OrderByDescending(x => x.CreatedOn),
                ("name", true) => queryable.OrderByDescending(x => x.Name),
                _ => queryable.OrderBy(x => x.Name)
            };
            var items = await AsyncExecuter.ToListAsync(ordered.ThenBy(x => x.Id).Skip((page - 1) * take).Take(take));
            var dtos = await ToCatalogDtosAsync(items);
            return Success(dtos, total, page, take, total == 0 ? "Chưa có dịch vụ nào được cung cấp." : null);
        }

        public async Task<BaseResponse<WashServiceResultDto>> GetMyServiceAsync(Guid id)
        {
            var service = await _serviceRepo.FindAsync(id);
            if (service == null || service.IsDeleted || service.ApprovalStatus != ServiceApprovalStatus.Approved)
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ!");
            return Success((await ToCatalogDtosAsync([service]))[0]);
        }

        public async Task<BaseResponse<MerchantServiceSummaryDto>> GetSummaryAsync()
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var services = await AsyncExecuter.ToListAsync((await _serviceRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && x.ApprovalStatus == ServiceApprovalStatus.Approved)
                .GroupBy(x => x.IsActive).Select(g => new { Active = g.Key, Count = g.Count() }));
            var merchantId = MerchantId;
            var proposals = await AsyncExecuter.ToListAsync((await _requestRepo.GetQueryableAsync())
                .Where(x => x.MerchantId == merchantId)
                .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
            await uow.CompleteAsync();
            int P(ServiceApprovalStatus s) => proposals.Where(x => x.Status == s).Sum(x => x.Count);
            return Success(new MerchantServiceSummaryDto
            {
                TotalServices = services.Sum(x => x.Count),
                ActiveServices = services.Where(x => x.Active).Sum(x => x.Count),
                InactiveServices = services.Where(x => !x.Active).Sum(x => x.Count),
                PendingProposals = P(ServiceApprovalStatus.Pending),
                ApprovedProposals = P(ServiceApprovalStatus.Approved),
                RejectedProposals = P(ServiceApprovalStatus.Rejected),
                GeneratedAt = DateTime.UtcNow
            });
        }

        private async Task<List<WashServiceResultDto>> ToCatalogDtosAsync(List<WashService> items)
        {
            var ids = items.Select(x => x.Id).ToList();
            var merchantId = MerchantId;
            var pending = await AsyncExecuter.ToListAsync((await _requestRepo.GetQueryableAsync())
                .Where(x => x.Status == ServiceApprovalStatus.Pending && x.ServiceId != null && ids.Contains(x.ServiceId.Value))
                .Select(x => new { ServiceId = x.ServiceId!.Value, x.Id, x.MerchantId }));
            var merchant = await _merchantRepo.FindAsync(merchantId);
            var merchantActive = merchant?.Status == MerchantStatus.Active;
            var dtos = ObjectMapper.Map<List<WashService>, List<WashServiceResultDto>>(items);
            foreach (var dto in dtos)
            {
                var service = items.First(x => x.Id == dto.Id);
                var forService = pending.Where(x => x.ServiceId == dto.Id).ToList();
                dto.ImageUrls = ReadImages(service.Images);
                dto.Version = VersionOf(service);
                dto.HasPendingProposal = forService.Count > 0;
                dto.MyPendingProposalId = forService.FirstOrDefault(x => x.MerchantId == merchantId)?.Id;
                (dto.ProposeBlockedCode, dto.ProposeBlockedReason) =
                    !merchantActive ? ("MerchantInactive", "Tài khoản Merchant không hoạt động.")
                    : dto.HasPendingProposal ? ("PendingProposal", "Dịch vụ đang có đề xuất chờ duyệt.")
                    : ((string?)null, (string?)null);
                dto.CanProposeUpdate = dto.ProposeBlockedCode == null;
            }
            return dtos;
        }

        /// <summary>Content version of a service: hash of the fields a proposal can change.</summary>
        private static string VersionOf(WashService service) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Snapshot(service), Json))))[..16].ToLowerInvariant();

        public Task<BaseResponse<ServiceProposalDto>> ProposeCreateAsync(ServiceProposalCommand command) =>
            ProposeAsync(null, command);

        public Task<BaseResponse<ServiceProposalDto>> ProposeUpdateAsync(Guid serviceId, ServiceProposalCommand command) =>
            ProposeAsync(serviceId, command);

        private BaseResponse<T> Fail<T>(HttpStatusCode status, string code, string message, T? data = default) => new()
        {
            Succeeded = false, StatusCode = status, Message = message, Data = data, Meta = new { code }
        };

        private async Task<BaseResponse<ServiceProposalDto>> ProposeAsync(Guid? serviceId, ServiceProposalCommand command)
        {
            if (command.ClientRequestId == Guid.Empty)
                return BadRequest<ServiceProposalDto>("clientRequestId không hợp lệ.");
            var merchant = await _merchantRepo.FindAsync(MerchantId);
            if (merchant == null || merchant.Status != MerchantStatus.Active)
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "MerchantInactive", "Tài khoản Merchant không hoạt động.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _serviceRepo.AcquireLockAsync(SubmitLock);
            if (command.ClientRequestId.HasValue)
            {
                var previous = await _requestRepo.FirstOrDefaultAsync(x => x.MerchantId == merchant.Id && x.ClientRequestId == command.ClientRequestId);
                if (previous != null)
                {
                    await uow.CompleteAsync();
                    var sameTarget = serviceId.HasValue
                        ? previous.RequestType == ServiceChangeRequestType.Update && previous.ServiceId == serviceId
                        : previous.RequestType == ServiceChangeRequestType.Create;
                    if (!sameTarget)
                        return Fail(HttpStatusCode.Conflict, "ClientRequestIdReused", "clientRequestId này đã dùng cho một đề xuất khác.",
                            ToDto(previous, merchant.BusinessName));
                    return new BaseResponse<ServiceProposalDto>
                    {
                        Succeeded = true, StatusCode = HttpStatusCode.OK, Data = ToDto(previous, merchant.BusinessName),
                        Meta = new { code = "Replayed", replayed = true },
                        Message = "Đề xuất này đã được gửi trước đó; không gửi lại."
                    };
                }
            }

            WashService? service = null;
            if (serviceId.HasValue)
            {
                service = await _serviceRepo.FindAsync(serviceId.Value);
                if (service == null || service.IsDeleted)
                    return NotFound<ServiceProposalDto>("Không tìm thấy dịch vụ!");
                if (!string.IsNullOrWhiteSpace(command.ExpectedVersion) && command.ExpectedVersion.Trim() != VersionOf(service))
                    return Fail<ServiceProposalDto>(HttpStatusCode.Conflict, "StaleVersion",
                        "Dịch vụ đã được thay đổi sau khi bạn mở; vui lòng tải lại trước khi đề xuất.");
                // Edits keep the service's category and unit; only name, description, prices, duration and images change.
                command.ServiceType = service.ServiceType;
                command.Unit = service.Unit;
            }

            var error = WashServiceRules.Validate(command.Name, command.UnitPrice, command.PricePerKg, command.PricePerItem,
                command.EstimatedDurationHours, command.ServiceType, command.Unit);
            if (error != null)
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, error == WashServiceRules.InvalidPrice ? "InvalidPrice" : "Invalid", error);
            var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
            if (reason?.Length > 1000 || command.Description?.Length > 2000)
                return BadRequest<ServiceProposalDto>("Mô tả hoặc lý do quá dài.");

            // Images: null keeps the service's images; a list is the new set. URLs already on the service may stay;
            // every other URL must be the Merchant's own upload.
            var current = service == null ? [] : ReadImages(service.Images);
            List<string> images;
            if (command.ImageUrls == null)
                images = current;
            else
            {
                images = command.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                if (images.Count > UploadStorage.MaxImagesPerSubmission)
                    return BadRequest<ServiceProposalDto>($"Tối đa {UploadStorage.MaxImagesPerSubmission} ảnh.");
                var added = images.Where(x => !current.Contains(x)).ToList();
                var imageError = UploadStorage.ValidateOwnedImages(_configuration, added, merchant.Id, 0, out _);
                if (imageError != null)
                    return BadRequest<ServiceProposalDto>(imageError);
            }

            var name = command.Name.Trim();
            if (await WashServiceRules.NameTakenAsync(_serviceRepo, name, serviceId) ||
                await PendingNameTakenAsync(name, serviceId))
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "DuplicateName", WashServiceRules.DuplicateName);
            if (serviceId.HasValue && await _requestRepo.AnyAsync(x => x.ServiceId == serviceId && x.Status == ServiceApprovalStatus.Pending))
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "PendingProposal", "Dịch vụ đang có đề xuất chờ duyệt.");

            var proposed = new ServiceProposalDataDto
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
                UnitPrice = command.UnitPrice,
                PricePerKg = command.PricePerKg,
                PricePerItem = command.PricePerItem,
                EstimatedDurationHours = command.EstimatedDurationHours,
                ServiceType = command.ServiceType,
                Unit = command.Unit.Trim(),
                ImageUrls = images
            };
            var request = new ServiceChangeRequest(GuidGenerator.Create())
            {
                MerchantId = merchant.Id,
                ServiceId = serviceId,
                RequestType = serviceId.HasValue ? ServiceChangeRequestType.Update : ServiceChangeRequestType.Create,
                ProposedData = JsonSerializer.Serialize(proposed, Json),
                ProposedName = name,
                ClientRequestId = command.ClientRequestId,
                CurrentData = service == null ? null : JsonSerializer.Serialize(Snapshot(service), Json),
                Reason = reason,
                Status = ServiceApprovalStatus.Pending
            };
            await _requestRepo.InsertAsync(request, autoSave: true);
            await _notifications.SendToAdminsAsync(NotificationType.ServiceProposalSubmitted, "Đề xuất dịch vụ cần duyệt",
                serviceId.HasValue
                    ? $"Merchant {merchant.BusinessName} đề xuất chỉnh sửa dịch vụ {service!.Name}, cần phê duyệt."
                    : $"Merchant {merchant.BusinessName} đề xuất dịch vụ mới {name}, cần phê duyệt.",
                "service-proposal", request.Id);
            await uow.CompleteAsync();
            return Created(ToDto(request, merchant.BusinessName), "Đề xuất đã được gửi. Vui lòng chờ Admin phê duyệt.");
        }

        private async Task<bool> PendingNameTakenAsync(string name, Guid? exceptServiceId)
        {
            var normalized = name.ToLower();
            return await _requestRepo.AnyAsync(x => x.Status == ServiceApprovalStatus.Pending &&
                                                    (exceptServiceId == null || x.ServiceId != exceptServiceId) &&
                                                    x.ProposedName.ToLower() == normalized);
        }

        public async Task<PaginatedResult<ServiceProposalDto>> GetListAsync(ServiceProposalFilterQuery query)
        {
            var isAdmin = CurrentUser.IsInRole("Admin");
            var queryable = await _requestRepo.GetQueryableAsync();
            // Merchants only see their own proposals.
            var merchantId = isAdmin ? query.MerchantId : MerchantId;
            if (merchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == merchantId.Value);
            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);
            if (query.RequestType.HasValue)
                queryable = queryable.Where(x => x.RequestType == query.RequestType.Value);
            if (query.ServiceId.HasValue)
                queryable = queryable.Where(x => x.ServiceId == query.ServiceId.Value);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                var services = await _serviceRepo.GetQueryableAsync();
                queryable = queryable.Where(x => x.ProposedName.ToLower().Contains(kw) ||
                                                 services.Any(s => s.Id == x.ServiceId && s.Code.ToLower().Contains(kw)));
            }
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable
                .OrderBy(x => x.Status == ServiceApprovalStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * take).Take(take));
            var names = await MerchantNamesAsync(items.Select(x => x.MerchantId));
            return Success(items.Select(x => ToDto(x, names.GetValueOrDefault(x.MerchantId, string.Empty))).ToList(),
                total, page, take, total == 0 ? "Chưa có đề xuất nào." : null);
        }

        public async Task<BaseResponse<ServiceProposalDto>> GetByIdAsync(Guid id)
        {
            var request = await _requestRepo.FindAsync(id);
            if (request == null || (!CurrentUser.IsInRole("Admin") && request.MerchantId != CurrentUser.Id))
                return NotFound<ServiceProposalDto>("Không tìm thấy đề xuất!");
            var names = await MerchantNamesAsync([request.MerchantId]);
            return Success(ToDto(request, names.GetValueOrDefault(request.MerchantId, string.Empty)));
        }

        private const string AlreadyReviewed = "Đề xuất đã được xử lý trước đó.";

        public async Task<BaseResponse<ServiceProposalDto>> ApproveAsync(Guid id)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            // One reviewer at a time per proposal: a second approve/reject waits, then sees the stored result.
            await _serviceRepo.AcquireLockAsync($"service-proposal:{id}");
            var request = await _requestRepo.FindAsync(id);
            if (request == null)
                return NotFound<ServiceProposalDto>("Không tìm thấy đề xuất!");
            if (request.Status != ServiceApprovalStatus.Pending)
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "AlreadyReviewed", AlreadyReviewed);
            var data = Read(request.ProposedData);
            if (data == null)
                return BadRequest<ServiceProposalDto>("Có lỗi xảy ra. Vui lòng thử lại.");
            var merchant = await _merchantRepo.FindAsync(request.MerchantId);
            if (merchant == null)
                return BadRequest<ServiceProposalDto>("Merchant của đề xuất không còn tồn tại.");

            // Re-check at approval time: another service may have taken the name meanwhile.
            var error = WashServiceRules.Validate(data.Name, data.UnitPrice, data.PricePerKg, data.PricePerItem,
                data.EstimatedDurationHours, data.ServiceType, data.Unit);
            if (error != null)
                return BadRequest<ServiceProposalDto>(error);
            if (await WashServiceRules.NameTakenAsync(_serviceRepo, data.Name, request.ServiceId))
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "DuplicateName", WashServiceRules.DuplicateName);

            var now = DateTime.UtcNow;
            WashService service;
            if (request.RequestType == ServiceChangeRequestType.Create)
            {
                service = new WashService(GuidGenerator.Create())
                {
                    MerchantId = merchant.Id,
                    Code = $"SVC_{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                    IsActive = true
                };
                Apply(service, data);
                service.ApprovalStatus = ServiceApprovalStatus.Approved;
                service.ApprovedBy = CurrentUser.Id;
                service.ApprovedAt = now;
                await _serviceRepo.InsertAsync(service, autoSave: true);
                request.ServiceId = service.Id;
            }
            else
            {
                var existing = request.ServiceId.HasValue ? await _serviceRepo.FindAsync(request.ServiceId.Value) : null;
                if (existing == null || existing.IsDeleted)
                    return BadRequest<ServiceProposalDto>("Dịch vụ cần chỉnh sửa không còn tồn tại.");
                // The proposal was made against a snapshot; a newer change to the shared service must not be overwritten.
                var sent = Read(request.CurrentData);
                if (sent != null && JsonSerializer.Serialize(sent, Json) != JsonSerializer.Serialize(Snapshot(existing), Json))
                    return Fail<ServiceProposalDto>(HttpStatusCode.Conflict, "StaleVersion",
                        "Dịch vụ đã thay đổi sau khi Merchant gửi đề xuất; hãy từ chối và yêu cầu Merchant gửi lại theo bản mới.");
                service = existing;
                Apply(service, data);
                service.ApprovedBy = CurrentUser.Id;
                service.ApprovedAt = now;
                await _serviceRepo.UpdateAsync(service, autoSave: true);
            }

            request.Status = ServiceApprovalStatus.Approved;
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = now;
            request.UpdatedAt = now;
            await _requestRepo.UpdateAsync(request, autoSave: true);
            await _notifications.SendAsync([merchant.Id], NotificationType.ServiceProposalReviewed, "Đề xuất được phê duyệt",
                $"Yêu cầu {(request.RequestType == ServiceChangeRequestType.Create ? "thêm dịch vụ" : "chỉnh sửa dịch vụ")} {data.Name} của bạn đã được phê duyệt.",
                "service", service.Id);
            await uow.CompleteAsync();
            return Success(ToDto(request, merchant.BusinessName), "Đã phê duyệt đề xuất!");
        }

        public async Task<BaseResponse<ServiceProposalDto>> RejectAsync(Guid id, RejectServiceProposalCommand command)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                return BadRequest<ServiceProposalDto>("Vui lòng nhập lý do từ chối (tối đa 1000 ký tự).");
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _serviceRepo.AcquireLockAsync($"service-proposal:{id}");
            var request = await _requestRepo.FindAsync(id);
            if (request == null)
                return NotFound<ServiceProposalDto>("Không tìm thấy đề xuất!");
            if (request.Status != ServiceApprovalStatus.Pending)
                return Fail<ServiceProposalDto>(HttpStatusCode.BadRequest, "AlreadyReviewed", AlreadyReviewed);

            request.Status = ServiceApprovalStatus.Rejected;
            request.RejectionReason = reason;
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepo.UpdateAsync(request, autoSave: true);
            var data = Read(request.ProposedData);
            await _notifications.SendAsync([request.MerchantId], NotificationType.ServiceProposalReviewed, "Đề xuất bị từ chối",
                $"Yêu cầu {(request.RequestType == ServiceChangeRequestType.Create ? "thêm dịch vụ" : "chỉnh sửa dịch vụ")} {data?.Name} của bạn đã bị từ chối. Lý do: {reason}",
                "service-proposal", request.Id);
            await uow.CompleteAsync();
            var names = await MerchantNamesAsync([request.MerchantId]);
            return Success(ToDto(request, names.GetValueOrDefault(request.MerchantId, string.Empty)), "Đã từ chối đề xuất!");
        }

        private async Task<Dictionary<Guid, string>> MerchantNamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            return (await _merchantRepo.GetListAsync(x => list.Contains(x.Id))).ToDictionary(x => x.Id, x => x.BusinessName);
        }

        private static void Apply(WashService service, ServiceProposalDataDto data)
        {
            service.Name = data.Name;
            service.Description = data.Description;
            service.UnitPrice = data.UnitPrice;
            service.PricePerKg = data.PricePerKg;
            service.PricePerItem = data.PricePerItem;
            service.EstimatedDurationHours = data.EstimatedDurationHours;
            service.ServiceType = data.ServiceType;
            service.Unit = data.Unit;
            service.Images = data.ImageUrls.Count == 0 ? null : JsonSerializer.Serialize(data.ImageUrls);
        }

        private static ServiceProposalDataDto Snapshot(WashService s) => new()
        {
            Name = s.Name,
            Description = s.Description,
            UnitPrice = s.UnitPrice,
            PricePerKg = s.PricePerKg,
            PricePerItem = s.PricePerItem,
            EstimatedDurationHours = s.EstimatedDurationHours,
            ServiceType = s.ServiceType,
            Unit = s.Unit,
            ImageUrls = ReadImages(s.Images)
        };

        private static List<string> ReadImages(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
            catch (JsonException) { return []; }
        }

        private static ServiceProposalDataDto? Read(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<ServiceProposalDataDto>(json, Json); }
            catch (JsonException) { return null; }
        }

        private static ServiceProposalDto ToDto(ServiceChangeRequest x, string merchantName) => new()
        {
            Id = x.Id,
            MerchantId = x.MerchantId,
            MerchantName = merchantName,
            ServiceId = x.ServiceId,
            RequestType = x.RequestType,
            Status = x.Status,
            Proposed = Read(x.ProposedData) ?? new ServiceProposalDataDto(),
            Current = Read(x.CurrentData),
            Reason = x.Reason,
            RejectionReason = x.RejectionReason,
            ReviewedAt = x.ReviewedAt,
            CreatedAt = x.CreatedAt
        };
    }
}
