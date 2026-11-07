using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Results;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Feedback;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.MerchantApplication.Services
{
    /// <summary>
    /// UC 2.1.2: Merchant xem hồ sơ cửa hàng và gửi yêu cầu cập nhật (tên, địa chỉ, SĐT, giờ mở cửa, hình ảnh);
    /// hồ sơ chỉ thay đổi khi Admin phê duyệt.
    /// </summary>
    public class MerchantProfileAppService : WashGoAppService, IMerchantProfileAppService
    {
        private const string StoreImageType = "store";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IRepository<MerchantProfileRequest, Guid> _requestRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<MerchantImage, Guid> _imageRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<OrderRating, Guid> _ratingRepo;
        private readonly NotificationPublisher _notifications;
        private readonly IConfiguration _configuration;

        public MerchantProfileAppService(
            IRepository<MerchantProfileRequest, Guid> requestRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<MerchantImage, Guid> imageRepo,
            IRepository<User, Guid> userRepo,
            IRepository<OrderRating, Guid> ratingRepo,
            NotificationPublisher notifications,
            IConfiguration configuration)
        {
            _requestRepo = requestRepo;
            _merchantRepo = merchantRepo;
            _imageRepo = imageRepo;
            _userRepo = userRepo;
            _ratingRepo = ratingRepo;
            _notifications = notifications;
            _configuration = configuration;
        }

        // Merchant accounts share their id with the Merchant profile.
        private Guid MerchantId => CurrentUser.Id!.Value;

        public async Task<BaseResponse<MerchantProfileDto>> GetMyProfileAsync()
        {
            var merchant = await _merchantRepo.FindAsync(MerchantId);
            if (merchant == null)
                return NotFound<MerchantProfileDto>("Không tìm thấy hồ sơ cửa hàng!");
            var user = await _userRepo.FindAsync(merchant.Id);
            var pending = await _requestRepo.FirstOrDefaultAsync(x => x.MerchantId == merchant.Id && x.Status == ServiceApprovalStatus.Pending);
            var publicRatingCount = await _ratingRepo.CountAsync(x => x.MerchantId == merchant.Id && !x.IsHidden);
            return Success(new MerchantProfileDto
            {
                Id = merchant.Id,
                BusinessName = merchant.BusinessName,
                TaxCode = merchant.TaxCode,
                Email = user?.Email ?? string.Empty,
                Address = merchant.Address,
                District = merchant.District,
                City = merchant.City,
                Latitude = merchant.Latitude,
                Longitude = merchant.Longitude,
                Phone = merchant.Phone,
                OpeningHours = merchant.OpeningHours,
                ImageUrls = await StoreImagesAsync(merchant.Id),
                Status = merchant.Status,
                Rating = merchant.Rating,
                TotalOrders = merchant.TotalOrders,
                CommissionRate = merchant.CommissionRate,
                ServiceRadiusKm = merchant.ServiceRadiusKm,
                MaxCapacity = merchant.MaxCapacity,
                ContractStartDate = merchant.ContractStartDate,
                ContractEndDate = merchant.ContractEndDate,
                JoinedAt = merchant.ApprovedAt ?? merchant.CreatedAt,
                PublicRatingCount = publicRatingCount,
                PendingRequest = pending == null ? null : ToDto(pending, merchant.BusinessName)
            });
        }

        public async Task<BaseResponse<MerchantProfileRequestDto>> RequestUpdateAsync(MerchantProfileUpdateCommand command)
        {
            var merchant = await _merchantRepo.FindAsync(MerchantId);
            if (merchant == null)
                return NotFound<MerchantProfileRequestDto>("Không tìm thấy hồ sơ cửa hàng!");
            if (merchant.Status != MerchantStatus.Active)
                return BadRequest<MerchantProfileRequestDto>("Tài khoản Merchant không hoạt động.");

            var name = command.BusinessName?.Trim() ?? string.Empty;
            var address = command.Address?.Trim() ?? string.Empty;
            var city = command.City?.Trim() ?? string.Empty;
            var phone = command.Phone?.Trim() ?? string.Empty;
            var hours = MerchantRules.NormalizeJson(command.OpeningHours);
            if (name.Length is 0 or > 200)
                return BadRequest<MerchantProfileRequestDto>("Tên cửa hàng phải có từ 1 đến 200 ký tự.");
            if (address.Length is 0 or > 500)
                return BadRequest<MerchantProfileRequestDto>("Địa chỉ không được để trống (tối đa 500 ký tự).");
            if (city.Length is 0 or > 100)
                return BadRequest<MerchantProfileRequestDto>("Tỉnh/Thành phố không được để trống (tối đa 100 ký tự).");
            if (command.Latitude.HasValue && (command.Latitude.Value < -90m || command.Latitude.Value > 90m))
                return BadRequest<MerchantProfileRequestDto>("Vĩ độ (Latitude) không hợp lệ (phải từ -90 đến 90).");
            if (command.Longitude.HasValue && (command.Longitude.Value < -180m || command.Longitude.Value > 180m))
                return BadRequest<MerchantProfileRequestDto>("Kinh độ (Longitude) không hợp lệ (phải từ -180 đến 180).");

            var digits = MerchantRules.NormalizePhone(phone);
            if (digits == null)
                return BadRequest<MerchantProfileRequestDto>(MerchantRules.InvalidPhone);
            if (hours?.Length > 1000)
                return BadRequest<MerchantProfileRequestDto>("Giờ mở cửa quá dài.");

            var current = new MerchantProfileDataDto
            {
                BusinessName = merchant.BusinessName,
                Address = merchant.Address,
                City = merchant.City,
                Latitude = merchant.Latitude,
                Longitude = merchant.Longitude,
                Phone = merchant.Phone,
                OpeningHours = MerchantRules.NormalizeJson(merchant.OpeningHours),
                ImageUrls = await StoreImagesAsync(merchant.Id)
            };
            var images = current.ImageUrls;
            if (command.ImageUrls != null)
            {
                // Images already on the profile stay valid; new ones must be uploads of this Merchant.
                var added = command.ImageUrls.Where(x => !current.ImageUrls.Contains(x?.Trim() ?? string.Empty)).ToList();
                if (UploadStorage.ValidateOwnedImages(_configuration, added, merchant.Id, 0, out _) != null)
                    return BadRequest<MerchantProfileRequestDto>("Không thể upload ảnh. Vui lòng thử lại.");
                images = command.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                if (images.Count > UploadStorage.MaxImagesPerSubmission)
                    return BadRequest<MerchantProfileRequestDto>($"Tối đa {UploadStorage.MaxImagesPerSubmission} ảnh.");
            }
            var proposed = new MerchantProfileDataDto
            {
                BusinessName = name,
                Address = address,
                City = city,
                Latitude = command.Latitude ?? merchant.Latitude,
                Longitude = command.Longitude ?? merchant.Longitude,
                Phone = digits,
                OpeningHours = hours,
                ImageUrls = images
            };
            if (Changes(current, proposed).Count == 0)
                return BadRequest<MerchantProfileRequestDto>("Không có thông tin nào thay đổi.");
            if (await _requestRepo.AnyAsync(x => x.MerchantId == merchant.Id && x.Status == ServiceApprovalStatus.Pending))
                return BadRequest<MerchantProfileRequestDto>("Bạn đang có yêu cầu cập nhật chờ Admin phê duyệt.");

            var request = new MerchantProfileRequest(GuidGenerator.Create())
            {
                MerchantId = merchant.Id,
                ProposedData = JsonSerializer.Serialize(proposed, Json),
                CurrentData = JsonSerializer.Serialize(current, Json),
                Status = ServiceApprovalStatus.Pending
            };
            await _requestRepo.InsertAsync(request, autoSave: true);
            await _notifications.SendToAdminsAsync(NotificationType.MerchantProfileUpdateRequested, "Yêu cầu cập nhật hồ sơ Merchant",
                $"Merchant {merchant.BusinessName} gửi yêu cầu cập nhật hồ sơ cửa hàng, cần phê duyệt.",
                "merchant-profile-request", request.Id);
            return Created(ToDto(request, merchant.BusinessName), "Yêu cầu cập nhật đã được gửi. Vui lòng chờ Admin phê duyệt.");
        }

        public async Task<PaginatedResult<MerchantProfileRequestDto>> GetRequestsAsync(MerchantProfileRequestFilterQuery query)
        {
            var queryable = await _requestRepo.GetQueryableAsync();
            // Merchants only see their own requests.
            var merchantId = CurrentUser.IsInRole("Admin") ? query.MerchantId : MerchantId;
            if (merchantId.HasValue)
                queryable = queryable.Where(x => x.MerchantId == merchantId.Value);
            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(queryable
                .OrderBy(x => x.Status == ServiceApprovalStatus.Pending ? 0 : 1).ThenByDescending(x => x.CreatedAt)
                .Skip((page - 1) * take).Take(take));
            var names = await MerchantNamesAsync(items.Select(x => x.MerchantId));
            return Success(items.Select(x => ToDto(x, names.GetValueOrDefault(x.MerchantId, string.Empty))).ToList(), total, page, take,
                total == 0 ? "Chưa có yêu cầu cập nhật hồ sơ nào." : null);
        }

        public async Task<BaseResponse<MerchantProfileRequestDto>> GetRequestAsync(Guid id)
        {
            var request = await _requestRepo.FindAsync(id);
            if (request == null || (!CurrentUser.IsInRole("Admin") && request.MerchantId != MerchantId))
                return NotFound<MerchantProfileRequestDto>("Không tìm thấy yêu cầu cập nhật!");
            var names = await MerchantNamesAsync([request.MerchantId]);
            return Success(ToDto(request, names.GetValueOrDefault(request.MerchantId, string.Empty)));
        }

        public async Task<BaseResponse<MerchantProfileRequestDto>> ApproveAsync(Guid id)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var request = await _requestRepo.FindAsync(id);
            if (request == null)
                return NotFound<MerchantProfileRequestDto>("Không tìm thấy yêu cầu cập nhật!");
            if (request.Status != ServiceApprovalStatus.Pending)
                return BadRequest<MerchantProfileRequestDto>("Yêu cầu đã được xử lý trước đó.");
            var merchant = await _merchantRepo.FindAsync(request.MerchantId);
            var data = Read(request.ProposedData);
            if (merchant == null)
                return BadRequest<MerchantProfileRequestDto>("Merchant của yêu cầu không còn tồn tại.");

            var now = DateTime.UtcNow;
            merchant.BusinessName = data.BusinessName;
            merchant.Address = data.Address;
            if (!string.IsNullOrWhiteSpace(data.City))
                merchant.City = data.City;
            if (data.Latitude.HasValue && data.Longitude.HasValue)
            {
                merchant.Latitude = data.Latitude.Value;
                merchant.Longitude = data.Longitude.Value;
            }
            merchant.Phone = data.Phone;
            merchant.OpeningHours = data.OpeningHours;
            merchant.UpdatedAt = now;
            await _merchantRepo.UpdateAsync(merchant, autoSave: true);
            await ReplaceStoreImagesAsync(merchant.Id, data.ImageUrls);

            request.Status = ServiceApprovalStatus.Approved;
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = now;
            request.UpdatedAt = now;
            await _requestRepo.UpdateAsync(request, autoSave: true);
            await _notifications.SendAsync([merchant.Id], NotificationType.MerchantProfileReviewed, "Hồ sơ đã được cập nhật",
                "Yêu cầu cập nhật hồ sơ cửa hàng của bạn đã được Admin phê duyệt.", "merchant-profile-request", request.Id);
            await uow.CompleteAsync();
            return Success(ToDto(request, merchant.BusinessName), "Đã phê duyệt yêu cầu cập nhật!");
        }

        public async Task<BaseResponse<MerchantProfileRequestDto>> RejectAsync(Guid id, RejectMerchantProfileRequestCommand command)
        {
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                return BadRequest<MerchantProfileRequestDto>("Vui lòng nhập lý do từ chối (tối đa 1000 ký tự).");
            var request = await _requestRepo.FindAsync(id);
            if (request == null)
                return NotFound<MerchantProfileRequestDto>("Không tìm thấy yêu cầu cập nhật!");
            if (request.Status != ServiceApprovalStatus.Pending)
                return BadRequest<MerchantProfileRequestDto>("Yêu cầu đã được xử lý trước đó.");

            request.Status = ServiceApprovalStatus.Rejected;
            request.RejectionReason = reason;
            request.ReviewedBy = CurrentUser.Id;
            request.ReviewedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            await _requestRepo.UpdateAsync(request, autoSave: true);
            await _notifications.SendAsync([request.MerchantId], NotificationType.MerchantProfileReviewed, "Yêu cầu cập nhật bị từ chối",
                $"Yêu cầu cập nhật hồ sơ cửa hàng của bạn đã bị từ chối. Lý do: {reason}", "merchant-profile-request", request.Id);
            var names = await MerchantNamesAsync([request.MerchantId]);
            return Success(ToDto(request, names.GetValueOrDefault(request.MerchantId, string.Empty)), "Đã từ chối yêu cầu cập nhật!");
        }

        private async Task<List<string>> StoreImagesAsync(Guid merchantId) =>
            (await _imageRepo.GetListAsync(x => x.MerchantId == merchantId && x.ImageType == StoreImageType))
                .OrderBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList();

        private async Task ReplaceStoreImagesAsync(Guid merchantId, List<string> urls)
        {
            var existing = await _imageRepo.GetListAsync(x => x.MerchantId == merchantId && x.ImageType == StoreImageType);
            if (existing.Select(x => x.SecureUrl).SequenceEqual(urls))
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

        private async Task<Dictionary<Guid, string>> MerchantNamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            return (await _merchantRepo.GetListAsync(x => list.Contains(x.Id))).ToDictionary(x => x.Id, x => x.BusinessName);
        }

        private static List<string> Changes(MerchantProfileDataDto current, MerchantProfileDataDto proposed)
        {
            var changed = new List<string>();
            if (current.BusinessName != proposed.BusinessName) changed.Add("businessName");
            if (current.Address != proposed.Address) changed.Add("address");
            if (current.City != proposed.City) changed.Add("city");
            if (current.Latitude != proposed.Latitude) changed.Add("latitude");
            if (current.Longitude != proposed.Longitude) changed.Add("longitude");
            if (current.Phone != proposed.Phone) changed.Add("phone");
            if (current.OpeningHours != proposed.OpeningHours) changed.Add("openingHours");
            if (!current.ImageUrls.SequenceEqual(proposed.ImageUrls)) changed.Add("imageUrls");
            return changed;
        }

        private static MerchantProfileDataDto Read(string json) =>
            JsonSerializer.Deserialize<MerchantProfileDataDto>(json, Json) ?? new MerchantProfileDataDto();

        private static MerchantProfileRequestDto ToDto(MerchantProfileRequest x, string merchantName)
        {
            var proposed = Read(x.ProposedData);
            var current = Read(x.CurrentData);
            return new MerchantProfileRequestDto
            {
                Id = x.Id,
                MerchantId = x.MerchantId,
                MerchantName = merchantName,
                Status = x.Status,
                Proposed = proposed,
                Current = current,
                ChangedFields = Changes(current, proposed),
                RejectionReason = x.RejectionReason,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt
            };
        }
    }
}
