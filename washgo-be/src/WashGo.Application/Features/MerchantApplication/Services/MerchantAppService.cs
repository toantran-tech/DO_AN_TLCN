using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
using WashGo.Application.Contracts.Features.MerchantContracts.Services;
using WashGo.Application.Features.AuthApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Features.WashOrderApplication.Services;
using WashGo.Domain.Entities.Finance;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using WashGo.Core.Utils;
using System.Data;
using System.Text.Json;

using WashGo.Application.Features.SystemSettingApplication.Services;

namespace WashGo.Application.Features.MerchantApplication.Services
{
    /// <summary>UC 3.2.1–3.2.4: Admin xem, thêm, chỉnh sửa, khóa/mở khóa Merchant.</summary>
    public class MerchantAppService : WashGoAppService, IMerchantAppService
    {
        private const string StoreImageType = "store";
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        // Orders the Merchant still has to process (UC 3.2.4 6a).
        private static readonly WashOrderStatus[] UnfinishedStatuses =
        [
            WashOrderStatus.Pending, WashOrderStatus.Deposited, WashOrderStatus.Collected, WashOrderStatus.Washing,
            WashOrderStatus.Drying, WashOrderStatus.Ironing, WashOrderStatus.Folding, WashOrderStatus.AwaitingPayment,
            WashOrderStatus.Paid, WashOrderStatus.ReadyToReturn
        ];

        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Role, Guid> _roleRepo;
        private readonly IRepository<RefreshToken, Guid> _tokenRepo;
        private readonly IRepository<MerchantServiceArea, Guid> _msaRepo;
        private readonly IRepository<ServiceArea, Guid> _areaRepo;
        private readonly IRepository<MerchantImage, Guid> _imageRepo;
        private readonly IRepository<MerchantProfileRequest, Guid> _profileRequestRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<MerchantSettlement, Guid> _settlementRepo;
        private readonly OrderListEnricher _enricher;
        private readonly SystemSettingProvider _settings;
        private readonly IAuthEmailSender _emailSender;
        private readonly NotificationPublisher _notifications;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MerchantAppService> _logger;
        private readonly MerchantWriteGuard _writeGuard;
        private readonly MerchantDocumentLinks _documents;

        public MerchantAppService(
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Role, Guid> roleRepo,
            IRepository<RefreshToken, Guid> tokenRepo,
            IRepository<MerchantServiceArea, Guid> msaRepo,
            IRepository<ServiceArea, Guid> areaRepo,
            IRepository<MerchantImage, Guid> imageRepo,
            IRepository<MerchantProfileRequest, Guid> profileRequestRepo,
            IWashOrderRepository orderRepo,
            IRepository<MerchantSettlement, Guid> settlementRepo,
            OrderListEnricher enricher,
            SystemSettingProvider settings,
            IAuthEmailSender emailSender,
            NotificationPublisher notifications,
            IConfiguration configuration,
            ILogger<MerchantAppService> logger,
            MerchantWriteGuard writeGuard, MerchantDocumentLinks documents)
        {
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _tokenRepo = tokenRepo;
            _msaRepo = msaRepo;
            _areaRepo = areaRepo;
            _imageRepo = imageRepo;
            _profileRequestRepo = profileRequestRepo;
            _orderRepo = orderRepo;
            _settlementRepo = settlementRepo;
            _enricher = enricher;
            _settings = settings;
            _emailSender = emailSender;
            _notifications = notifications;
            _configuration = configuration;
            _logger = logger;
            _writeGuard = writeGuard;
            _documents = documents;
        }

        public async Task<BaseResponse<MerchantSummaryDto>> GetSummaryAsync()
        {
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: IsolationLevel.RepeatableRead);
            var counts = await AsyncExecuter.ToListAsync((await _merchantRepo.GetQueryableAsync())
                .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
            var pending = await _profileRequestRepo.CountAsync(x => x.Status == ServiceApprovalStatus.Pending);
            var result = new MerchantSummaryDto
            {
                Total = counts.Sum(x => x.Count),
                Active = counts.Where(x => x.Status == MerchantStatus.Active).Sum(x => x.Count),
                Inactive = counts.Where(x => x.Status == MerchantStatus.Inactive).Sum(x => x.Count),
                Suspended = counts.Where(x => x.Status == MerchantStatus.Suspended).Sum(x => x.Count),
                PendingProfileRequests = pending,
                GeneratedAt = DateTime.UtcNow
            };
            await snapshot.CompleteAsync();
            return Success(result);
        }

        public async Task<BaseResponse<MerchantOperationResultDto>> GetOperationAsync(Guid operationId)
        {
            var row = await _writeGuard.FindAsync(operationId, CurrentUser.Id!.Value);
            if (row == null) return NotFound<MerchantOperationResultDto>("Không tìm thấy thao tác đã commit của bạn.");
            object result = JsonSerializer.Deserialize<JsonElement>(row.ResponseJson);
            if (row.Operation == "Create")
            {
                var created = JsonSerializer.Deserialize<BaseResponse<MerchantResultDto>>(row.ResponseJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (created?.Data != null)
                {
                    await FillCredentialDeliveryAsync(created.Data);
                    result = created;
                }
            }
            return Success(new MerchantOperationResultDto
            {
                OperationId = row.Id, MerchantId = row.MerchantId, Operation = row.Operation, CreatedAt = row.CreatedAt,
                Result = result
            });
        }

        // UC 3.2.1
        public async Task<PaginatedResult<MerchantResultDto>> GetListAsync(MerchantFilterQuery query)
        {
            var dateError = query.DateRangeError();
            var sorts = query.SortBy ?? [];
            var allowed = new[] { "businessname", "createdat", "joinedat", "rating", "status", "id" };
            if (dateError != null || sorts.Any(x => !allowed.Contains(x.Field?.ToLowerInvariant())) || query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
                return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = dateError ?? "Sort hỗ trợ BusinessName, CreatedAt, JoinedAt, Rating, Status, Id." };
            var queryable = await _merchantRepo.GetQueryableAsync();
            if (query.FromDate > 0) { var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate).UtcDateTime; queryable = queryable.Where(x => x.CreatedAt >= from); }
            if (query.ToDate > 0) { var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate).UtcDateTime; queryable = queryable.Where(x => x.CreatedAt <= to); }

            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);

            if (!string.IsNullOrWhiteSpace(query.City))
                queryable = queryable.Where(x => x.City == query.City);

            if (!string.IsNullOrWhiteSpace(query.District))
                queryable = queryable.Where(x => x.District == query.District);

            if (query.ServiceAreaId.HasValue)
            {
                var links = await _msaRepo.GetQueryableAsync();
                queryable = queryable.Where(x => links.Any(l => l.MerchantId == x.Id && l.IsActive && l.ServiceAreaId == query.ServiceAreaId.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.BusinessName.ToLower().Contains(kw) ||
                                                 x.Address.ToLower().Contains(kw) ||
                                                 x.Phone.Contains(kw));
            }

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            IOrderedQueryable<Merchant>? ordered = null;
            void Sort<TKey>(System.Linq.Expressions.Expression<Func<Merchant, TKey>> field, bool descending) =>
                ordered = ordered == null ? descending ? queryable.OrderByDescending(field) : queryable.OrderBy(field) : descending ? ordered.ThenByDescending(field) : ordered.ThenBy(field);
            foreach (var option in sorts)
                switch (option.Field.ToLowerInvariant())
                {
                    case "businessname": Sort(x => x.BusinessName, option.IsDescending); break;
                    case "createdat": Sort(x => x.CreatedAt, option.IsDescending); break;
                    case "joinedat": Sort(x => x.ApprovedAt ?? x.CreatedAt, option.IsDescending); break;
                    case "rating": Sort(x => x.Rating, option.IsDescending); break;
                    case "status": Sort(x => x.Status, option.IsDescending); break;
                    case "id": Sort(x => x.Id, option.IsDescending); break;
                }
            var items = await AsyncExecuter.ToListAsync(
                (ordered ?? queryable.OrderBy(x => x.BusinessName)).ThenBy(x => x.Id)
                         .Skip((page - 1) * take)
                         .Take(take)
            );

            return Success(await ToDtosAsync(items), total, page, take, total == 0 ? "Chưa có Merchant nào." : null);
        }

        public async Task<BaseResponse<MerchantResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _merchantRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy đối tác giặt sấy!");
            }
            return Success((await ToDtosAsync([entity]))[0]);
        }

        // UC 3.2.2: creates the Merchant login (role Merchant) and profile; credentials go out by email.
        public Task<BaseResponse<MerchantResultDto>> CreateAsync(CreateMerchantCommand command) =>
            _writeGuard.IdentitySafeAsync(() => CreateCoreAsync(command));

        private async Task<BaseResponse<MerchantResultDto>> CreateCoreAsync(CreateMerchantCommand command)
        {
            var email = command.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            command.Email = email;
            command.TaxCode = string.IsNullOrWhiteSpace(command.TaxCode) ? null : command.TaxCode.Trim();
            string temporaryPassword;
            Merchant entity;
            BaseResponse<MerchantResultDto> response;
            using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                if (await _writeGuard.ReplayAsync<MerchantResultDto>(command.ClientRequestId, CurrentUser.Id!.Value, null, "Create", command) is { } replay)
                {
                    if (replay.Succeeded && replay.Data != null) await FillCredentialDeliveryAsync(replay.Data);
                    return replay;
                }
                if (!MerchantRules.IsValidEmail(email))
                    return BadRequest<MerchantResultDto>("Email không hợp lệ.");
                await _writeGuard.LockIdentityAsync(email, command.TaxCode);
                if (await _userRepo.AnyAsync(x => x.Email.ToLower() == email))
                    return MerchantWriteGuard.Error<MerchantResultDto>(HttpStatusCode.Conflict, "EmailAlreadyUsed", "Email này đã được sử dụng.");
                if (command.TaxCode != null && await _merchantRepo.AnyAsync(x => x.TaxCode == command.TaxCode))
                    return MerchantWriteGuard.Error<MerchantResultDto>(HttpStatusCode.Conflict, "TaxCodeAlreadyUsed", "Mã số thuế này đã được sử dụng.");
                var (error, phone) = await ValidateAsync(command, email, null);
                if (error != null) return BadRequest<MerchantResultDto>(error);
                var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, CurrentUser.Id.Value, 0, out var images);
                if (imageError != null) return BadRequest<MerchantResultDto>(imageError);
                var role = await _roleRepo.FirstOrDefaultAsync(x => x.Name == "Merchant");
                if (role == null) return BadRequest<MerchantResultDto>("Có lỗi xảy ra. Vui lòng thử lại.");
                temporaryPassword = TemporaryPassword.Generate();
                var displayName = command.BusinessName.Trim();
                var user = new User(GuidGenerator.Create())
                {
                    RoleId = role.Id, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                    AuthProvider = AuthProvider.Local,
                    // Account display names have a 100-character limit; the store retains its full legal name.
                    FullName = displayName.Length > 100 ? displayName[..100] : displayName, IsActive = true
                };
                entity = new Merchant(user.Id)
                {
                    Status = MerchantStatus.Active, Rating = 5.0m,
                    CommissionRate = command.CommissionRate ?? (await _settings.GetAsync()).DefaultCommissionRate,
                    ApprovedBy = CurrentUser.Id, ApprovedAt = DateTime.UtcNow
                };
                Apply(entity, command, phone!);
                var documentError = await _documents.ApplyAsync(entity, command, CurrentUser.Id.Value);
                if (documentError != null) return BadRequest<MerchantResultDto>(documentError);
                await _userRepo.InsertAsync(user, autoSave: true);
                await _merchantRepo.InsertAsync(entity, autoSave: true);
                await ReplaceImagesAsync(entity.Id, images);
                await ReplaceAreasAsync(entity.Id, command.ServiceAreaIds ?? []);
                await Audit.LogAsync(new AuditEntry("Merchant", "Create", "Merchant", entity.Id.ToString(), entity.BusinessName)
                { Details = new { entity.CommissionRate, status = entity.Status.ToString(), operationId = command.ClientRequestId } });
                response = Created((await ToDtosAsync([entity]))[0], "Tài khoản Merchant đã được tạo; trạng thái gửi email được trả riêng.");
                await _writeGuard.RecordAsync(command.ClientRequestId, CurrentUser.Id.Value, entity.Id, "Create", command, response);
                await uow.CompleteAsync();
            }
            // Account + operation are committed BEFORE SMTP. Only the first creator ever attempts delivery.
            var delivery = CredentialDeliveryStatus.Unknown;
            try { delivery = await _emailSender.SendMerchantCredentialsAsync(email, entity.BusinessName, temporaryPassword); }
            catch (Exception) { _logger.LogWarning("Merchant {MerchantId} credential email outcome is unknown.", entity.Id); }
            try
            {
                using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                await _writeGuard.LockMerchantAsync(entity.Id);
                var saved = await _merchantRepo.GetAsync(entity.Id);
                saved.CredentialDelivery = delivery;
                saved.CredentialSentAt = delivery == CredentialDeliveryStatus.Sent ? DateTime.UtcNow : null;
                // Delivery metadata does not change the profile version or overwrite concurrent profile edits.
                await _merchantRepo.UpdateAsync(saved, autoSave: true);
                await uow.CompleteAsync();
                response.Data!.CredentialDelivery = saved.CredentialDelivery;
                response.Data.CredentialSentAt = saved.CredentialSentAt;
            }
            catch (Exception)
            {
                // The create is already durable. Do not report a false failed creation or retry SMTP.
                _logger.LogWarning("Merchant {MerchantId} created; credential delivery metadata could not be confirmed.", entity.Id);
            }
            return response;
        }

        private async Task FillCredentialDeliveryAsync(MerchantResultDto dto)
        {
            var merchant = await _merchantRepo.FindAsync(dto.Id);
            if (merchant == null) return;
            dto.CredentialDelivery = merchant.CredentialDelivery;
            dto.CredentialSentAt = merchant.CredentialSentAt;
        }

        // UC 3.2.3
        public Task<BaseResponse<MerchantResultDto>> UpdateAsync(UpdateMerchantCommand command) =>
            _writeGuard.IdentitySafeAsync(() => UpdateCoreAsync(command));

        private async Task<BaseResponse<MerchantResultDto>> UpdateCoreAsync(UpdateMerchantCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await _writeGuard.ReplayAsync<MerchantResultDto>(command.OperationId, CurrentUser.Id!.Value,
                    command.Id, "Update", command) is { } replay)
                return replay;
            // Match create's lock order. Project the current email without tracking a stale User entity.
            var identityEmail = string.IsNullOrWhiteSpace(command.Email)
                ? await AsyncExecuter.FirstOrDefaultAsync((await _userRepo.GetQueryableAsync()).Where(x => x.Id == command.Id).Select(x => x.Email)) ?? string.Empty
                : command.Email.Trim().ToLowerInvariant();
            await _writeGuard.LockIdentityAsync(identityEmail, command.TaxCode?.Trim());
            await _writeGuard.LockMerchantAsync(command.Id);
            if (command.Status.HasValue && !Enum.IsDefined(typeof(MerchantStatus), command.Status.Value))
            {
                return BadRequest<MerchantResultDto>("Trạng thái Merchant không hợp lệ. Chỉ chấp nhận 1, 2 hoặc 3.");
            }

            var entity = await _merchantRepo.FindAsync(command.Id);
            var user = entity == null ? null : await _userRepo.FindAsync(entity.Id);
            if (entity == null || user == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy hồ sơ đối tác giặt sấy!");
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return MerchantWriteGuard.Error<MerchantResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion từ GET /merchant/{id}.");
            if (command.ExpectedVersion != MerchantWriteGuard.Version(entity))
                return MerchantWriteGuard.Error(HttpStatusCode.Conflict, "VersionMismatch", "Hồ sơ đã thay đổi; vui lòng tải lại.", (await ToDtosAsync([entity]))[0]);
            // Keep the legacy field readable, but never let profile edits bypass lock rules.
            if (command.Status.HasValue && command.Status.Value != entity.Status)
                return new BaseResponse<MerchantResultDto>
                {
                    Succeeded = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    Meta = new { code = "UseMerchantStatusEndpoint" },
                    Message = "Vui lòng dùng PUT /merchant/status để khóa hoặc mở khóa Merchant."
                };
            var auditBefore = (await ToDtosAsync([entity]))[0];

            // Email is optional on edit: blank keeps the login email.
            var email = string.IsNullOrWhiteSpace(command.Email) ? user.Email : command.Email.Trim().ToLowerInvariant();
            if (!MerchantRules.IsValidEmail(email))
                return BadRequest<MerchantResultDto>("Email không hợp lệ.");
            var (error, phone) = await ValidateAsync(command, email, entity.Id);
            if (error != null)
                return BadRequest<MerchantResultDto>(error);
            List<string>? images = null;
            if (command.ImageUrls != null)
            {
                var current = await StoreImagesAsync(entity.Id);
                var added = command.ImageUrls.Where(x => !current.Contains(x?.Trim() ?? string.Empty)).ToList();
                var imageError = UploadStorage.ValidateOwnedImages(_configuration, added, CurrentUser.Id!.Value, 0, out _);
                if (imageError != null)
                    return BadRequest<MerchantResultDto>(imageError);
                images = command.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                if (images.Count > UploadStorage.MaxImagesPerSubmission)
                    return BadRequest<MerchantResultDto>($"Tối đa {UploadStorage.MaxImagesPerSubmission} ảnh.");
            }

            var previousVersion = entity.UpdatedAt.Ticks / 10;
            var documentError = await _documents.ApplyAsync(entity, command, CurrentUser.Id!.Value);
            if (documentError != null) return BadRequest<MerchantResultDto>(documentError);
            Apply(entity, command, phone!);
            entity.UpdatedAt = new DateTime(Math.Max(entity.UpdatedAt.Ticks / 10, previousVersion + 1) * 10, DateTimeKind.Utc);
            if (command.CommissionRate.HasValue)
                entity.CommissionRate = command.CommissionRate.Value;
            await _merchantRepo.UpdateAsync(entity, autoSave: true);
            if (user.Email != email)
            {
                user.Email = email;
                user.UpdatedAt = DateTime.UtcNow;
            }
            await _userRepo.UpdateAsync(user, autoSave: true);
            if (images != null)
                await ReplaceImagesAsync(entity.Id, images);
            if (command.ServiceAreaIds != null)
                await ReplaceAreasAsync(entity.Id, command.ServiceAreaIds);
            var auditAfter = (await ToDtosAsync([entity]))[0];
            await Audit.LogAsync(new AuditEntry("Merchant", "Update", "Merchant", entity.Id.ToString(), entity.BusinessName)
            {
                ChangedFields = AuditLogWriter.Diff(auditBefore, auditAfter)
            });
            var response = Success(auditAfter, "Cập nhật Merchant thành công!");
            await _writeGuard.RecordAsync(command.OperationId, CurrentUser.Id.Value, entity.Id, "Update", command, response);
            await uow.CompleteAsync();
            return response;
        }

        // UC 3.2.4: lock needs a reason and, with unfinished orders, an explicit confirmation.
        public async Task<BaseResponse<MerchantResultDto>> UpdateStatusAsync(UpdateMerchantStatusCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await _writeGuard.ReplayAsync<MerchantResultDto>(command.OperationId, CurrentUser.Id!.Value,
                    command.MerchantId, "Status", command) is { } replay)
                return replay;
            await _writeGuard.LockMerchantAsync(command.MerchantId);
            if (!Enum.IsDefined(typeof(MerchantStatus), command.Status))
            {
                return BadRequest<MerchantResultDto>("Trạng thái Merchant không hợp lệ. Chỉ chấp nhận 1, 2 hoặc 3.");
            }

            var entity = await _merchantRepo.FindAsync(command.MerchantId);
            var user = entity == null ? null : await _userRepo.FindAsync(entity.Id);
            if (entity == null || user == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy đối tác Merchant!");
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return MerchantWriteGuard.Error<MerchantResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion từ GET /merchant/{id}.");
            if (command.ExpectedVersion != MerchantWriteGuard.Version(entity))
                return MerchantWriteGuard.Error(HttpStatusCode.Conflict, "VersionMismatch", "Hồ sơ đã thay đổi; vui lòng tải lại.", (await ToDtosAsync([entity]))[0]);
            if (entity.Status == command.Status)
            {
                var unchanged = Success((await ToDtosAsync([entity]))[0], "Merchant đã ở trạng thái này.");
                await _writeGuard.RecordAsync(command.OperationId, CurrentUser.Id.Value, entity.Id, "Status", command, unchanged);
                await uow.CompleteAsync();
                return unchanged;
            }

            var now = DateTime.UtcNow;
            var locking = command.Status != MerchantStatus.Active;
            var reason = command.RejectionReason?.Trim();
            if (locking)
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                    return BadRequest<MerchantResultDto>("Vui lòng nhập lý do khóa (tối đa 1000 ký tự).");
                var unfinished = await AsyncExecuter.CountAsync((await _orderRepo.GetQueryableAsync())
                    .Where(o => !o.IsDeleted && o.MerchantId == entity.Id && UnfinishedStatuses.Contains(o.Status)));
                if (unfinished > 0 && !command.Confirm)
                {
                    var dto = (await ToDtosAsync([entity]))[0];
                    return new BaseResponse<MerchantResultDto>
                    {
                        Succeeded = false,
                        StatusCode = HttpStatusCode.Conflict,
                        Data = dto,
                        Message = $"Merchant đang có {unfinished} đơn chưa xử lý. Vẫn muốn khóa?"
                    };
                }
            }

            entity.Status = command.Status;
            MerchantWriteGuard.Touch(entity);
            if (locking)
            {
                entity.RejectionReason = reason;
                user.IsActive = false;
                // Locked accounts cannot refresh their session.
                var tokens = await _tokenRepo.GetListAsync(x => x.UserId == user.Id && !x.IsRevoked);
                foreach (var token in tokens)
                    token.IsRevoked = true;
                if (tokens.Count > 0)
                    await _tokenRepo.UpdateManyAsync(tokens, autoSave: true);
            }
            else
            {
                entity.RejectionReason = null;
                entity.ApprovedBy = CurrentUser.Id;
                entity.ApprovedAt = now;
                user.IsActive = true;
            }
            user.UpdatedAt = now;
            await _merchantRepo.UpdateAsync(entity, autoSave: true);
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _notifications.SendAsync([entity.Id], NotificationType.MerchantStatusChanged,
                locking ? "Tài khoản Merchant đã bị khóa" : "Tài khoản Merchant đã được mở khóa",
                locking ? $"Tài khoản cửa hàng {entity.BusinessName} đã bị Admin khóa. Lý do: {reason}"
                        : $"Tài khoản cửa hàng {entity.BusinessName} đã được Admin kích hoạt lại.",
                "merchant", entity.Id);
            await Audit.LogAsync(new AuditEntry("Merchant", locking ? "Lock" : "Unlock", "Merchant", entity.Id.ToString(), entity.BusinessName)
            {
                Reason = reason, ChangedFields = ["status"], Details = new { status = entity.Status.ToString() }
            });
            var response = Success((await ToDtosAsync([entity]))[0], locking ? "Đã khóa Merchant thành công!" : "Đã mở khóa Merchant thành công!");
            await _writeGuard.RecordAsync(command.OperationId, CurrentUser.Id.Value, entity.Id, "Status", command, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, MerchantFilterQuery query)
        {
            var queryable = await _merchantRepo.GetQueryableAsync();
            var values = await AsyncExecuter.ToListAsync(queryable.Select(x => x.City).Distinct());
            var result = values.Where(v => !string.IsNullOrEmpty(v))
                               .Select(v => new ColumnFilterDistinctValueDto { Value = v, Label = v, Count = 1 })
                               .ToList();
            return Success(result);
        }

        private async Task<string?> LegacySlaUrlAsync(Guid merchantId) =>
            (await _merchantRepo.FindAsync(merchantId))?.SlaDocUrl;

        /// <summary>Distinct cities of Merchants (for the Admin filter), computed in the database.</summary>
        public async Task<BaseResponse<List<string>>> GetCitiesAsync() =>
            Success(await AsyncExecuter.ToListAsync((await _merchantRepo.GetQueryableAsync())
                .Where(x => x.City != null && x.City != "").Select(x => x.City!.Trim()).Distinct().OrderBy(x => x)));

        private async Task<(string? Error, string? Phone)> ValidateAsync(CreateMerchantCommand command, string email, Guid? currentId)
        {
            var name = command.BusinessName?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 200)
                return ("Tên cửa hàng phải có từ 1 đến 200 ký tự.", null);
            if (string.IsNullOrWhiteSpace(command.Address) || command.Address.Trim().Length > 500)
                return ("Địa chỉ không được để trống (tối đa 500 ký tự).", null);
            var phone = MerchantRules.NormalizePhone(command.Phone);
            if (phone == null)
                return (MerchantRules.InvalidPhone, null);
            if (!MerchantRules.ValidCoordinates(command.Latitude, command.Longitude))
                return (MerchantRules.InvalidCoordinates, null);
            if (command.CommissionRate is < 0 or > 100)
                return (MerchantRules.InvalidCommission, null);
            if (command.CommissionRate.HasValue && decimal.Round(command.CommissionRate.Value, 2) != command.CommissionRate.Value)
                return ("Tỷ lệ chiết khấu chỉ được có tối đa 2 chữ số thập phân.", null);
            if ((command.City?.Trim().Length ?? 0) > 100 || (command.District?.Trim().Length ?? 0) > 100)
                return ("Tỉnh/thành phố và quận/huyện tối đa 100 ký tự.", null);
            if (command.ServiceRadiusKm is < 1 or > 100)
                return ("Bán kính phục vụ phải từ 1 đến 100 km.", null);
            if (command.MaxCapacity is < 1 or > 100000)
                return ("Sức chứa tối đa phải lớn hơn 0.", null);
            if (command.DepositAmount < 0 || command.DepositAmount > 9999999999999999.99m ||
                decimal.Round(command.DepositAmount, 2) != command.DepositAmount)
                return ("Tiền ký quỹ không hợp lệ.", null);
            if (command.ContractStartDate.HasValue && command.ContractEndDate.HasValue &&
                MerchantContractRules.NormalizeDate(command.ContractEndDate) < MerchantContractRules.NormalizeDate(command.ContractStartDate))
                return ("Ngày kết thúc hợp đồng phải sau ngày bắt đầu.", null);
            // SlaDocUrl is legacy and read-only: new SLA documents go through slaDocumentId (private, permission-checked).
            if (!string.IsNullOrWhiteSpace(command.SlaDocUrl) && command.SlaDocUrl.Trim() != (currentId.HasValue ? await LegacySlaUrlAsync(currentId.Value) : null))
                return ("SlaDocUrl đã ngừng ghi; hãy upload tài liệu SLA qua /files/documents và gửi slaDocumentId.", null);
            if (command.ServiceAreaIds is { Count: > 0 } areaIds)
            {
                var distinct = areaIds.Distinct().ToList();
                if (await AsyncExecuter.CountAsync((await _areaRepo.GetQueryableAsync()).Where(x => distinct.Contains(x.Id) && x.IsActive)) != distinct.Count)
                    return ("Vùng phục vụ không tồn tại hoặc đã ngừng hoạt động.", null);
            }
            if (await _userRepo.AnyAsync(x => x.Email.ToLower() == email && x.Id != currentId))
                return ("Email này đã được sử dụng.", null);
            var taxCode = string.IsNullOrWhiteSpace(command.TaxCode) ? null : command.TaxCode.Trim();
            if (taxCode != null && (taxCode.Length > 20 || await _merchantRepo.AnyAsync(x => x.TaxCode == taxCode && x.Id != currentId)))
                return (taxCode.Length > 20 ? "Mã số thuế không hợp lệ." : "Mã số thuế này đã được sử dụng.", null);
            return (null, phone);
        }

        private static void Apply(Merchant entity, CreateMerchantCommand command, string phone)
        {
            entity.BusinessName = command.BusinessName.Trim();
            entity.TaxCode = string.IsNullOrWhiteSpace(command.TaxCode) ? null : command.TaxCode.Trim();
            entity.BusinessLicense = MerchantRules.NormalizeJson(command.BusinessLicense);
            entity.Address = command.Address.Trim();
            entity.District = command.District?.Trim() ?? string.Empty;
            entity.City = command.City?.Trim() ?? string.Empty;
            entity.Latitude = command.Latitude;
            entity.Longitude = command.Longitude;
            entity.Phone = phone;
            entity.OpeningHours = MerchantRules.NormalizeJson(command.OpeningHours);
            entity.DepositAmount = command.DepositAmount;
            entity.ServiceRadiusKm = command.ServiceRadiusKm;
            entity.MaxCapacity = command.MaxCapacity;
            entity.ContractStartDate = MerchantContractRules.NormalizeDate(command.ContractStartDate);
            entity.ContractEndDate = MerchantContractRules.NormalizeDate(command.ContractEndDate);
            // Legacy value is kept as is (never written from requests any more).
            entity.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<List<string>> StoreImagesAsync(Guid merchantId) =>
            (await _imageRepo.GetListAsync(x => x.MerchantId == merchantId && x.ImageType == StoreImageType))
                .OrderBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList();

        private async System.Threading.Tasks.Task ReplaceImagesAsync(Guid merchantId, List<string> urls)
        {
            var existing = await _imageRepo.GetListAsync(x => x.MerchantId == merchantId && x.ImageType == StoreImageType);
            if (existing.OrderBy(x => x.DisplayOrder).Select(x => x.SecureUrl).SequenceEqual(urls))
                return;
            await _imageRepo.DeleteManyAsync(existing, autoSave: true);
            await _imageRepo.InsertManyAsync(urls.Select((url, i) => new MerchantImage(GuidGenerator.Create())
            {
                MerchantId = merchantId,
                ImageType = StoreImageType,
                PublicId = url[(url.LastIndexOf('/') + 1)..],
                SecureUrl = url,
                IsPrimary = i == 0,
                DisplayOrder = i
            }), autoSave: true);
        }

        private async System.Threading.Tasks.Task ReplaceAreasAsync(Guid merchantId, List<Guid> areaIds)
        {
            var existing = await _msaRepo.GetListAsync(x => x.MerchantId == merchantId);
            var desired = areaIds.ToHashSet();
            var removed = existing.Where(x => !desired.Contains(x.ServiceAreaId)).ToList();
            if (removed.Count > 0)
                await _msaRepo.DeleteManyAsync(removed, autoSave: true);
            // Unchanged links retain priority, activation and assignment history.
            var retained = existing.Select(x => x.ServiceAreaId).ToHashSet();
            var added = desired.Where(id => !retained.Contains(id)).Select(areaId => new MerchantServiceArea(GuidGenerator.Create())
            {
                MerchantId = merchantId,
                ServiceAreaId = areaId,
                Priority = 1,
                IsActive = true,
                AssignedBy = CurrentUser.Id,
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            if (added.Count > 0)
                await _msaRepo.InsertManyAsync(added, autoSave: true);
        }

        /// <summary>Tổng đơn, doanh thu (đơn đã thanh toán) và số đơn chưa xử lý được tính từ đơn hàng thực tế.</summary>
        private async Task<List<MerchantResultDto>> ToDtosAsync(List<Merchant> merchants)
        {
            var ids = merchants.Select(x => x.Id).ToList();
            var emails = (await _userRepo.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Email);
            var areas = (await _msaRepo.GetListAsync(x => ids.Contains(x.MerchantId) && x.IsActive))
                .GroupBy(x => x.MerchantId).ToDictionary(g => g.Key, g => g.Select(x => x.ServiceAreaId).ToList());
            var images = (await _imageRepo.GetListAsync(x => ids.Contains(x.MerchantId) && x.ImageType == StoreImageType))
                .GroupBy(x => x.MerchantId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList());
            var stats = (await AsyncExecuter.ToListAsync((await _orderRepo.GetQueryableAsync())
                    .Where(o => !o.IsDeleted && o.MerchantId != null && ids.Contains(o.MerchantId.Value))
                    .GroupBy(o => o.MerchantId!.Value)
                    .Select(g => new
                    {
                        MerchantId = g.Key,
                        Total = g.Count(o => o.Status != WashOrderStatus.Cancelled),
                        Revenue = g.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => (decimal?)o.FinalAmount) ?? 0,
                        Unfinished = g.Count(o => UnfinishedStatuses.Contains(o.Status))
                    })))
                .ToDictionary(x => x.MerchantId);

            var dtos = ObjectMapper.Map<List<Merchant>, List<MerchantResultDto>>(merchants);
            var documents = await LazyServiceProvider.LazyGetRequiredService<PrivateDocumentStore>().MetadataAsync(merchants.SelectMany(x => new[] { x.BusinessLicenseDocumentId, x.ContractDocumentId, x.SlaDocumentId }));
            var ratings = await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<WashGo.Domain.Entities.Feedback.OrderRating, Guid>>().GetQueryableAsync())
                .Where(x => x.MerchantId.HasValue && ids.Contains(x.MerchantId.Value) && !x.IsHidden && !x.IsDeleted).GroupBy(x => x.MerchantId).Select(g => new { Id = g.Key, Count = g.Count() }));
            foreach (var dto in dtos)
            {
                dto.Documents = new[] { dto.BusinessLicenseDocumentId, dto.ContractDocumentId, dto.SlaDocumentId }.Where(x => x.HasValue && documents.ContainsKey(x.Value)).Select(x => documents[x!.Value]).ToList();
                dto.PublicRatingCount = ratings.FirstOrDefault(x => x.Id == dto.Id)?.Count ?? 0;
                dto.Version = MerchantWriteGuard.Version(merchants.First(x => x.Id == dto.Id));
                dto.Email = emails.GetValueOrDefault(dto.Id, string.Empty);
                dto.ServiceAreaIds = areas.GetValueOrDefault(dto.Id) ?? [];
                dto.ImageUrls = images.GetValueOrDefault(dto.Id) ?? [];
                var stat = stats.GetValueOrDefault(dto.Id);
                dto.TotalOrders = stat?.Total ?? 0;
                dto.TotalRevenue = stat?.Revenue ?? 0;
                dto.UnfinishedOrders = stat?.Unfinished ?? 0;
            }
            return dtos;
        }

        // UC 2.2.1: Merchant Dashboard
        public async Task<BaseResponse<MerchantDashboardDto>> GetDashboardAsync(MerchantDashboardQuery query)
        {
            var merchantId = CurrentUser.Id;
            if (!merchantId.HasValue)
                return BadRequest<MerchantDashboardDto>("Không xác định được danh tính Merchant.");

            var (period, fromUtc, toUtc, _, _, error) = DateTimePeriodHelper.ResolvePeriod(query.Period, query.FromDate, query.ToDate, "all");
            if (error != null)
                return BadRequest<MerchantDashboardDto>(error);
            // Counts, latest orders and settlements are read in one repeatable-read transaction: one consistent snapshot.
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);

            var ordersQuery = (await _orderRepo.GetQueryableAsync())
                .Where(o => !o.IsDeleted && o.MerchantId == merchantId.Value);

            if (fromUtc.HasValue && toUtc.HasValue)
            {
                var fromSec = ((DateTimeOffset)fromUtc.Value).ToUnixTimeSeconds();
                var toSec = ((DateTimeOffset)toUtc.Value).ToUnixTimeSeconds();
                ordersQuery = ordersQuery.Where(o => o.CreatedOn >= fromSec && o.CreatedOn <= toSec);
            }

            // Thống kê phân bố theo 14 trạng thái
            var countsByStatus = (await AsyncExecuter.ToListAsync(
                ordersQuery
                    .GroupBy(o => o.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
            )).ToDictionary(x => x.Status, x => x.Count);

            var statusCounts = new MerchantStatusCountsDto
            {
                Pending = countsByStatus.GetValueOrDefault(WashOrderStatus.Pending),
                Deposited = countsByStatus.GetValueOrDefault(WashOrderStatus.Deposited),
                Collected = countsByStatus.GetValueOrDefault(WashOrderStatus.Collected),
                Washing = countsByStatus.GetValueOrDefault(WashOrderStatus.Washing),
                Drying = countsByStatus.GetValueOrDefault(WashOrderStatus.Drying),
                Ironing = countsByStatus.GetValueOrDefault(WashOrderStatus.Ironing),
                Folding = countsByStatus.GetValueOrDefault(WashOrderStatus.Folding),
                AwaitingPayment = countsByStatus.GetValueOrDefault(WashOrderStatus.AwaitingPayment),
                Paid = countsByStatus.GetValueOrDefault(WashOrderStatus.Paid),
                ReadyToReturn = countsByStatus.GetValueOrDefault(WashOrderStatus.ReadyToReturn),
                Returned = countsByStatus.GetValueOrDefault(WashOrderStatus.Returned),
                Completed = countsByStatus.GetValueOrDefault(WashOrderStatus.Completed),
                MovedToStorage = countsByStatus.GetValueOrDefault(WashOrderStatus.MovedToStorage),
                Cancelled = countsByStatus.GetValueOrDefault(WashOrderStatus.Cancelled)
            };

            var totalOrders = countsByStatus.Values.Sum();

            // Lấy 5 đơn mới nhất của tiệm
            var latestOrdersEntities = await AsyncExecuter.ToListAsync(
                ordersQuery.OrderByDescending(o => o.CreatedOn).Take(5)
            );

            var latestDtos = latestOrdersEntities.Select(o =>
            {
                var dto = ObjectMapper.Map<WashOrder, WashOrderResultDto>(o);
                dto.RedactForMerchant();
                return dto;
            }).ToList();

            if (latestDtos.Count > 0)
            {
                await _enricher.FillAsync(latestDtos, "Merchant");
            }

            // Lấy tối đa 4 kỳ đối soát gần nhất của tiệm
            var settlementsQuery = (await _settlementRepo.GetQueryableAsync())
                .Where(s => !s.IsDeleted && s.MerchantId == merchantId.Value)
                .OrderByDescending(s => s.PeriodTo)
                .ThenByDescending(s => s.CreatedOn)
                .Take(4);

            var recentSettlements = (await AsyncExecuter.ToListAsync(settlementsQuery))
                .Select(s => new SettlementDto
                {
                    Id = s.Id,
                    MerchantId = s.MerchantId,
                    MerchantName = s.MerchantName,
                    PeriodFrom = s.PeriodFrom,
                    PeriodTo = s.PeriodTo,
                    OrderCount = s.OrderCount,
                    GrossRevenue = s.GrossRevenue,
                    RefundedAmount = s.RefundedAmount,
                    NetRevenue = s.NetRevenue,
                    CommissionRate = s.CommissionRate,
                    CommissionAmount = s.CommissionAmount,
                    PayoutAmount = s.PayoutAmount,
                    Status = s.Status,
                    PaymentReference = s.PaymentReference,
                    PaidAt = s.PaidAt,
                    CreatedOn = s.CreatedOn
                }).ToList();

            var result = new MerchantDashboardDto
            {
                Period = period,
                FromDate = fromUtc,
                ToDate = toUtc,
                TotalOrders = totalOrders,
                StatusCounts = statusCounts,
                LatestOrders = latestDtos,
                RecentSettlements = recentSettlements,
                GeneratedAt = DateTime.UtcNow
            };
            await snapshot.CompleteAsync();
            return Success(result);
        }

        // UC 2.2.3: Báo cáo Doanh thu & Tăng trưởng Merchant
        public async Task<BaseResponse<MerchantRevenueReportDto>> GetRevenueReportAsync(MerchantRevenueReportQuery query)
        {
            var merchantId = CurrentUser.Id;
            if (!merchantId.HasValue)
                return BadRequest<MerchantRevenueReportDto>("Không xác định được danh tính Merchant.");
            if (await _merchantRepo.FindAsync(merchantId.Value) == null)
                return NotFound<MerchantRevenueReportDto>("Không tìm thấy thông tin Merchant.");

            var (period, fromUtc, toUtc, prevFromUtc, prevToUtc, error) = DateTimePeriodHelper.ResolvePeriod(query.Period, query.FromDate, query.ToDate, "month");
            if (error != null)
                return BadRequest<MerchantRevenueReportDto>(error);
            // Same definitions as /merchant-reports: money by transaction/refund date, commission at each order's locked rate.
            var finance = LazyServiceProvider.LazyGetRequiredService<WashGo.Application.Features.MerchantReportApplication.Services.MerchantFinanceAggregator>();
            DateTime? Exclusive(DateTime? inclusiveEnd) => inclusiveEnd?.AddTicks(1);
            // Current, previous and series totals are read from one repeatable-read snapshot.
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var current = await finance.TotalsAsync(merchantId.Value, fromUtc, Exclusive(toUtc));
            var series = await finance.SeriesAsync(merchantId.Value, fromUtc, Exclusive(toUtc), hourly: false);
            var orders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted && o.MerchantId == merchantId.Value && o.CancelledAt != null);
            if (fromUtc.HasValue) orders = orders.Where(o => o.CancelledAt >= fromUtc.Value);
            if (toUtc.HasValue) orders = orders.Where(o => o.CancelledAt <= toUtc.Value);
            var cancelled = await AsyncExecuter.CountAsync(orders);

            decimal? previousPayout = null;
            decimal? growth = null;
            var isNew = false;
            if (prevFromUtc.HasValue && prevToUtc.HasValue)
            {
                var previous = await finance.TotalsAsync(merchantId.Value, prevFromUtc, Exclusive(prevToUtc));
                previousPayout = previous.Payout;
                // No baseline: growth is undefined (null + isNewRevenue), never a made-up 100%.
                if (previous.Payout != 0m)
                    growth = Math.Round((current.Payout - previous.Payout) / Math.Abs(previous.Payout) * 100m, 1);
                else if (current.Payout == 0m)
                    growth = 0m;
                else
                    isNew = true;
                if (!current.CommissionComplete || !previous.CommissionComplete)
                    growth = null;
            }

            return Success(new MerchantRevenueReportDto
            {
                Period = period,
                FromDate = fromUtc,
                ToDate = toUtc,
                TotalGrossRevenue = current.Gross,
                TotalCommission = current.Commission,
                TotalPayoutAmount = current.Payout,
                RefundedAmount = current.Refunds,
                PaidOrderCount = current.PaidOrders,
                CancelledOrderCount = cancelled,
                PreviousPayoutAmount = previousPayout,
                GrowthRate = growth,
                IsNewRevenue = isNew,
                CommissionComplete = current.CommissionComplete,
                OrdersWithoutCommissionSnapshot = current.UncoveredOrders,
                DailySeries = series.Select(b => new MerchantDailyRevenueDto
                {
                    Date = b.LocalStart, GrossRevenue = b.Gross, Commission = b.Commission,
                    Payout = b.Gross - b.Refunds - b.Commission, OrderCount = b.PaidOrders
                }).ToList(),
                GeneratedAt = DateTime.UtcNow
            });
        }

        // UC 2.2.2: Cảnh báo vi phạm SLA (Overdue Attention)
        public async Task<BaseResponse<MerchantSlaAlertsDto>> GetSlaAlertsAsync(MerchantSlaAlertsQuery query)
        {
            var merchantId = CurrentUser.Id;
            if (!merchantId.HasValue)
                return BadRequest<MerchantSlaAlertsDto>("Không xác định được danh tính Merchant.");

            var settings = await _settings.GetAsync();
            int collectSlaHours = settings.MerchantCollectSlaHours > 0 ? settings.MerchantCollectSlaHours : 4;
            int returnSlaHours = settings.MerchantReturnSlaHours > 0 ? settings.MerchantReturnSlaHours : 24;
            var page = Math.Max(1, query.Page ?? 1);
            var take = Math.Clamp(query.Take ?? 20, 1, 100);
            var now = DateTime.UtcNow;
            var collectBefore = now.AddHours(-collectSlaHours);
            var returnBefore = now.AddHours(-returnSlaHours);

            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var orders = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted && o.MerchantId == merchantId.Value);
            // Same rules as before, evaluated by the database for the whole scope; only the requested page is loaded.
            var collection = orders.Where(o => o.Status == WashOrderStatus.Deposited && o.DepositedAt != null && o.DepositedAt.Value.AddHours(o.CollectSlaHoursSnapshot ?? collectSlaHours) < now)
                .Select(o => new SlaRow { Id = o.Id, Type = MerchantSlaAlertType.CollectionOverdue, Deadline = o.DepositedAt!.Value.AddHours(o.CollectSlaHoursSnapshot ?? collectSlaHours) });
            var processing = orders.Where(o => (o.Status == WashOrderStatus.Washing || o.Status == WashOrderStatus.Drying ||
                                                o.Status == WashOrderStatus.Ironing || o.Status == WashOrderStatus.Folding) &&
                                               o.EstimatedFinishAt != null && o.EstimatedFinishAt < now)
                .Select(o => new SlaRow { Id = o.Id, Type = MerchantSlaAlertType.ProcessingOverdue, Deadline = o.EstimatedFinishAt!.Value });
            var returns = orders.Where(o => o.Status == WashOrderStatus.Paid && o.PaidAt != null && o.PaidAt.Value.AddHours(o.ReturnSlaHoursSnapshot ?? returnSlaHours) < now)
                .Select(o => new SlaRow { Id = o.Id, Type = MerchantSlaAlertType.ReturnOverdue, Deadline = o.PaidAt!.Value.AddHours(o.ReturnSlaHoursSnapshot ?? returnSlaHours) });
            var collectionCount = await AsyncExecuter.CountAsync(collection);
            var processingCount = await AsyncExecuter.CountAsync(processing);
            var returnCount = await AsyncExecuter.CountAsync(returns);
            var all = query.AlertType switch
            {
                MerchantSlaAlertType.CollectionOverdue => collection,
                MerchantSlaAlertType.ProcessingOverdue => processing,
                MerchantSlaAlertType.ReturnOverdue => returns,
                _ => collection.Concat(processing).Concat(returns)
            };
            var rows = await AsyncExecuter.ToListAsync(all.OrderBy(x => x.Deadline).ThenBy(x => x.Id).Skip((page - 1) * take).Take(take));
            var ids = rows.Select(x => x.Id).ToList();
            var details = (await AsyncExecuter.ToListAsync(orders.Where(o => ids.Contains(o.Id))
                    .Select(o => new { o.Id, o.OrderCode, o.MerchantOrderCode, o.Status, o.LockerName, o.BoxNumber })))
                .ToDictionary(x => x.Id);
            await snapshot.CompleteAsync();

            return Success(new MerchantSlaAlertsDto
            {
                CollectionAlertsCount = collectionCount,
                ProcessingAlertsCount = processingCount,
                ReturnAlertsCount = returnCount,
                Page = page,
                Take = take,
                PriorityTotal = query.AlertType switch
                {
                    MerchantSlaAlertType.CollectionOverdue => collectionCount,
                    MerchantSlaAlertType.ProcessingOverdue => processingCount,
                    MerchantSlaAlertType.ReturnOverdue => returnCount,
                    _ => collectionCount + processingCount + returnCount
                },
                PriorityOrders = rows.Where(x => details.ContainsKey(x.Id)).Select(x =>
                {
                    var o = details[x.Id];
                    return new MerchantPriorityOrderDto
                    {
                        OrderId = o.Id, OrderCode = o.OrderCode, MerchantOrderCode = o.MerchantOrderCode, Status = o.Status,
                        AlertType = x.Type, Deadline = DateTime.SpecifyKind(x.Deadline, DateTimeKind.Utc),
                        OverdueMinutes = (int)(now - x.Deadline).TotalMinutes, LockerName = o.LockerName, BoxNumber = o.BoxNumber
                    };
                }).ToList(),
                ServerNow = now
            });
        }

        private sealed class SlaRow
        {
            public Guid Id { get; init; }
            public MerchantSlaAlertType Type { get; init; }
            public DateTime Deadline { get; init; }
        }
    }
}
