using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Application.Features.AuthApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using PasswordHasher = global::BCrypt.Net.BCrypt;

namespace WashGo.Application.Features.ShipperApplication.Services
{
    /// <summary>
    /// UC 2.4.1–2.4.3: Merchant xem, thêm, sửa Shipper của tiệm mình (Admin quản lý được mọi tiệm).
    /// Tạo Shipper = tạo tài khoản role Shipper + hồ sơ, gửi mật khẩu tạm qua email.
    /// </summary>
    public class ShipperManagementAppService : WashGoAppService, IShipperManagementAppService
    {
        private static readonly Regex IdentityPattern = new(@"^(\d{9}|\d{12})$", RegexOptions.Compiled);

        private readonly IShipperRepository _shipperRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Role, Guid> _roleRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IAuthEmailSender _emailSender;
        private readonly IRepository<RefreshToken, Guid> _refreshTokenRepo;
        private readonly IRepository<ShipperAudit, Guid> _auditRepo;
        private readonly IDataFilter _dataFilter;
        private readonly IRepository<DeliveryTask, Guid> _taskRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ShipperManagementAppService> _logger;

        public ShipperManagementAppService(
            IShipperRepository shipperRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Role, Guid> roleRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IWashOrderRepository orderRepo,
            IAuthEmailSender emailSender,
            IRepository<RefreshToken, Guid> refreshTokenRepo,
            IRepository<ShipperAudit, Guid> auditRepo,
            IDataFilter dataFilter,
            IRepository<DeliveryTask, Guid> taskRepo,
            ILockerRepository lockerRepo,
            IConfiguration configuration,
            ILogger<ShipperManagementAppService> logger)
        {
            _shipperRepo = shipperRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _merchantRepo = merchantRepo;
            _orderRepo = orderRepo;
            _emailSender = emailSender;
            _refreshTokenRepo = refreshTokenRepo;
            _auditRepo = auditRepo;
            _dataFilter = dataFilter;
            _taskRepo = taskRepo;
            _lockerRepo = lockerRepo;
            _configuration = configuration;
            _logger = logger;
        }

        private bool IsAdmin => CurrentUser.IsInRole("Admin");

        // Merchant accounts share their id with the Merchant profile.
        private Guid? ScopeMerchantId(Guid? requested) => IsAdmin ? requested : CurrentUser.Id;

        public async Task<PaginatedResult<ShipperDto>> GetListAsync(ShipperFilterQuery query)
        {
            var merchantId = ScopeMerchantId(query.MerchantId);
            var shippers = await _shipperRepo.GetQueryableAsync();
            var users = await _userRepo.GetQueryableAsync();
            var rows = from shipper in shippers
                       join user in users on shipper.UserId equals user.Id
                       select new { shipper, user };
            if (merchantId.HasValue)
                rows = rows.Where(x => x.shipper.MerchantId == merchantId.Value);
            if (query.Status.HasValue)
                rows = rows.Where(x => x.shipper.Status == query.Status.Value);
            if (query.IsActive.HasValue)
                rows = rows.Where(x => x.shipper.IsActive == query.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                rows = rows.Where(x => x.user.FullName.ToLower().Contains(kw) || x.user.Email.ToLower().Contains(kw) ||
                    (x.user.Phone != null && x.user.Phone.Contains(kw)) ||
                    (x.shipper.VehiclePlate != null && x.shipper.VehiclePlate.ToLower().Contains(kw)));
            }

            var sorts = query.SortBy ?? [];
            // Shipper star ratings were dropped: sortBy is no longer supported; a legacy Rating sort is ignored (default order).
            if (sorts.Any(x => !string.Equals(x.Field, "Rating", StringComparison.OrdinalIgnoreCase)))
                return new PaginatedResult<ShipperDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.BadRequest,
                    Message = "Danh sách Shipper không hỗ trợ sắp xếp tùy chọn.", CurrentPage = query.Page, PageSize = query.Take
                };
            sorts = [];

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(rows);
            // Sorted over the whole filtered set before paging; unrated (0) last in both directions, then name, then id.
            var ordered = sorts.Length == 0
                ? rows.OrderByDescending(x => x.shipper.IsActive).ThenBy(x => x.user.FullName).ThenBy(x => x.shipper.Id)
                : sorts[0].IsDescending
                    ? rows.OrderBy(x => x.shipper.Rating <= 0).ThenByDescending(x => x.shipper.Rating)
                        .ThenBy(x => x.user.FullName).ThenBy(x => x.shipper.Id)
                    : rows.OrderBy(x => x.shipper.Rating <= 0).ThenBy(x => x.shipper.Rating)
                        .ThenBy(x => x.user.FullName).ThenBy(x => x.shipper.Id);
            var items = await AsyncExecuter.ToListAsync(ordered.Skip((page - 1) * take).Take(take));
            var activeCounts = await ActiveTaskCountsAsync(items.Select(x => x.shipper.Id).ToList());
            var dtos = items.Select(x => ToDto(x.shipper, x.user, activeCounts.GetValueOrDefault(x.shipper.Id))).ToList();
            return Success(dtos, total, page, take, total == 0 ? "Bạn chưa có Shipper nào." : null);
        }

        public async Task<BaseResponse<ShipperDto>> GetByIdAsync(Guid id)
        {
            var (shipper, user) = await FindOwnedAsync(id);
            if (shipper == null || user == null)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");
            var active = await ActiveTaskCountsAsync([shipper.Id]);
            return Success(ToDto(shipper, user, active.GetValueOrDefault(shipper.Id)));
        }

        public async Task<BaseResponse<ShipperSummaryDto>> GetSummaryAsync(Guid? merchantId)
        {
            var scope = ScopeMerchantId(merchantId);
            var shippers = await _shipperRepo.GetQueryableAsync();
            if (scope.HasValue)
                shippers = shippers.Where(x => x.MerchantId == scope.Value);
            var users = await _userRepo.GetQueryableAsync();
            // One grouped query so every number comes from the same moment.
            var rows = await AsyncExecuter.ToListAsync((from shipper in shippers
                                                        join user in users on shipper.UserId equals user.Id
                                                        select new { shipper.IsActive, shipper.Status, Locked = !user.IsActive })
                .GroupBy(x => new { x.IsActive, x.Status, x.Locked })
                .Select(g => new { g.Key.IsActive, g.Key.Status, g.Key.Locked, Count = g.Count() }));
            return Success(new ShipperSummaryDto
            {
                Total = rows.Sum(x => x.Count),
                Working = rows.Where(x => x.IsActive).Sum(x => x.Count),
                OnLeave = rows.Where(x => !x.IsActive).Sum(x => x.Count),
                Available = rows.Where(x => x.Status == ShipperStatus.Available).Sum(x => x.Count),
                Busy = rows.Where(x => x.Status == ShipperStatus.Busy).Sum(x => x.Count),
                Offline = rows.Where(x => x.Status == ShipperStatus.Offline).Sum(x => x.Count),
                Locked = rows.Where(x => x.Locked).Sum(x => x.Count),
                GeneratedAt = DateTime.UtcNow
            });
        }

        public async Task<BaseResponse<ShipperDto>> CreateAsync(CreateShipperCommand command)
        {
            var merchantId = ScopeMerchantId(command.MerchantId);
            if (!merchantId.HasValue)
                return BadRequest<ShipperDto>("Vui lòng chọn Merchant quản lý Shipper.");
            if (command.ClientRequestId == Guid.Empty)
                return BadRequest<ShipperDto>("clientRequestId không hợp lệ.");
            if (command.ClientRequestId.HasValue && await ReplayCreateAsync(merchantId.Value, command) is { } replay)
                return replay;
            var merchant = await _merchantRepo.FindAsync(merchantId.Value);
            if (merchant == null || merchant.Status != MerchantStatus.Active)
                return BadRequest<ShipperDto>("Merchant không tồn tại hoặc không hoạt động.");

            var (error, fullName, phone, email, plate) = ShipperRules.Validate(command.FullName, command.Phone, command.Email,
                command.VehicleType, command.VehiclePlate);
            if (error != null)
                return BadRequest<ShipperDto>(error);
            var identity = string.IsNullOrWhiteSpace(command.IdentityNumber) ? null : command.IdentityNumber.Trim();
            if (identity != null && !IdentityPattern.IsMatch(identity))
                return BadRequest<ShipperDto>("Số CMND/CCCD phải gồm 9 hoặc 12 chữ số.");
            var front = Clean(command.IdentityFrontImageUrl);
            var back = Clean(command.IdentityBackImageUrl);
            if ((front == null) != (back == null))
                return BadRequest<ShipperDto>("Ảnh CMND/CCCD cần đủ mặt trước và mặt sau (hoặc bỏ trống cả hai).");
            if (front != null && command.IdentityImageUrls.Any(x => !string.IsNullOrWhiteSpace(x)))
                return BadRequest<ShipperDto>("Dùng identityFrontImageUrl/identityBackImageUrl hoặc identityImageUrls, không dùng cả hai.");
            if (front != null && front == back)
                return BadRequest<ShipperDto>("Ảnh mặt trước và mặt sau phải khác nhau.");
            if (front != null && await ValidateSidesAsync(front, back!) is { } sideError)
                return BadRequest<ShipperDto>(sideError);
            var (imageError, images) = await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), command.IdentityImageUrls,
                CurrentUser.Id!.Value, 0);
            if (imageError != null)
                return BadRequest<ShipperDto>(imageError);
            // The advisory lock only lives inside a transaction: hold one (gate) until the account is committed, so a concurrent
            // send with the same key waits and is then answered by the replay below instead of a duplicate-phone error.
            using var gate = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.AcquireLockAsync($"shipper-phone:{phone}");
            if (command.ClientRequestId.HasValue && await ReplayCreateAsync(merchantId.Value, command) is { } lockedReplay)
                return lockedReplay;
            var duplicate = await DuplicateErrorAsync(phone, email, null);
            if (duplicate != null)
                return BadRequest<ShipperDto>(duplicate);
            var role = await _roleRepo.FirstOrDefaultAsync(x => x.Name == "Shipper");
            if (role == null)
                return BadRequest<ShipperDto>("Có lỗi xảy ra. Vui lòng thử lại.");

            var temporaryPassword = TemporaryPassword.Generate();
            var user = new User(GuidGenerator.Create())
            {
                RoleId = role.Id,
                Email = email,
                PasswordHash = PasswordHasher.HashPassword(temporaryPassword),
                AuthProvider = AuthProvider.Local,
                FullName = fullName,
                Phone = phone,
                IsActive = true
            };
            var shipper = new Shipper(GuidGenerator.Create())
            {
                UserId = user.Id,
                MerchantId = merchantId.Value,
                VehicleType = command.VehicleType,
                VehiclePlate = plate,
                IdentityNumber = identity,
                IdentityImages = images.Count == 0 ? null : JsonSerializer.Serialize(images),
                IdentityFrontImageUrl = front,
                IdentityBackImageUrl = back,
                Status = ShipperStatus.Available,
                IsActive = true
            };

            shipper.CreateRequestId = command.ClientRequestId;
            try
            {
                using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                await _userRepo.InsertAsync(user, autoSave: true);
                await _shipperRepo.InsertAsync(shipper, autoSave: true);
                await uow.CompleteAsync();
            }
            catch (Exception) when (command.ClientRequestId.HasValue)
            {
                // A concurrent send with the same key won the unique index: answer with that shipper.
                if (await ReplayCreateAsync(merchantId.Value, command) is { } winner)
                    return winner;
                throw;
            }
            await gate.CompleteAsync();

            // Spec 6a: a failed delivery is logged but the account stays created; the outcome is stored as data.
            var sent = await _emailSender.SendAccountCredentialsAsync(email, fullName, temporaryPassword);
            if (!sent)
                _logger.LogWarning("Could not send Shipper credentials to {Email}.", email);
            using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var saved = await _shipperRepo.GetAsync(shipper.Id);
                saved.CredentialDelivery = sent ? CredentialDeliveryStatus.Sent : CredentialDeliveryStatus.Failed;
                saved.CredentialSentAt = sent ? DateTime.UtcNow : null;
                await _shipperRepo.UpdateAsync(saved, autoSave: true);
                await uow.CompleteAsync();
                shipper = saved;
            }
            await Audit.LogAsync(new AuditEntry("Shipper", "Create", "Shipper", shipper.Id.ToString(), user.FullName)
            {
                Details = new { merchantId = shipper.MerchantId, email = user.Email, credentialEmailSent = sent }
            });
            return Created(ToDto(shipper, user, 0), sent
                ? "Thêm Shipper thành công! Thông tin đăng nhập đã được gửi qua email."
                : "Thêm Shipper thành công! Chưa gửi được email thông tin đăng nhập; Shipper có thể dùng Quên mật khẩu.");
        }

        // Same key from the same Merchant: return the created shipper (no new account, no second email).
        private async Task<BaseResponse<ShipperDto>?> ReplayCreateAsync(Guid merchantId, CreateShipperCommand command)
        {
            var existing = await _shipperRepo.FirstOrDefaultAsync(x => x.MerchantId == merchantId && x.CreateRequestId == command.ClientRequestId);
            if (existing == null)
                return null;
            var user = await _userRepo.GetAsync(existing.UserId);
            var dto = await ToDtoAsync(existing, user);
            if (!string.Equals(user.Email, command.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
                return new BaseResponse<ShipperDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.Conflict, Data = dto, Meta = new { code = "ClientRequestIdReused" },
                    Message = "clientRequestId này đã dùng để tạo một Shipper khác."
                };
            return new BaseResponse<ShipperDto>
            {
                Succeeded = true, StatusCode = HttpStatusCode.OK, Data = dto, Meta = new { code = "Replayed", replayed = true },
                Message = "Shipper này đã được tạo trước đó; không tạo thêm tài khoản và không gửi lại email."
            };
        }

        public async Task<BaseResponse<ShipperDto>> UpdateAsync(Guid id, UpdateShipperCommand command)
        {
            // Row lock: a change to Tạm nghỉ and a new assignment of this shipper cannot cross.
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var shipper = await _shipperRepo.GetForUpdateAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (user == null)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");
            if (command.ExpectedRevision.HasValue && command.ExpectedRevision.Value != shipper.Revision)
                return new BaseResponse<ShipperDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.Conflict, Data = await ToDtoAsync(shipper, user),
                    Meta = new { code = "RevisionMismatch", currentRevision = shipper.Revision },
                    Message = "Thông tin Shipper đã được người khác thay đổi; vui lòng tải lại trước khi lưu."
                };

            var (error, fullName, phone, email, plate) = ShipperRules.Validate(command.FullName, command.Phone, command.Email,
                command.VehicleType, command.VehiclePlate);
            if (error != null)
                return BadRequest<ShipperDto>(error);
            await _orderRepo.AcquireLockAsync($"shipper-phone:{phone}");
            var duplicate = await DuplicateErrorAsync(phone, email, user.Id);
            if (duplicate != null)
                return BadRequest<ShipperDto>(duplicate);
            var active = (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id);
            if (!command.IsActive && shipper.IsActive && active > 0)
                return BadRequest<ShipperDto>("Shipper đang có nhiệm vụ lấy/trả chưa hoàn tất; không thể chuyển sang Tạm nghỉ.");
            var (identityError, identityChanges) = await ApplyIdentityAsync(shipper, command);
            if (identityError != null)
                return BadRequest<ShipperDto>(identityError);

            user.FullName = fullName;
            user.Phone = phone;
            user.Email = email;
            user.UpdatedAt = DateTime.UtcNow;
            shipper.VehicleType = command.VehicleType;
            shipper.VehiclePlate = plate;
            shipper.IsActive = command.IsActive;
            shipper.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
            if (identityChanges.Count > 0)
                await AuditAsync(shipper, ShipperAuditAction.IdentityUpdated, null, string.Join(",", identityChanges));
            await uow.CompleteAsync();
            await Audit.LogAsync(new AuditEntry("Shipper", "Update", "Shipper", shipper.Id.ToString(), user.FullName)
            {
                Details = new { merchantId = shipper.MerchantId }
            });
            return Success(ToDto(shipper, user, active), "Cập nhật Shipper thành công!");
        }

        public async Task<BaseResponse<ShipperDto>> LockAsync(Guid id, ShipperAccountActionCommand command)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 500)
                return BadRequest<ShipperDto>("Vui lòng nhập lý do khóa tài khoản (tối đa 500 ký tự).");
            return (await LockCoreAsync(id, reason)).ToResponse();
        }

        public async Task<BaseResponse<ShipperDto>> UnlockAsync(Guid id, ShipperAccountActionCommand command)
        {
            var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
            if (reason?.Length > 500)
                return BadRequest<ShipperDto>("Lý do tối đa 500 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var shipper = await _shipperRepo.GetForUpdateAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (user == null)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");
            if (user.IsActive)
            {
                await uow.CompleteAsync();
                return Success(await ToDtoAsync(shipper, user), "Tài khoản Shipper đang hoạt động, không cần mở khóa.");
            }
            // Unlocking restores sign-in only: working/operational status and old tasks are left as they are.
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            shipper.LockedAt = null;
            shipper.LockReason = null;
            shipper.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
            await AuditAsync(shipper, ShipperAuditAction.Unlocked, reason, null);
            await uow.CompleteAsync();
            return Success(await ToDtoAsync(shipper, user), "Đã mở khóa tài khoản Shipper.");
        }

        public async Task<BaseResponse<List<ShipperAuditDto>>> GetAuditsAsync(Guid id)
        {
            Shipper? shipper;
            using (_dataFilter.Disable<ISoftDelete>())
                shipper = await _shipperRepo.FindAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return NotFound<List<ShipperAuditDto>>("Không tìm thấy Shipper!");
            var rows = await AsyncExecuter.ToListAsync((await _auditRepo.GetQueryableAsync())
                .Where(x => x.ShipperId == id).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));
            return Success(rows.Select(x => new ShipperAuditDto
            {
                Id = x.Id, Action = x.Action, Reason = x.Reason, ActorId = x.ActorId, ActorRole = x.ActorRole,
                ActorName = x.ActorName, Details = x.Details, CreatedAt = x.CreatedAt
            }).ToList());
        }

        public const int MaxBatchSize = 50;

        public async Task<PaginatedResult<ShipperTaskDto>> GetTasksAsync(Guid id, ShipperTaskQuery query)
        {
            Shipper? shipper;
            using (_dataFilter.Disable<ISoftDelete>())
                shipper = await _shipperRepo.FindAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return new PaginatedResult<ShipperTaskDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.NotFound, Message = "Không tìm thấy Shipper!",
                    CurrentPage = query.Page, PageSize = query.Take
                };
            var orders = await _orderRepo.GetQueryableAsync();
            var rows = from task in await _taskRepo.GetQueryableAsync()
                       join order in orders on task.OrderId equals order.Id
                       where task.ShipperId == id
                       select new { task, order };
            // A Merchant only sees tasks on its own orders, even for its own shipper.
            if (!IsAdmin)
                rows = rows.Where(x => x.order.MerchantId == CurrentUser.Id);
            if (query.TaskType.HasValue)
                rows = rows.Where(x => x.task.TaskType == query.TaskType.Value);
            if (query.Status.HasValue)
                rows = rows.Where(x => x.task.Status == query.Status.Value);

            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(rows);
            var items = await AsyncExecuter.ToListAsync(rows
                .OrderByDescending(x => x.task.CompletedAt ?? x.task.EndedAt ?? x.task.AssignedAt)
                .ThenByDescending(x => x.task.Id)
                .Skip((page - 1) * take).Take(take));
            var lockerIds = items.Where(x => x.order.ReturnLockerId.HasValue).Select(x => x.order.ReturnLockerId!.Value).Distinct().ToList();
            var lockerNames = (await _lockerRepo.GetListAsync(x => lockerIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
            var list = items.Select(x =>
            {
                var collect = x.task.TaskType == DeliveryTaskType.Collect;
                return new ShipperTaskDto
                {
                    TaskId = x.task.Id, OrderId = x.order.Id, OrderCode = x.order.OrderCode,
                    MerchantOrderCode = x.order.MerchantOrderCode, TaskType = x.task.TaskType, Status = x.task.Status,
                    AssignedAt = x.task.AssignedAt, CompletedAt = x.task.CompletedAt, EndedAt = x.task.EndedAt,
                    LockerName = collect || !x.order.ReturnLockerId.HasValue
                        ? x.order.LockerName
                        : lockerNames.GetValueOrDefault(x.order.ReturnLockerId.Value, x.order.LockerName),
                    BoxNumber = collect ? x.order.BoxNumber : x.order.ReturnBoxNumber ?? x.order.BoxNumber,
                    OrderStatus = x.order.Status
                };
            }).ToList();
            return Success(list, total, page, take, total == 0 ? "Shipper chưa có nhiệm vụ nào." : null);
        }

        public async Task<BaseResponse<ShipperDto>> DeleteAsync(Guid id, string? reason)
        {
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (reason == null || reason.Length > 500)
                return BadRequest<ShipperDto>("Vui lòng nhập lý do xóa Shipper (tối đa 500 ký tự).");
            return (await DeleteCoreAsync(id, reason)).ToResponse();
        }

        public Task<BaseResponse<ShipperBatchResultDto>> BatchLockAsync(ShipperBatchActionCommand command) =>
            BatchAsync(command, "khóa", LockCoreAsync);

        public Task<BaseResponse<ShipperBatchResultDto>> BatchDeleteAsync(ShipperBatchActionCommand command) =>
            BatchAsync(command, "xóa", DeleteCoreAsync);

        // Partial success: every shipper runs the single-item rule in its own transaction and gets its own result.
        private async Task<BaseResponse<ShipperBatchResultDto>> BatchAsync(ShipperBatchActionCommand command, string verb,
            Func<Guid, string, Task<AccountActionResult>> action)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length > 500)
                return BadRequest<ShipperBatchResultDto>($"Vui lòng nhập lý do {verb} (tối đa 500 ký tự).");
            var requested = command.ShipperIds ?? [];
            var ids = requested.Where(x => x != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0 || requested.Count > MaxBatchSize)
                return BadRequest<ShipperBatchResultDto>($"Chọn từ 1 đến {MaxBatchSize} Shipper.");
            var result = new ShipperBatchResultDto { Requested = requested.Count, DuplicatesIgnored = requested.Count - ids.Count - requested.Count(x => x == Guid.Empty) };
            foreach (var id in ids)
            {
                var item = await action(id, reason);
                result.Results.Add(new ShipperBatchItemDto
                {
                    ShipperId = id, Succeeded = item.Status == HttpStatusCode.OK, Code = item.Code, Message = item.Message
                });
            }
            result.Succeeded = result.Results.Count(x => x.Succeeded);
            result.Failed = result.Results.Count - result.Succeeded;
            return Success(result, $"Đã {verb} {result.Succeeded}/{result.Results.Count} Shipper.");
        }

        // Soft delete: hidden from lists/assignment, sign-in revoked; orders and delivery history keep the shipper.
        // Identity data stays on the hidden row for reconciliation and is no longer returned by any API.
        private async Task<AccountActionResult> DeleteCoreAsync(Guid id, string reason)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            Shipper? shipper;
            using (_dataFilter.Disable<ISoftDelete>())
                shipper = await _shipperRepo.GetForUpdateAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return new(HttpStatusCode.NotFound, "NotFound", "Không tìm thấy Shipper!", null);
            if (shipper.IsDeleted)
            {
                await uow.CompleteAsync();
                return new(HttpStatusCode.OK, "AlreadyDeleted", "Shipper đã được xóa trước đó.", null);
            }
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (user == null || !await _roleRepo.AnyAsync(x => x.Id == user.RoleId && x.Name == "Shipper"))
                return new(HttpStatusCode.BadRequest, "NotShipper", "Tài khoản này không phải tài khoản Shipper.", null);
            var active = (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id);
            if (active > 0)
                return new(HttpStatusCode.Conflict, "HasActiveTasks",
                    $"Shipper đang có {active} nhiệm vụ lấy/trả chưa hoàn tất; hãy để Shipper hoàn tất hoặc phân công người khác trước khi xóa.",
                    ToDto(shipper, user, active));

            var now = DateTime.UtcNow;
            shipper.IsDeleted = true;
            shipper.DeletedAt = now;
            shipper.DeletedBy = CurrentUser.Id;
            shipper.IsActive = false;
            shipper.UpdatedAt = now;
            user.IsActive = false;
            user.UpdatedAt = now;
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
            await RevokeSessionsAsync(user.Id);
            await AuditAsync(shipper, ShipperAuditAction.Deleted, reason, null);
            await uow.CompleteAsync();
            return new(HttpStatusCode.OK, "Deleted", "Đã xóa Shipper; tài khoản không còn đăng nhập được, lịch sử đơn hàng được giữ nguyên.", null);
        }

        /// <summary>Result of one account action, shared by the single and the batch endpoints.</summary>
        public sealed record AccountActionResult(HttpStatusCode Status, string Code, string Message, ShipperDto? Data)
        {
            public BaseResponse<ShipperDto> ToResponse() => new()
            {
                Succeeded = Status == HttpStatusCode.OK, StatusCode = Status, Message = Message, Data = Data,
                Meta = new { code = Code }
            };
        }

        // Locks the row first: assignment takes the same lock, so a lock and a new task cannot cross.
        private async Task<AccountActionResult> LockCoreAsync(Guid id, string reason)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var shipper = await _shipperRepo.GetForUpdateAsync(id);
            if (shipper == null || !IsAdmin && shipper.MerchantId != CurrentUser.Id)
                return new(HttpStatusCode.NotFound, "NotFound", "Không tìm thấy Shipper!", null);
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (user == null || !await _roleRepo.AnyAsync(x => x.Id == user.RoleId && x.Name == "Shipper"))
                return new(HttpStatusCode.BadRequest, "NotShipper", "Tài khoản này không phải tài khoản Shipper.", null);
            if (!user.IsActive)
            {
                await uow.CompleteAsync();
                return new(HttpStatusCode.OK, "AlreadyLocked", "Tài khoản Shipper đã bị khóa trước đó.", await ToDtoAsync(shipper, user));
            }
            var active = (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id);
            if (active > 0)
                return new(HttpStatusCode.Conflict, "HasActiveTasks",
                    $"Shipper đang có {active} nhiệm vụ lấy/trả chưa hoàn tất; hãy để Shipper hoàn tất hoặc phân công người khác trước khi khóa.",
                    ToDto(shipper, user, active));

            var now = DateTime.UtcNow;
            user.IsActive = false;
            user.UpdatedAt = now;
            shipper.LockedAt = now;
            shipper.LockReason = reason;
            shipper.UpdatedAt = now;
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
            await RevokeSessionsAsync(user.Id);
            await AuditAsync(shipper, ShipperAuditAction.Locked, reason, null);
            await uow.CompleteAsync();
            return new(HttpStatusCode.OK, "Locked", "Đã khóa tài khoản Shipper; các phiên đăng nhập hiện tại đã bị thu hồi.", ToDto(shipper, user, 0));
        }

        // Refresh tokens are revoked; access tokens stop working at once because each request checks the session and User.IsActive.
        private async Task RevokeSessionsAsync(Guid userId)
        {
            var sessions = await _refreshTokenRepo.GetListAsync(x => x.UserId == userId && !x.IsRevoked);
            foreach (var session in sessions)
                session.IsRevoked = true;
            if (sessions.Count > 0)
                await _refreshTokenRepo.UpdateManyAsync(sessions, autoSave: true);
        }

        private async Task AuditAsync(Shipper shipper, ShipperAuditAction action, string? reason, string? details)
        {
            await _auditRepo.InsertAsync(new ShipperAudit(GuidGenerator.Create())
            {
                ShipperId = shipper.Id,
                MerchantId = shipper.MerchantId,
                Action = action,
                Reason = reason,
                ActorId = CurrentUser.Id ?? Guid.Empty,
                ActorRole = IsAdmin ? "Admin" : "Merchant",
                ActorName = CurrentUser.Name ?? CurrentUser.UserName ?? (IsAdmin ? "Admin" : "Merchant"),
                Details = details,
                CreatedAt = DateTime.UtcNow
            }, autoSave: true);
            await Audit.LogAsync(new AuditEntry("Shipper", action.ToString(), "Shipper", shipper.Id.ToString(), null)
            {
                Reason = reason, ChangedFields = details?.Split(',', StringSplitOptions.RemoveEmptyEntries), Details = new { merchantId = shipper.MerchantId }
            });
        }

        private async Task<ShipperDto> ToDtoAsync(Shipper shipper, User user) =>
            ToDto(shipper, user, (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id));

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // New identity images must be uploads of the current Merchant/Admin (same rule as other evidence images).
        private async Task<string?> ValidateSidesAsync(params string[] urls) =>
            (await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), urls, CurrentUser.Id!.Value, urls.Length)).Error;

        /// <summary>
        /// Applies identityNumber / front / back from an update (null keeps, "" clears) and returns the changed field names
        /// (never the values) for the audit. Front and back must end up both set or both empty.
        /// </summary>
        private async Task<(string? Error, List<string> Changes)> ApplyIdentityAsync(Shipper shipper, UpdateShipperCommand command)
        {
            var changes = new List<string>();
            if (command.IdentityNumber != null)
            {
                var number = Clean(command.IdentityNumber);
                if (number != null && !IdentityPattern.IsMatch(number))
                    return ("Số CMND/CCCD phải gồm 9 hoặc 12 chữ số.", changes);
                if (number != shipper.IdentityNumber)
                    changes.Add("identityNumber");
                shipper.IdentityNumber = number;
            }
            if (command.IdentityFrontImageUrl == null && command.IdentityBackImageUrl == null)
                return (null, changes);

            var front = command.IdentityFrontImageUrl == null ? shipper.IdentityFrontImageUrl : Clean(command.IdentityFrontImageUrl);
            var back = command.IdentityBackImageUrl == null ? shipper.IdentityBackImageUrl : Clean(command.IdentityBackImageUrl);
            if ((front == null) != (back == null))
                return ("Ảnh CMND/CCCD cần đủ mặt trước và mặt sau (hoặc xóa cả hai).", changes);
            if (front != null && front == back)
                return ("Ảnh mặt trước và mặt sau phải khác nhau.", changes);
            // Only newly sent images are checked; the stored ones were accepted before.
            var added = new[] { front, back }.Where(x => x != null && x != shipper.IdentityFrontImageUrl && x != shipper.IdentityBackImageUrl)
                .Select(x => x!).ToArray();
            if (added.Length > 0 && await ValidateSidesAsync(added) is { } imageError)
                return (imageError, changes);
            if (front != shipper.IdentityFrontImageUrl)
                changes.Add("identityFrontImage");
            if (back != shipper.IdentityBackImageUrl)
                changes.Add("identityBackImage");
            // Any image change replaces (or clears) the old images whose sides were unknown.
            if (!string.IsNullOrWhiteSpace(shipper.IdentityImages) && shipper.IdentityImages != "[]")
                changes.Add("legacyIdentityImagesReplaced");
            shipper.IdentityFrontImageUrl = front;
            shipper.IdentityBackImageUrl = back;
            if (changes.Contains("legacyIdentityImagesReplaced"))
                shipper.IdentityImages = null;
            return (null, changes);
        }

        private async Task<(Shipper? Shipper, User? User)> FindOwnedAsync(Guid id)
        {
            var shipper = await _shipperRepo.FindAsync(id);
            if (shipper == null || (!IsAdmin && shipper.MerchantId != CurrentUser.Id))
                return (null, null);
            return (shipper, await _userRepo.FindAsync(shipper.UserId));
        }

        private async Task<string?> DuplicateErrorAsync(string phone, string email, Guid? exceptUserId)
        {
            if (await _userRepo.AnyAsync(x => x.Phone == phone && x.Id != exceptUserId))
                return "Số điện thoại này đã được sử dụng.";
            if (await _userRepo.AnyAsync(x => x.Email.ToLower() == email && x.Id != exceptUserId))
                return "Email này đã được sử dụng.";
            return null;
        }

        private async Task<Dictionary<Guid, int>> ActiveTaskCountsAsync(List<Guid> shipperIds)
        {
            if (shipperIds.Count == 0) return [];
            var rows = await AsyncExecuter.ToListAsync(DeliveryTaskRules.ActiveDeliveries(await _orderRepo.GetQueryableAsync())
                .Where(x => shipperIds.Contains(x.ShipperId!.Value))
                .GroupBy(x => x.ShipperId!.Value)
                .Select(g => new { ShipperId = g.Key, Count = g.Count() }));
            return rows.ToDictionary(x => x.ShipperId, x => x.Count);
        }

        private static ShipperDto ToDto(Shipper s, User u, int activeTasks) => new()
        {
            Id = s.Id,
            UserId = u.Id,
            MerchantId = s.MerchantId,
            FullName = u.FullName,
            Phone = u.Phone,
            Email = u.Email,
            AvatarUrl = string.IsNullOrWhiteSpace(u.Avatar) ? null : u.Avatar,
            VehicleType = s.VehicleType,
            VehiclePlate = s.VehiclePlate,
            Status = s.Status,
            IsActive = s.IsActive,
            AccountActive = u.IsActive,
            LockedAt = u.IsActive ? null : s.LockedAt,
            LockReason = u.IsActive ? null : s.LockReason,
            TotalDeliveries = s.TotalDeliveries,
            ActiveTaskCount = activeTasks,
            Rating = s.Rating,
            IdentityNumber = s.IdentityNumber,
            IdentityFrontImageUrl = s.IdentityFrontImageUrl,
            IdentityBackImageUrl = s.IdentityBackImageUrl,
            IdentityImageUrls = s.IdentityFrontImageUrl != null && s.IdentityBackImageUrl != null
                ? [s.IdentityFrontImageUrl, s.IdentityBackImageUrl]
                : string.IsNullOrWhiteSpace(s.IdentityImages) ? [] : JsonSerializer.Deserialize<List<string>>(s.IdentityImages) ?? [],
            IdentityImageSides = s.IdentityFrontImageUrl != null && s.IdentityBackImageUrl != null ? "FrontBack"
                : string.IsNullOrWhiteSpace(s.IdentityImages) || s.IdentityImages == "[]" ? "None" : "Unspecified",
            CreatedAt = s.CreatedAt,
            Revision = s.Revision,
            CredentialDelivery = s.CredentialDelivery,
            CredentialSentAt = s.CredentialSentAt
        };
    }
}
