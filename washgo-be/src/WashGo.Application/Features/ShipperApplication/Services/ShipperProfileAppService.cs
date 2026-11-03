using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;

namespace WashGo.Application.Features.ShipperApplication.Services
{
    /// <summary>UC 4.1.3–4.1.4: Shipper xem và tự cập nhật hồ sơ cá nhân.</summary>
    public class ShipperProfileAppService : WashGoAppService, IShipperProfileAppService
    {
        private readonly IShipperRepository _shipperRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IConfiguration _configuration;

        public ShipperProfileAppService(
            IShipperRepository shipperRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IConfiguration configuration)
        {
            _shipperRepo = shipperRepo;
            _userRepo = userRepo;
            _merchantRepo = merchantRepo;
            _configuration = configuration;
        }

        public async Task<BaseResponse<ShipperProfileDto>> GetMyProfileAsync()
        {
            var (shipper, user) = await FindMineAsync();
            if (shipper == null || user == null)
                return NotFound<ShipperProfileDto>("Không tìm thấy hồ sơ Shipper.");
            return Success(await ToDtoAsync(shipper, user));
        }

        public async Task<BaseResponse<ShipperProfileDto>> UpdateMyProfileAsync(UpdateShipperProfileCommand command)
        {
            var (shipper, user) = await FindMineAsync();
            if (shipper == null || user == null)
                return NotFound<ShipperProfileDto>("Không tìm thấy hồ sơ Shipper.");

            // Email is not editable here; validate it as-is so the shared rules apply unchanged.
            var (error, fullName, phone, _, plate) = ShipperRules.Validate(command.FullName, command.Phone, user.Email,
                command.VehicleType, command.VehiclePlate);
            if (error != null)
                return BadRequest<ShipperProfileDto>(error);
            if (await _userRepo.AnyAsync(x => x.Phone == phone && x.Id != user.Id))
                return BadRequest<ShipperProfileDto>("Số điện thoại này đã được sử dụng.");

            var avatar = user.Avatar;
            if (command.AvatarUrl != null)
            {
                avatar = string.IsNullOrWhiteSpace(command.AvatarUrl) ? null : command.AvatarUrl.Trim();
                // A new avatar must be an existing upload of this account.
                if (avatar != null && avatar != user.Avatar &&
                    UploadStorage.ValidateOwnedImages(_configuration, [avatar], user.Id, 1, out _) != null)
                    return BadRequest<ShipperProfileDto>("Không thể upload ảnh. Vui lòng thử lại.");
            }

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var now = DateTime.UtcNow;
            user.FullName = fullName;
            user.Phone = phone;
            user.Avatar = avatar;
            user.UpdatedAt = now;
            shipper.VehicleType = command.VehicleType;
            shipper.VehiclePlate = plate;
            shipper.UpdatedAt = now;
            await _userRepo.UpdateAsync(user, autoSave: true);
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
            await uow.CompleteAsync();
            return Success(await ToDtoAsync(shipper, user), "Cập nhật hồ sơ thành công!");
        }

        private async Task<(Shipper? Shipper, User? User)> FindMineAsync()
        {
            if (!CurrentUser.Id.HasValue)
                return (null, null);
            var shipper = await _shipperRepo.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id.Value);
            return shipper == null ? (null, null) : (shipper, await _userRepo.FindAsync(shipper.UserId));
        }

        private async Task<ShipperProfileDto> ToDtoAsync(Shipper s, User u) => new()
        {
            Id = s.Id,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            AvatarUrl = u.Avatar,
            VehicleType = s.VehicleType,
            VehiclePlate = s.VehiclePlate,
            Status = s.Status,
            IsActive = s.IsActive,
            TotalDeliveries = s.TotalDeliveries,
            Rating = s.Rating,
            MerchantId = s.MerchantId,
            MerchantName = (await _merchantRepo.FindAsync(s.MerchantId))?.BusinessName ?? string.Empty
        };
    }
}
