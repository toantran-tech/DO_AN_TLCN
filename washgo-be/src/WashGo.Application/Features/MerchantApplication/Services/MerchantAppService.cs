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
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

using WashGo.Application.Features.SystemSettingApplication.Services;

namespace WashGo.Application.Features.MerchantApplication.Services
{
    /// <summary>UC 3.2.1–3.2.4: Admin xem, thêm, chỉnh sửa, khóa/mở khóa Merchant.</summary>
    public class MerchantAppService : WashGoAppService, IMerchantAppService
    {
        private const string StoreImageType = "store";

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
        private readonly IWashOrderRepository _orderRepo;
        private readonly SystemSettingProvider _settings;
        private readonly IAuthEmailSender _emailSender;
        private readonly NotificationPublisher _notifications;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MerchantAppService> _logger;

        public MerchantAppService(
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Role, Guid> roleRepo,
            IRepository<RefreshToken, Guid> tokenRepo,
            IRepository<MerchantServiceArea, Guid> msaRepo,
            IRepository<ServiceArea, Guid> areaRepo,
            IRepository<MerchantImage, Guid> imageRepo,
            IWashOrderRepository orderRepo,
            SystemSettingProvider settings,
            IAuthEmailSender emailSender,
            NotificationPublisher notifications,
            IConfiguration configuration,
            ILogger<MerchantAppService> logger)
        {
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _tokenRepo = tokenRepo;
            _msaRepo = msaRepo;
            _areaRepo = areaRepo;
            _imageRepo = imageRepo;
            _orderRepo = orderRepo;
            _settings = settings;
            _emailSender = emailSender;
            _notifications = notifications;
            _configuration = configuration;
            _logger = logger;
        }

        // UC 3.2.1
        public async Task<PaginatedResult<MerchantResultDto>> GetListAsync(MerchantFilterQuery query)
        {
            var queryable = await _merchantRepo.GetQueryableAsync();

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
            var items = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(x => x.BusinessName)
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
        public async Task<BaseResponse<MerchantResultDto>> CreateAsync(CreateMerchantCommand command)
        {
            var email = command.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!MerchantRules.IsValidEmail(email))
                return BadRequest<MerchantResultDto>("Email không hợp lệ.");
            var (error, phone) = await ValidateAsync(command, email, null);
            if (error != null)
                return BadRequest<MerchantResultDto>(error);
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, CurrentUser.Id!.Value, 0, out var images);
            if (imageError != null)
                return BadRequest<MerchantResultDto>(imageError);
            var role = await _roleRepo.FirstOrDefaultAsync(x => x.Name == "Merchant");
            if (role == null)
                return BadRequest<MerchantResultDto>("Có lỗi xảy ra. Vui lòng thử lại.");

            var temporaryPassword = TemporaryPassword.Generate();
            var user = new User(GuidGenerator.Create())
            {
                RoleId = role.Id,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                AuthProvider = AuthProvider.Local,
                FullName = command.BusinessName.Trim(),
                IsActive = true
            };
            // Merchant accounts share their id with the Merchant profile.
            var entity = new Merchant(user.Id)
            {
                Status = MerchantStatus.Active,
                Rating = 5.0m,
                CommissionRate = command.CommissionRate ?? (await _settings.GetAsync()).DefaultCommissionRate,
                ApprovedBy = CurrentUser.Id,
                ApprovedAt = DateTime.UtcNow
            };
            Apply(entity, command, phone!);

            using (var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                await _userRepo.InsertAsync(user, autoSave: true);
                await _merchantRepo.InsertAsync(entity, autoSave: true);
                await ReplaceImagesAsync(entity.Id, images);
                await ReplaceAreasAsync(entity.Id, command.ServiceAreaIds ?? []);
                await uow.CompleteAsync();
            }

            // Spec 6a: a failed delivery is logged but the account stays created.
            var sent = await _emailSender.SendAccountCredentialsAsync(email, entity.BusinessName, temporaryPassword);
            if (!sent)
                _logger.LogWarning("Could not send Merchant credentials to {Email}.", email);
            return Created((await ToDtosAsync([entity]))[0], "Thêm Merchant thành công!");
        }

        // UC 3.2.3
        public async Task<BaseResponse<MerchantResultDto>> UpdateAsync(UpdateMerchantCommand command)
        {
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

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            Apply(entity, command, phone!);
            if (command.CommissionRate.HasValue)
                entity.CommissionRate = command.CommissionRate.Value;
            if (command.Status.HasValue && command.Status.Value != entity.Status)
            {
                entity.Status = command.Status.Value;
                user.IsActive = entity.Status == MerchantStatus.Active;
            }
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
            await uow.CompleteAsync();

            return Success((await ToDtosAsync([entity]))[0], "Cập nhật Merchant thành công!");
        }

        // UC 3.2.4: lock needs a reason and, with unfinished orders, an explicit confirmation.
        public async Task<BaseResponse<MerchantResultDto>> UpdateStatusAsync(UpdateMerchantStatusCommand command)
        {
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
            if (entity.Status == command.Status)
                return Success((await ToDtosAsync([entity]))[0], "Merchant đã ở trạng thái này.");

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

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            entity.Status = command.Status;
            entity.UpdatedAt = now;
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
            await uow.CompleteAsync();
            return Success((await ToDtosAsync([entity]))[0], locking ? "Đã khóa Merchant thành công!" : "Đã mở khóa Merchant thành công!");
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
            if (command.ServiceRadiusKm is < 1 or > 100)
                return ("Bán kính phục vụ phải từ 1 đến 100 km.", null);
            if (command.MaxCapacity is < 1 or > 100000)
                return ("Sức chứa tối đa phải lớn hơn 0.", null);
            if (command.DepositAmount < 0)
                return ("Tiền ký quỹ không hợp lệ.", null);
            if (command.ContractStartDate.HasValue && command.ContractEndDate.HasValue &&
                command.ContractEndDate.Value < command.ContractStartDate.Value)
                return ("Ngày kết thúc hợp đồng phải sau ngày bắt đầu.", null);
            if (!string.IsNullOrWhiteSpace(command.SlaDocUrl) &&
                (command.SlaDocUrl.Length > 500 || !Uri.TryCreate(command.SlaDocUrl.Trim(), UriKind.RelativeOrAbsolute, out _)))
                return ("Không thể upload file. Vui lòng thử lại.", null);
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
            entity.ContractStartDate = command.ContractStartDate;
            entity.ContractEndDate = command.ContractEndDate;
            entity.SlaDocUrl = string.IsNullOrWhiteSpace(command.SlaDocUrl) ? null : command.SlaDocUrl.Trim();
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
            await _msaRepo.DeleteManyAsync(existing, autoSave: true);
            await _msaRepo.InsertManyAsync(areaIds.Distinct().Select(areaId => new MerchantServiceArea(GuidGenerator.Create())
            {
                MerchantId = merchantId,
                ServiceAreaId = areaId,
                Priority = 1,
                IsActive = true,
                AssignedBy = CurrentUser.Id,
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }), autoSave: true);
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
            foreach (var dto in dtos)
            {
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
    }
}
