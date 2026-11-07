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
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

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
            if (sorts.Any(x => !string.Equals(x.Field, "Rating", StringComparison.OrdinalIgnoreCase)))
                return new PaginatedResult<ShipperDto>
                {
                    Succeeded = false, StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message = "Chỉ hỗ trợ sắp xếp theo Rating.", CurrentPage = query.Page, PageSize = query.Take
                };

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
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.IdentityImageUrls,
                CurrentUser.Id!.Value, 0, out var images);
            if (imageError != null)
                return BadRequest<ShipperDto>(imageError);
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
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
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
                Status = ShipperStatus.Available,
                IsActive = true
            };

            using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                await _userRepo.InsertAsync(user, autoSave: true);
                await _shipperRepo.InsertAsync(shipper, autoSave: true);
                await uow.CompleteAsync();
            }

            // Spec 6a: a failed delivery is logged but the account stays created.
            var sent = await _emailSender.SendAccountCredentialsAsync(email, fullName, temporaryPassword);
            if (!sent)
                _logger.LogWarning("Could not send Shipper credentials to {Email}.", email);
            return Created(ToDto(shipper, user, 0), sent
                ? "Thêm Shipper thành công! Thông tin đăng nhập đã được gửi qua email."
                : "Thêm Shipper thành công! Chưa gửi được email thông tin đăng nhập; Shipper có thể dùng Quên mật khẩu.");
        }

        public async Task<BaseResponse<ShipperDto>> UpdateAsync(Guid id, UpdateShipperCommand command)
        {
            var (shipper, user) = await FindOwnedAsync(id);
            if (shipper == null || user == null)
                return NotFound<ShipperDto>("Không tìm thấy Shipper!");

            var (error, fullName, phone, email, plate) = ShipperRules.Validate(command.FullName, command.Phone, command.Email,
                command.VehicleType, command.VehiclePlate);
            if (error != null)
                return BadRequest<ShipperDto>(error);
            var duplicate = await DuplicateErrorAsync(phone, email, user.Id);
            if (duplicate != null)
                return BadRequest<ShipperDto>(duplicate);
            var active = (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id);
            if (!command.IsActive && shipper.IsActive && active > 0)
                return BadRequest<ShipperDto>("Shipper đang có nhiệm vụ lấy/trả chưa hoàn tất; không thể chuyển sang Tạm nghỉ.");

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
            var (shipper, _) = await FindOwnedAsync(id);
            if (shipper == null)
                return NotFound<List<ShipperAuditDto>>("Không tìm thấy Shipper!");
            var rows = await AsyncExecuter.ToListAsync((await _auditRepo.GetQueryableAsync())
                .Where(x => x.ShipperId == id).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));
            return Success(rows.Select(x => new ShipperAuditDto
            {
                Id = x.Id, Action = x.Action, Reason = x.Reason, ActorId = x.ActorId, ActorRole = x.ActorRole,
                ActorName = x.ActorName, Details = x.Details, CreatedAt = x.CreatedAt
            }).ToList());
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
        }

        private async Task<ShipperDto> ToDtoAsync(Shipper shipper, User user) =>
            ToDto(shipper, user, (await ActiveTaskCountsAsync([shipper.Id])).GetValueOrDefault(shipper.Id));

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
            IdentityImageUrls = string.IsNullOrWhiteSpace(s.IdentityImages)
                ? [] : JsonSerializer.Deserialize<List<string>>(s.IdentityImages) ?? [],
            CreatedAt = s.CreatedAt
        };
    }
}
