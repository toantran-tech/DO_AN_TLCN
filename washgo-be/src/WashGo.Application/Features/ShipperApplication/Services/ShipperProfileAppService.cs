using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Results;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;

namespace WashGo.Application.Features.ShipperApplication.Services;

public class ShipperProfileAppService(IShipperRepository shippers, IRepository<User, Guid> users,
    IRepository<Merchant, Guid> merchants, IRepository<MerchantImage, Guid> images,
    IConfiguration configuration, AdministrativeWriteGuard guard) : WashGoAppService, IShipperProfileAppService
{
    private const string Module = "ShipperProfile";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<BaseResponse<ShipperProfileDto>> GetMyProfileAsync()
    {
        var (shipper, user) = await FindMineAsync();
        return shipper == null || user == null ? Missing() : Success(await ToDtoAsync(shipper, user));
    }

    public async Task<BaseResponse<ShipperProfileDto>> GetOperationAsync(Guid operationId)
    {
        var row = await guard.FindAsync(operationId, CurrentUser.Id ?? Guid.Empty, Module);
        return row == null ? NotFound<ShipperProfileDto>("Chưa có kết quả được lưu; chỉ gửi lại cùng operationId.")
            : JsonSerializer.Deserialize<BaseResponse<ShipperProfileDto>>(row.ResponseJson, Json)!;
    }

    public async Task<BaseResponse<ShipperProfileDto>> UpdateMyProfileAsync(UpdateShipperProfileCommand command)
    {
        var actor = CurrentUser.Id ?? Guid.Empty;
        if (command.OperationId == null || string.IsNullOrWhiteSpace(command.ExpectedRevision))
            return Error("ProfileGuardRequired", "Cần expectedRevision và operationId.", HttpStatusCode.BadRequest);
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var replay = await guard.ReplayAsync<ShipperProfileDto>(command.OperationId, actor, Module, "Update", actor.ToString(), command);
        if (replay != null) return replay;
        // Project only the ID before locking: tracking a Shipper before FOR UPDATE can retain a stale revision.
        var id = await AsyncExecuter.FirstOrDefaultAsync((await shippers.GetQueryableAsync())
            .Where(x => x.UserId == actor).Select(x => (Guid?)x.Id));
        if (!id.HasValue) return Missing();
        var shipper = await shippers.GetForUpdateAsync(id.Value);
        var user = await users.FindAsync(actor);
        if (shipper == null || user == null || !user.IsActive) return Missing();
        if (command.ExpectedRevision != Version(shipper, user))
            return Error("RevisionMismatch", "Hồ sơ đã thay đổi; vui lòng tải lại.", HttpStatusCode.Conflict, await ToDtoAsync(shipper, user));
        var (error, fullName, phone, _, plate) = ShipperRules.Validate(command.FullName, command.Phone, user.Email, command.VehicleType, command.VehiclePlate);
        if (error != null) return Error("InvalidProfile", error, HttpStatusCode.BadRequest);
        // Same normalized phone lock is shared by self/management writers in this round.
        await guard.LockAsync($"shipper-phone:{phone}");
        if (await users.AnyAsync(x => x.Phone == phone && x.Id != user.Id))
            return Error("PhoneAlreadyUsed", "Số điện thoại này đã được sử dụng.", HttpStatusCode.BadRequest);
        var avatar = user.Avatar;
        if (command.AvatarUrl != null)
        {
            avatar = string.IsNullOrWhiteSpace(command.AvatarUrl) ? null : command.AvatarUrl.Trim();
            if (avatar?.Length > 2048 || avatar != null && avatar != user.Avatar &&
                UploadStorage.ValidateOwnedImages(configuration, [avatar], user.Id, 1, out _) != null)
                return Error("AvatarNotOwned", "Ảnh không hợp lệ hoặc không thuộc tài khoản.", HttpStatusCode.BadRequest);
        }
        var now = DateTime.UtcNow;
        user.FullName = fullName; user.Phone = phone; user.Avatar = avatar; user.UpdatedAt = now;
        shipper.VehicleType = command.VehicleType; shipper.VehiclePlate = plate; shipper.UpdatedAt = now;
        await users.UpdateAsync(user, autoSave: true);
        await shippers.UpdateAsync(shipper, autoSave: true);
        await Audit.LogAsync(new AuditEntry("Shipper", "SelfUpdate", "Shipper", shipper.Id.ToString(), null));
        var response = Success(await ToDtoAsync(shipper, user), "Cập nhật hồ sơ thành công!");
        await guard.RecordAsync(command.OperationId, actor, Module, "Update", actor.ToString(), command, response);
        await uow.CompleteAsync();
        return response;
    }

    private async Task<(Shipper? Shipper, User? User)> FindMineAsync()
    {
        if (!CurrentUser.Id.HasValue) return (null, null);
        var shipper = await shippers.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id);
        var user = shipper == null ? null : await users.FindAsync(shipper.UserId);
        return (shipper, user?.IsActive == true ? user : null);
    }

    private static string Version(Shipper s, User u) => AdministrativeWriteGuard.Version(new
    {
        s.Revision, s.MerchantId, s.IsActive, s.VehicleType, s.VehiclePlate,
        u.FullName, u.Email, u.Phone, u.Avatar
    });

    private async Task<ShipperProfileDto> ToDtoAsync(Shipper s, User u)
    {
        var merchant = await merchants.FindAsync(s.MerchantId);
        var merchantUser = merchant == null ? null : await users.FindAsync(merchant.Id);
        return new ShipperProfileDto
        {
            Id = s.Id, FullName = u.FullName, Email = u.Email, Phone = u.Phone, AvatarUrl = u.Avatar,
            VehicleType = s.VehicleType, VehiclePlate = s.VehiclePlate, Status = s.Status, IsActive = s.IsActive,
            TotalDeliveries = s.TotalDeliveries, Rating = s.Rating, MerchantId = s.MerchantId,
            MerchantName = merchant?.BusinessName ?? string.Empty, MerchantPhone = merchant?.Phone,
            MerchantEmail = merchantUser?.Email,
            MerchantAddress = merchant == null ? null : string.Join(", ", new[] { merchant.Address, merchant.District, merchant.City }.Where(x => !string.IsNullOrWhiteSpace(x))),
            MerchantImageUrls = merchant == null ? [] : (await images.GetListAsync(x => x.MerchantId == merchant.Id))
                .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList(),
            Revision = Version(s, u), CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt > u.UpdatedAt ? s.UpdatedAt : u.UpdatedAt
        };
    }

    private static BaseResponse<ShipperProfileDto> Missing() => Error("ShipperProfileMissing", "Không tìm thấy hồ sơ Shipper. Vui lòng liên hệ Merchant/Admin.", HttpStatusCode.NotFound);
    private static BaseResponse<ShipperProfileDto> Error(string code, string message, HttpStatusCode status, ShipperProfileDto? data = null) =>
        AdministrativeWriteGuard.Error(status, code, message, data);
}
